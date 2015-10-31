using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MangaCrawlerLib.Crawlers;

namespace MangaCrawlerLib
{
    public class DownloadManager
    {
        public string SettingsDir { get; private set; }
        public MangaSettings MangaSettings { get; private set; }
        public Bookmarks Bookmarks { get; }
        public Downloading Downloadings { get; }
        
        private List<Entity> m_downloading = new List<Entity>();
        private Server[] _servers;

        public static DownloadManager Instance { get; private set; }

        public static void Create(MangaSettings mangaSettings, string settingsDir)
        {
            Instance = new DownloadManager(mangaSettings, settingsDir);
            Instance.Initialize();
        }

        private void Initialize()
        {
            _servers = Catalog.LoadCatalog();

            Bookmarks.Load();
            Downloadings.Load();
        }

        private DownloadManager(MangaSettings mangaSettings, string settingsDir)
        {
            SettingsDir = settingsDir;
            MangaSettings = mangaSettings;
            Bookmarks = new Bookmarks();
            Downloadings = new Downloading();

            HtmlWeb.UserAgent_Actual = mangaSettings.UserAgent;
        }

        public bool NeedGUIRefresh(bool resetState)
        {
            lock (m_downloading)
            {
                var result = m_downloading.Any();

                if (resetState)
                {
                    m_downloading = (from entity in m_downloading
                                     where entity.IsDownloading
                                     select entity).ToList();
                }

                return result;
            }
        }

        public void DownloadSeries(Server server, bool force)
        {
            if (server == null)
                return;

            if (!server.IsDownloadRequired(force))
                return;

            if (server.MiniatureState != Entity.MiniatureStatus.Loading)
            {
                //server.Miniature = null;
                server.MiniatureState = Entity.MiniatureStatus.None;
            }

            lock (m_downloading)
            {
                m_downloading.Add(server);
            }

            server.State = ServerState.Waiting;
            server.LimiterOrder = Catalog.NextID();

            new Task(server.DownloadSeries, TaskCreationOptions.LongRunning).Start();
        }

        public void DownloadChapters(Serie serie, bool force)
        {
            if (serie == null)
                return;

            if (!serie.IsDownloadRequired(force))
                return;

            if (serie.MiniatureState != Entity.MiniatureStatus.Loading)
            {
                //serie.Miniature = null;
                serie.MiniatureState = Entity.MiniatureStatus.None;
            }

            lock (m_downloading)
            {
                m_downloading.Add(serie);
            }
            serie.State = SerieState.Waiting;
            serie.LimiterOrder = Catalog.NextID();

            new Task(serie.DownloadChapters, TaskCreationOptions.LongRunning).Start();
        }

        public void DownloadPages(IEnumerable<Chapter> chapters)
        {
            chapters = chapters.Where(ch => !ch.IsDownloading).ToList();

            if (!chapters.Any())
                return;

            foreach (var chapter in chapters)
            {
                chapter.State = ChapterState.Waiting;
                lock (m_downloading)
                {
                    m_downloading.Add(chapter);
                }
                Downloadings.Add(chapter);
                chapter.LimiterOrder = Catalog.NextID();
                Catalog.SaveChapterPages(chapter);
            }

            new Task(() =>
            {
                foreach (var chapter in chapters)
                {
                    var chapterSync = chapter;

                    new Task(() =>
                    {
                        chapterSync.DownloadPagesAndImages();
                    }, TaskCreationOptions.LongRunning).Start();
                }
            }).Start();
        }

        /// <summary>
        /// Thread safe - immutable.
        /// </summary>
        public IEnumerable<Server> Servers => _servers;

        public void Debug_ResetCheckDate()
        {
            foreach (var server in Servers)
                server.ResetCheckDate();
        }

        public void Debug_InsertSerie(int index, Server server)
        {
            (server.Crawler as TestServerCrawler).Debug_InsertSerie(index);
        }

        public void Debug_RemoveSerie(Server server, Serie selectedSerie)
        {
            (server.Crawler as TestServerCrawler).Debug_RemoveSerie(selectedSerie);
        }

        public void Debug_InsertChapter(int index, Serie serie)
        {
            (serie.Crawler as TestServerCrawler).Debug_InsertChapter(serie, index);
        }

        public void Debug_RemoveChapter(Chapter chapter)
        {
            (chapter.Crawler as TestServerCrawler).Debug_RemoveChapter(chapter);
        }

        public void Debug_RenameSerie(Serie serie)
        {
            (serie.Crawler as TestServerCrawler).Debug_RenameSerie(serie);
        }

        public void Debug_RenameChapter(Chapter chapter)
        {
            (chapter.Crawler as TestServerCrawler).Debug_RenameChapter(chapter);
        }

        public void Debug_ChangeSerieURL(Serie serie)
        {
            (serie.Crawler as TestServerCrawler).Debug_ChangeSerieURL(serie);
        }

        public void Debug_ChangeChapterURL(Chapter chapter)
        {
            (chapter.Crawler as TestServerCrawler).Debug_ChangeChapterURL(chapter);
        }

        public void BookmarksVisited(IEnumerable<Chapter> chapters)
        {
            var chaptersGroupedBySerie = from ch in chapters
                     group ch by ch.Serie;

            foreach (var chaptersGroup in chaptersGroupedBySerie)
            {
                foreach (var chapter in chaptersGroup)
                    chapter.Visited = true;

                Catalog.Save(chaptersGroup.First().Serie);
            }
        }

        public void Debug_DuplicateSerieName(Serie serie)
        {
            (serie.Crawler as TestServerCrawler).Debug_DuplicateSerieName(serie);
        }

        public void Debug_DuplicateChapterName(Chapter chapter)
        {
            (chapter.Crawler as TestServerCrawler).Debug_DuplicateChapterName(chapter);
        }

        public void Debug_DuplicateSerieURL(Serie serie)
        {
            (serie.Crawler as TestServerCrawler).Debug_DuplicateSerieURL(serie);
        }

        public void Debug_DuplicateChapterURL(Chapter chapter)
        {
            (chapter.Crawler as TestServerCrawler).Debug_DuplicateChapterURL(chapter);
        }

        public void Debug_MakeSerieError(Serie serie)
        {
            (serie.Crawler as TestServerCrawler).Debug_MakeSerieError(serie);
        }

        public void Debug_MakeChapterError(Chapter chapter)
        {
            (chapter.Crawler as TestServerCrawler).Debug_MakeChapterError(chapter);
        }
    }
}
