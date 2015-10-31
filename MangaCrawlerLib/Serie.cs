using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TomanuExtensions.Utils;
using TomanuExtensions;

namespace MangaCrawlerLib
{
    public class Serie : Entity
    {
        #region ChaptersCachedList
        private class ChaptersCachedList : CachedList<Chapter>
        {
            private readonly Serie _serie;

            public ChaptersCachedList(Serie serie)
            {
                _serie = serie;
            }

            protected override void EnsureLoaded()
            {
                lock (Lock)
                {
                    if (List != null)
                        return;

                    List = Catalog.LoadSerieChapters(_serie);
                }
            }
        }
        #endregion

        public override void UpdateMiniatureViaCrawler()
        {
            if (MiniatureState == MiniatureStatus.Loading) return;
            MiniatureState = MiniatureStatus.Loading;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var uri = Crawler.GetSerieMiniatureUrl(this);
                    SetMiniature(uri, 96, 64);
                }
                catch (Exception)
                {
                    //SetMiniature(new Bitmap(32, 32));
                    MiniatureState = MiniatureStatus.Error;
                    if (Server.Crawler.DefaultImage != null)
                    {
                        SetMiniature(Server.Crawler.DefaultImage);
                    }
                    throw;
                }
                MiniatureState = MiniatureStatus.Loaded;
            });
        }


        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private object _lock = new object();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SerieState _serieState;

        private readonly CachedList<Chapter> _chapters;
        private DateTime _checkDateTime = DateTime.MinValue;

        public Server Server { get; protected set; }
        public string Title { get; internal set; }
        public int DownloadProgress { get; protected set; }
        protected internal bool ChaptersDownloadedFirstTime { get; protected set; }

        internal Serie(Server server, string url, string title)
            : this(server, url, title, Catalog.NextID(), SerieState.Initial, false)
        {
        }

        internal Serie(Server server, string url, string title, ulong id, SerieState serieState, 
            bool chaptersDownloadedFirstTime)
            : base(id)
        {
            _chapters = new ChaptersCachedList(this);
            URL = HtmlDecode(url);
            Server = server;
            _serieState = serieState;
            ChaptersDownloadedFirstTime = chaptersDownloadedFirstTime;

            if (_serieState == SerieState.Downloading)
                _serieState = SerieState.Initial;
            if (_serieState == SerieState.Waiting)
                _serieState = SerieState.Initial;

            title = title.Trim();
            title = title.Replace("\t", " ");
            while (title.IndexOf("  ") != -1)
                title = title.Replace("  ", " ");

            Title = HtmlDecode(title);
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public IList<Chapter> Chapters => _chapters;

        internal override Crawler Crawler => Server.Crawler;

        internal void ResetCheckDate()
        {
            _checkDateTime = DateTime.MinValue;
        }

        internal void DownloadChapters()
        {
            var locker = new object();

            try
            {
                Merge<Chapter> merge = (catc, newc) =>
                {
                    catc.URL = newc.URL;
                };

                Crawler.DownloadChapters(this, (progress, result) =>
                {
                    lock (locker)
                    {
                        if (progress == 100)
                        {
                            if (!ChaptersDownloadedFirstTime)
                                result.ForEach(ch => ch.Visited = true);

                            EliminateDoubles(result.ToList());
                            _chapters.ReplaceInnerCollection(result, ChaptersDownloadedFirstTime, c => c.Title, merge);
                        }

                        DownloadProgress = progress;
                    }
                });

                State = SerieState.Downloaded;
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex1)
            {
                State = SerieState.Error;

                Loggers.MangaCrawler.Error($"Exception #1, serie: {this} state: {State}", ex1);

                try
                {
                    DownloadManager.Instance.DownloadSeries(Server, true);
                }
                catch (Exception ex2)
                {
                    Loggers.MangaCrawler.Error($"Exception #2, serie: {this} state: {State}", ex2);
                }
            }

            ChaptersDownloadedFirstTime = true;
            Catalog.Save(this);
            _checkDateTime = DateTime.Now;
        }

        public override string ToString() => $"{Server.Name} - {Title}";

        private static List<Chapter> EliminateDoubles(List<Chapter> chapters)
        {
            var sameNameSameURL = (from serie in chapters
                                      group serie by new { serie.Title, serie.URL } into gr
                                      from s in gr.Skip(1)
                                      select s).ToList();

            if (sameNameSameURL.Any())
                chapters = chapters.Except(sameNameSameURL).ToList();

            var sameNameDiffURL = from serie in chapters
                                     group serie by serie.Title into gr
                                     where gr.Count() > 1
                                     select gr;

            foreach (var gr in sameNameDiffURL)
            {
                var index = 1;

                foreach (var chapter in gr)
                {
                    for (;;)
                    {
                        string newTitle = $"{chapter.Title} ({index})";
                        index++;

                        if (chapters.Any(ch => ch.Title == newTitle))
                            continue;

                        chapter.Title = newTitle;
                        break;
                    }
                }
            }

            return chapters;
        }

        public bool IsDownloadRequired(bool force)
        {
            if (State == SerieState.Downloaded)
            {
                return force || DateTime.Now - _checkDateTime > DownloadManager.Instance.MangaSettings.CheckTimePeriod;
            }
            return (State == SerieState.Error) || (State == SerieState.Initial);
        }

        public SerieState State
        {
            get
            {
                return _serieState;
            }
            set
            {
                switch (value)
                {
                    case SerieState.Initial:
                    {
                        break;
                    }
                    case SerieState.Waiting:
                    {
                        Debug.Assert((State == SerieState.Downloaded) ||
                                     (State == SerieState.Initial) ||
                                     (State == SerieState.Error));
                        DownloadProgress = 0;
                        break;
                    }
                    case SerieState.Downloading:
                    {
                        Debug.Assert((State == SerieState.Waiting) ||
                                     (State == SerieState.Downloading));
                        break;
                    }
                    case SerieState.Downloaded:
                    {
                        Debug.Assert(State == SerieState.Downloading);
                        Debug.Assert(DownloadProgress == 100);
                        break;
                    }
                    case SerieState.Error:
                    {
                        Debug.Assert(State == SerieState.Downloading);
                        break;
                    }
                    default:
                    {
                        throw new InvalidOperationException($"Unknown state: {value}");
                    }
                }

                _serieState = value;
            }
        }

        public override string GetDirectory()
        {
            var mangaRootDir = DownloadManager.Instance.MangaSettings.GetMangaRootDir(true); ;

            return mangaRootDir +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Server.Name) +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Title) +
                   Path.DirectorySeparatorChar;
        }

        public override bool IsDownloading => (State == SerieState.Downloading) ||
                                              (State == SerieState.Waiting);

        public bool IsBookmarked => DownloadManager.Instance.Bookmarks.List.Contains(this);

        public IEnumerable<Chapter> GetNewChapters() => (from chapter in Chapters
                                                         where !chapter.Visited
                                                         select chapter).ToList();
    }
}
