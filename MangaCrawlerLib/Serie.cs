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
            private Serie m_serie;

            public ChaptersCachedList(Serie a_serie)
            {
                m_serie = a_serie;
            }

            protected override void EnsureLoaded()
            {
                lock (m_lock)
                {
                    if (m_list != null)
                        return;

                    m_list = Catalog.LoadSerieChapters(m_serie);
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
        private object m_lock = new object();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SerieState m_state;

        private CachedList<Chapter> m_chapters;
        private DateTime m_check_date_time = DateTime.MinValue;

        public Server Server { get; protected set; }
        public string Title { get; internal set; }
        public int DownloadProgress { get; protected set; }
        protected internal bool ChaptersDownloadedFirstTime { get; protected set; }

        internal Serie(Server a_server, string a_url, string a_title)
            : this(a_server, a_url, a_title, Catalog.NextID(), SerieState.Initial, false)
        {
        }

        internal Serie(Server a_server, string a_url, string a_title, ulong a_id, SerieState a_state, 
            bool a_chapters_downloaded_first_time)
            : base(a_id)
        {
            m_chapters = new ChaptersCachedList(this);
            URL = HtmlDecode(a_url);
            Server = a_server;
            m_state = a_state;
            ChaptersDownloadedFirstTime = a_chapters_downloaded_first_time;

            if (m_state == SerieState.Downloading)
                m_state = SerieState.Initial;
            if (m_state == SerieState.Waiting)
                m_state = SerieState.Initial;

            a_title = a_title.Trim();
            a_title = a_title.Replace("\t", " ");
            while (a_title.IndexOf("  ") != -1)
                a_title = a_title.Replace("  ", " ");

            Title = HtmlDecode(a_title);
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public IList<Chapter> Chapters => m_chapters;

        internal override Crawler Crawler => Server.Crawler;

        internal void ResetCheckDate()
        {
            m_check_date_time = DateTime.MinValue;
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
                            m_chapters.ReplaceInnerCollection(result, ChaptersDownloadedFirstTime, c => c.Title, merge);
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
            m_check_date_time = DateTime.Now;
        }

        public override string ToString()
        {
            return $"{Server.Name} - {Title}";
        }

        private static List<Chapter> EliminateDoubles(List<Chapter> a_chapters)
        {
            var same_name_same_url = (from serie in a_chapters
                                      group serie by new { serie.Title, serie.URL } into gr
                                      from s in gr.Skip(1)
                                      select s).ToList();

            if (same_name_same_url.Any())
                a_chapters = a_chapters.Except(same_name_same_url).ToList();

            var same_name_diff_url = from serie in a_chapters
                                     group serie by serie.Title into gr
                                     where gr.Count() > 1
                                     select gr;

            foreach (var gr in same_name_diff_url)
            {
                var index = 1;

                foreach (var chapter in gr)
                {
                    for (;;)
                    {
                        string new_title = $"{chapter.Title} ({index})";
                        index++;

                        if (a_chapters.Any(ch => ch.Title == new_title))
                            continue;

                        chapter.Title = new_title;
                        break;
                    }
                }
            }

            return a_chapters;
        }

        public bool IsDownloadRequired(bool a_force)
        {
            if (State == SerieState.Downloaded)
            {
                return a_force || DateTime.Now - m_check_date_time > DownloadManager.Instance.MangaSettings.CheckTimePeriod;
            }
            return (State == SerieState.Error) || (State == SerieState.Initial);
        }

        public SerieState State
        {
            get
            {
                return m_state;
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

                m_state = value;
            }
        }

        public override string GetDirectory()
        {
            var manga_root_dir = DownloadManager.Instance.MangaSettings.GetMangaRootDir(true); ;

            return manga_root_dir +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Server.Name) +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Title) +
                   Path.DirectorySeparatorChar;
        }

        public override bool IsDownloading => (State == SerieState.Downloading) ||
                                              (State == SerieState.Waiting);

        public bool IsBookmarked => DownloadManager.Instance.Bookmarks.List.Contains(this);

        public IEnumerable<Chapter> GetNewChapters()
        {
            return (from chapter in Chapters
                    where !chapter.Visited
                    select chapter).ToList();
        }
    }
}
