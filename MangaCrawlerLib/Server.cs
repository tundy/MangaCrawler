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
            private Server m_server;

            public SeriesCachedList(Server a_server)
            {
                m_server = a_server;
            }

            protected override void EnsureLoaded()
            {
                lock (m_lock)
                {
                    if (m_list != null)
                        return;

                    m_list = Catalog.LoadServerSeries(m_server);
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
        private Crawler m_crawler;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ServerState m_state;

        private CachedList<Serie> m_series;
        private DateTime m_check_date_time = DateTime.MinValue;

        public int DownloadProgress { get; private set; }
        public string Name { get; private set; }
        protected internal bool SeriesDownloadedFirstTime { get; protected set; }

        internal Server(string a_url, string a_name)
            : this(a_url, a_name, Catalog.NextID(), ServerState.Initial, false)
        {
        }

        internal Server(string a_url, string a_name, ulong a_id, ServerState a_state, 
            bool a_series_downloaded_first_time)
            : base(a_id)
        {
            m_series = new SeriesCachedList(this);
            URL = a_url;
            Name = a_name;
            m_state = a_state;
            SeriesDownloadedFirstTime = a_series_downloaded_first_time;

            if (m_state == ServerState.Downloading)
                m_state = ServerState.Initial;
            if (m_state == ServerState.Waiting)
                m_state = ServerState.Initial;
            if (m_state == ServerState.Downloaded)
                m_state = ServerState.Initial;
        }

        internal override Crawler Crawler => m_crawler ?? (m_crawler = CrawlerList.Get(this));

        /// <summary>
        /// Thread safe.
        /// </summary>
        public IList<Serie> Series => m_series;

        internal void ResetCheckDate()
        {
            m_check_date_time = DateTime.MinValue;

            if (!m_series.Filled) return;
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
                            m_series.ReplaceInnerCollection(result, SeriesDownloadedFirstTime, s => s.Title, merge);
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
            m_check_date_time = DateTime.Now;
        }

        private static IEnumerable<Serie> EliminateDoubles(List<Serie> a_series)
        {
            var same_name_same_url = (from serie in a_series
                                      group serie by new { serie.Title, serie.URL } into gr
                                      from s in gr.Skip(1)
                                      select s).ToList();

            if (same_name_same_url.Any())
                a_series = a_series.Except(same_name_same_url).ToList();

            var same_name_diff_url = from serie in a_series
                                     group serie by serie.Title into gr
                                     where gr.Count() > 1
                                     select gr;

            foreach (var gr in same_name_diff_url)
            {
                var index = 1;

                foreach (var serie in gr)
                {
                    for (; ; )
                    {
                        string new_title = $"{serie.Title} ({index})";
                        index++;

                        if (a_series.Any(ch => ch.Title == new_title))
                            continue;

                        serie.Title = new_title;
                        break;
                    }
                }
            }

            return a_series;
        }

        public override string ToString()
        {
            return $"{ID} - {Name}";
        }

        public bool IsDownloadRequired(bool a_force)
        {
            if (State == ServerState.Downloaded)
            {
                if (!a_force)
                {
                    return DateTime.Now - m_check_date_time > DownloadManager.Instance.MangaSettings.CheckTimePeriod;
                }
                else
                    return true;
            }
            else
                return (State == ServerState.Error) || (State == ServerState.Initial);
        }

        public override string GetDirectory()
        {
            var manga_root_dir = DownloadManager.Instance.MangaSettings.GetMangaRootDir(true); ;

            return manga_root_dir +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Name) +
                   Path.DirectorySeparatorChar;
        }

        public ServerState State
        {
            get
            {
                return m_state;
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

                m_state = value;
            }
        }

        public override bool IsDownloading => (State == ServerState.Downloading) ||
                                              (State == ServerState.Waiting);
    }
}
