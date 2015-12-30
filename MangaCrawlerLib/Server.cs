using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using MangaCrawlerLib.Crawlers;
using System.IO;
using System.Threading.Tasks;
using TomanuExtensions.Utils;

namespace MangaCrawlerLib
{
    public class Server : Entity
    {
        #region SeriesCachedList
        private class SeriesCachedList : CachedList<Serie>
        {
            private readonly Server _server;

            public SeriesCachedList(Server server)
            {
                _server = server;
            }

            protected override void EnsureLoaded()
            {
                lock (Lock)
                {
                    if (List != null)
                        return;

                    List = Catalog.LoadServerSeries(_server);
                }
            }
        }
        #endregion

        public override void UpdateMiniatureViaCrawler()
        {
            if (MiniatureState == MiniatureStatus.Loading) return;
            Task.Factory.StartNew(() =>
            {
                MiniatureState = MiniatureStatus.Loading;
                try
                {
                    SetMiniature(Crawler.GetServerMiniatureUrl(), 16, 16);
                }
                catch (Exception)
                {
                    //SetMiniature(new Bitmap(16, 16));
                    MiniatureState = MiniatureStatus.Error;
                    throw;
                }
                MiniatureState = MiniatureStatus.Loaded;
            });
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Crawler _crawler;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ServerState _serverState;

        private readonly CachedList<Serie> _series;
        private DateTime _checkDateTime = DateTime.MinValue;

        public int DownloadProgress { get; private set; }
        public string Name { get; }
        protected internal bool SeriesDownloadedFirstTime { get; protected set; }

        internal Server(string url, string name)
            : this(url, name, Catalog.NextID(), ServerState.Initial, false)
        {
        }

        internal Server(string url, string name, ulong id, ServerState serverState, 
            bool seriesDownloadedFirstTime)
            : base(id)
        {
            _series = new SeriesCachedList(this);
            URL = url;
            Name = name;
            _serverState = serverState;
            SeriesDownloadedFirstTime = seriesDownloadedFirstTime;

            if (_serverState == ServerState.Downloading)
                _serverState = ServerState.Initial;
            if (_serverState == ServerState.Waiting)
                _serverState = ServerState.Initial;
            if (_serverState == ServerState.Downloaded)
                _serverState = ServerState.Initial;
        }

        internal override Crawler Crawler => _crawler ?? (_crawler = CrawlerList.Get(this));

        /// <summary>
        /// Thread safe.
        /// </summary>
        public IList<Serie> Series => _series;

        internal void ResetCheckDate()
        {
            _checkDateTime = DateTime.MinValue;

            if (!_series.Filled) return;
            foreach (var serie in Series)
                serie.ResetCheckDate();
        }

        internal void DownloadSeries()
        {
            var locker = new object();

            try
            {
                Crawler.DownloadSeries(this, (progress, result) =>
                {
                    lock (locker)
                    {
                        Merge<Serie> merge = (cats, news) =>
                        {
                            cats.URL = news.URL;
                        };

                        if (progress == 100)
                        {
                            result = EliminateDoubles(result.ToList());
                            _series.ReplaceInnerCollection(result, SeriesDownloadedFirstTime, s => s.Title, merge);
                        }

                        DownloadProgress = progress;
                    }
                });

                DownloadManager.Instance.Bookmarks.RemoveNotExisted();
                State = ServerState.Downloaded;
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error($"Exception, server: {this} state: {State}", ex);
                State = ServerState.Error;
            }

            SeriesDownloadedFirstTime = true;
            Catalog.Save(this);
            _checkDateTime = DateTime.Now;
        }

        private static IEnumerable<Serie> EliminateDoubles(List<Serie> series)
        {
            var sameNameSameURL = (from serie in series
                                      group serie by new { serie.Title, serie.URL } into gr
                                      from s in gr.Skip(1)
                                      select s).ToList();

            if (sameNameSameURL.Any())
                series = series.Except(sameNameSameURL).ToList();

            var sameNameDiffURL = from serie in series
                                     group serie by serie.Title into gr
                                     where gr.Count() > 1
                                     select gr;

            foreach (var gr in sameNameDiffURL)
            {
                var index = 1;

                foreach (var serie in gr)
                {
                    for (; ; )
                    {
                        string newTitle = $"{serie.Title} ({index})";
                        index++;

                        if (series.Any(ch => ch.Title == newTitle))
                        {
                            continue;
                        }

                        serie.Title = newTitle;
                        break;
                    }
                }
            }

            return series;
        }

        public override string ToString() => $"{ID} - {Name}";

        public bool IsDownloadRequired(bool force)
        {
            return State != ServerState.Downloaded
                ? (State == ServerState.Error) || (State == ServerState.Initial)
                : force || DateTime.Now - _checkDateTime > DownloadManager.Instance.MangaSettings.CheckTimePeriod;
        }

        public override string GetDirectory()
        {
            return DownloadManager.Instance.MangaSettings.GetMangaRootDir(true) +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Name) +
                   Path.DirectorySeparatorChar;
        }

        public ServerState State
        {
            get
            {
                return _serverState;
            }
            set
            {
                switch (value)
                {
                    case ServerState.Initial:
                    {
                        break;
                    }
                    case ServerState.Waiting:
                    {
                        Debug.Assert((State == ServerState.Initial) ||
                                     (State == ServerState.Error) || 
                                     (State == ServerState.Downloaded));
                        DownloadProgress = 0;
                        break;
                    }
                    case ServerState.Downloading:
                    {
                        Debug.Assert((State == ServerState.Waiting) ||
                                     (State == ServerState.Downloading));
                        break;
                    }
                    case ServerState.Downloaded:
                    {
                        Debug.Assert(State == ServerState.Downloading);
                        Debug.Assert(DownloadProgress == 100);
                        break;
                    }
                    case ServerState.Error:
                    {
                        Debug.Assert(State == ServerState.Downloading);
                        break;
                    }
                    default:
                    {
                        throw new InvalidOperationException($"Unknown state: {value}");
                    }
                }

                _serverState = value;
            }
        }

        public override bool IsDownloading => (State == ServerState.Downloading) ||
                                              (State == ServerState.Waiting);
    }
}
