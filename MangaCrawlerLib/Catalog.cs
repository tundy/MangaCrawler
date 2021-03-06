﻿using System;
using System.Collections.Generic;
using System.Linq;
using MangaCrawlerLib.Crawlers;
using System.Xml.Linq;
using TomanuExtensions;
using System.IO;
using TomanuExtensions.Utils;
using System.Xml;
using Ionic.Zlib;

namespace MangaCrawlerLib
{
    public static class Catalog
    {
        #region Consts
        private class Files
        {
            public static string CATALOG_XML = "catalog.xml.zip";
            public static string DOWNLOADINGS_XML = "downloadings.xml.zip";
            public static string BOOKMARKS_XML = "bookmarks.xml.zip";
        }

        private class Nodes
        {
            public static string CATALOG_NODE = "Catalog";

            public static string GLOBAL_ID_COUNTER_NODE = "IDCounter";

            public static string CATALOG_SERVERS_NODE = "Servers";
            public static string SERVER_NODE = "Server";
            public static string SERVER_ID_NODE = "ID";
            public static string SERVER_NAME_NODE = "Name";
            public static string SERVER_STATE_NODE = "State";
            public static string SERVER_SERIES_DOWNLOADED_FIRST_TIME_NODE = "SeriesDownloadedFirstTime";
            public static string SERVER_URL_NODE = "URL";

            public static string SERVER_SERIES_NODE = "ServerSeries";
            public static string SERIES_NODE = "Series";
            public static string SERIE_ID_NODE = "ID";
            public static string SERIE_NODE = "Serie";
            public static string SERIE_TITLE_NODE = "Title";
            public static string SERIE_STATE_NODE = "State";
            public static string SERIE_CHAPTERS_DOWNLOADED_FIRST_TIME_NODE = "ChaptersDownloadedFirstTime";
            public static string SERIE_URL_NODE = "URL";

            public static string SERIE_CHAPTERS_NODE = "SerieChapters";
            public static string SERIE_SERVER_ID_NODE = "ServerID";
            public static string CHAPTERS_NODE = "Chapters";
            public static string CHAPTER_NODE = "Chapter";
            public static string CHAPTER_ID_NODE = "ID";
            public static string CHAPTER_STATE_NODE = "State";
            public static string CHAPTER_TITLE_NODE = "Title";
            public static string CHAPTER_LIMITER_ORDER_NODE = "LimiterOrder";
            public static string CHAPTER_URL_NODE = "URL";
            public static string CHAPTER_VISITED_NODE = "Visited";

            public static string CHAPTER_PAGES_NODE = "ChapterPages";
            public static string CHAPTER_SERIE_ID_NODE = "SerieID";
            public static string PAGES_NODE = "Pages";
            public static string PAGE_NODE = "Page";
            public static string PAGE_ID_NODE = "ID";
            public static string PAGE_INDEX_NODE = "Index";
            public static string PAGE_NAME_NODE = "Name";
            public static string PAGE_URL_NODE = "URL";
            public static string PAGE_HASH_NODE = "Hash";
            public static string PAGE_STATE_NODE = "State";
            public static string PAGE_IMAGEFILEPATH_NODE = "ImageFilePath";

            public static string DOWNLOADINGS_NODE = "Downloadings";
            public static string DOWNLOADING_CHAPTER_ID_NODE = "ChapterID";

            public static string BOOKMARKS_NODE = "Bookmarks";
            public static string BOOKMARK_SERIE_ID_NODE = "SerieID";
        }

        #if LOCAL_SERVERS
        private static string CATALOG_DIR = "Catalog_Test\\";
        #else
        private static string CATALOG_DIR = "Catalog\\";
        #endif

        #endregion

        private static readonly object Lock = new object();
        private static ulong _idCounter;

        internal static ulong NextID()
        {
            lock (Lock)
            {
                return ++_idCounter;
            }
        }

        private static string CatalogFile => CatalogDir + Files.CATALOG_XML;

        private static string DownloadingsFile => CatalogDir + Files.DOWNLOADINGS_XML;

        private static string BookmarksFile => CatalogDir + Files.BOOKMARKS_XML;

        public static string CatalogDir => DownloadManager.Instance.SettingsDir + CATALOG_DIR;

        private static IEnumerable<Server> GetServers()
        {
            return from c in CrawlerList.Crawlers
                   select new Server(c.GetServerURL(), c.Name);
        }

        internal static Server[] LoadCatalog()
        {
            var doc = LoadXml(CatalogFile);

            if (doc == null)
            {
                ClearCatalog();
                _idCounter = 0;
                return GetServers().ToArray();
            }

            _idCounter = 0;
            var servers = GetServers().ToList();

            try
            {
                var root = doc.Element(Nodes.CATALOG_NODE);

                _idCounter = ulong.Parse(root.Element(Nodes.GLOBAL_ID_COUNTER_NODE).Value);

                var catalogServers = (from server in root.Element(Nodes.CATALOG_SERVERS_NODE).Elements(Nodes.SERVER_NODE)
                                       select new Server(
                                           server.Element(Nodes.SERVER_URL_NODE).Value,
                                           server.Element(Nodes.SERVER_NAME_NODE).Value,
                                           ulong.Parse(server.Element(Nodes.SERVER_ID_NODE).Value), 
                                           EnumExtensions.Parse<ServerState>(
                                               server.Element(Nodes.SERVER_STATE_NODE).Value),
                                           bool.Parse(server.Element(
                                               Nodes.SERVER_SERIES_DOWNLOADED_FIRST_TIME_NODE).Value)
                                       )).ToList();

                if (!catalogServers.Select(s => s.ID).Unique())
                    throw new XmlException();

                catalogServers = (from server in catalogServers
                                   where servers.Any(s => s.Name == server.Name)
                                   select server).ToList();

                for (var i=0; i<servers.Count; i++)
                {
                    var cs = catalogServers.FirstOrDefault(s => s.Name == servers[i].Name);

                    if (cs == null)
                        continue;

                    servers[i] = new Server(servers[i].URL, servers[i].Name, servers[i].ID, cs.State, 
                        cs.SeriesDownloadedFirstTime);
                }

                return servers.ToArray();
            }
            catch (Exception ex1)
            {
                Loggers.MangaCrawler.Error("Exception #1", ex1);
                ClearCatalog();
                _idCounter = 0;
                return GetServers().ToArray();
            }
        }

        private static void ClearCatalog()
        {
            try
            {
                new DirectoryInfo(CatalogDir).DeleteContent();
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
            }
        }

        internal static void SaveCatalog()
        {
            lock (Lock)
            {
                try
                {
                    new DirectoryInfo(CatalogDir).Create();

                    var xml =
                        new XElement(Nodes.CATALOG_NODE,
                            new XElement(Nodes.GLOBAL_ID_COUNTER_NODE, _idCounter),
                            new XElement(Nodes.CATALOG_SERVERS_NODE, from s in DownloadManager.Instance.Servers
                                                               select new XElement(Nodes.SERVER_NODE,
                                                           new XElement(Nodes.SERVER_ID_NODE, s.ID),
                                                           new XElement(Nodes.SERVER_NAME_NODE, s.Name),
                                                           new XElement(Nodes.SERVER_STATE_NODE, s.State),
                                                           new XElement(Nodes.SERVER_SERIES_DOWNLOADED_FIRST_TIME_NODE, 
                                                               s.SeriesDownloadedFirstTime),
                                                           new XElement(Nodes.SERVER_URL_NODE, s.URL))));
                    XmlSave(CatalogFile, xml);
                }
                catch (Exception ex)
                {
                    Loggers.MangaCrawler.Error("Exception", ex);
                }
            }
        }

        private static string GetCatalogFile(ulong id)
        {
            return CatalogDir + id + ".xml.zip";
        }

        private static void DeleteCatalogFile(ulong id)
        {
            DeleteFile(GetCatalogFile(id));
        }

        private static void DeleteFile(string path)
        {
            try
            {
                new FileInfo(path).Delete();
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
            }
        }

        private static XDocument LoadCatalogXml(ulong id)
        {
            if (!new FileInfo(GetCatalogFile(id)).Exists)
                return null;

            try
            {
                return XmlLoad(GetCatalogFile(id));
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Fatal("Exception", ex);

                DeleteCatalogFile(id);
                return null;
            }
        }

        private static XDocument LoadXml(string path)
        {
            if (!new FileInfo(path).Exists)
                return null;

            try
            {
                return XmlLoad(path);
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Fatal("Exception", ex);

                DeleteFile(path);
                return null;
            }
        }

        internal static List<Serie> LoadServerSeries(Server server)
        {
            var xml = LoadCatalogXml(server.ID);

            if (xml == null)
                return new List<Serie>();

            try
            {
                var root = xml.Element(Nodes.SERVER_SERIES_NODE);

                var series = from serie in root.Element(Nodes.SERIES_NODE).Elements(Nodes.SERIE_NODE)
                             select new
                             {
                                 ID = ulong.Parse(serie.Element(Nodes.SERIE_ID_NODE).Value),
                                 Title = serie.Element(Nodes.SERIE_TITLE_NODE).Value,
                                 URL = serie.Element(Nodes.SERIE_URL_NODE).Value,
                                 State = EnumExtensions.Parse<SerieState>(
                                    serie.Element(Nodes.SERIE_STATE_NODE).Value),
                                 ChaptersDownloadedFirstTime = bool.Parse(
                                    serie.Element(Nodes.SERIE_CHAPTERS_DOWNLOADED_FIRST_TIME_NODE).Value)
                             };

                return (from serie in series
                        select new Serie(server, serie.URL, serie.Title, serie.ID, serie.State, 
                            serie.ChaptersDownloadedFirstTime)).ToList();
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);

                DeleteCatalogFile(server.ID);
                return new List<Serie>();
            }
        }

        private static void SaveServerSeries(Server server)
        {
            try
            {
                lock (Lock)
                {
                    new DirectoryInfo(CatalogDir).Create();

                    var xml = new XElement(Nodes.SERVER_SERIES_NODE,
                        new XElement(Nodes.SERIES_NODE,
                            from s in server.Series
                            select new XElement(Nodes.SERIE_NODE,
                                new XElement(Nodes.SERIE_ID_NODE, s.ID),
                                new XElement(Nodes.SERIE_TITLE_NODE, s.Title),
                                new XElement(Nodes.SERIE_STATE_NODE, s.State),
                                new XElement(Nodes.SERIE_CHAPTERS_DOWNLOADED_FIRST_TIME_NODE, 
                                    s.ChaptersDownloadedFirstTime),
                                new XElement(Nodes.SERIE_URL_NODE, s.URL))));

                    XmlSave(GetCatalogFile(server.ID), xml);
                }
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
            }
        }

        internal static List<Chapter> LoadSerieChapters(Serie serie)
        {
            var xml = LoadCatalogXml(serie.ID);

            if (xml == null)
                return new List<Chapter>();

            try
            {
                var root = xml.Element(Nodes.SERIE_CHAPTERS_NODE);

                var chapters = from chapter in root.Element(Nodes.CHAPTERS_NODE).Elements(Nodes.CHAPTER_NODE)
                               select new
                               {
                                   ID = ulong.Parse(chapter.Element(Nodes.CHAPTER_ID_NODE).Value),
                                   Title = chapter.Element(Nodes.CHAPTER_TITLE_NODE).Value,
                                   LimiterOrder = ulong.Parse(chapter.Element(Nodes.CHAPTER_LIMITER_ORDER_NODE).Value),
                                   URL = chapter.Element(Nodes.CHAPTER_URL_NODE).Value,
                                   Visited = bool.Parse(chapter.Element(Nodes.CHAPTER_VISITED_NODE).Value),
                                   State = EnumExtensions.Parse<ChapterState>(
                                       chapter.Element(Nodes.CHAPTER_STATE_NODE).Value)
                               };

                return (from chapter in chapters
                        select new Chapter(serie, chapter.URL, chapter.Title,
                            chapter.ID, chapter.State, chapter.LimiterOrder, chapter.Visited)).ToList();
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);

                DeleteCatalogFile(serie.ID);
                return new List<Chapter>();
            }
        }

        private static void SaveSerieChapters(Serie serie)
        {
            try
            {
                lock (Lock)
                {
                    new DirectoryInfo(CatalogDir).Create();

                    var xml = new XElement(Nodes.SERIE_CHAPTERS_NODE,
                        new XElement(Nodes.SERIE_SERVER_ID_NODE, serie.Server.ID),
                        new XElement(Nodes.CHAPTERS_NODE, 
                            from c in serie.Chapters
                            select new XElement(Nodes.CHAPTER_NODE,
                                new XElement(Nodes.CHAPTER_ID_NODE, c.ID),
                                new XElement(Nodes.CHAPTER_TITLE_NODE, c.Title),
                                new XElement(Nodes.CHAPTER_LIMITER_ORDER_NODE, c.LimiterOrder),
                                new XElement(Nodes.CHAPTER_STATE_NODE, c.State),
                                new XElement(Nodes.CHAPTER_VISITED_NODE, c.Visited),
                                new XElement(Nodes.CHAPTER_URL_NODE, c.URL))));

                    XmlSave(GetCatalogFile(serie.ID), xml);
                }
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
            }
        }

        internal static List<Page> LoadChapterPages(Chapter chapter)
        {
            var xml = LoadCatalogXml(chapter.ID);

            if (xml == null)
                return new List<Page>();

            try
            {
                var root = xml.Element(Nodes.CHAPTER_PAGES_NODE);

                var pages = from page in root.Element(Nodes.PAGES_NODE).Elements(Nodes.PAGE_NODE)
                            select new
                            {
                                ID = ulong.Parse(page.Element(Nodes.PAGE_ID_NODE).Value),
                                Name = page.Element(Nodes.PAGE_NAME_NODE).Value,
                                Index = page.Element(Nodes.PAGE_INDEX_NODE).Value.ToInt(),
                                URL = page.Element(Nodes.PAGE_URL_NODE).Value,
                                Hash = ConvertHexStringToBytes(page.Element(Nodes.PAGE_HASH_NODE).Value),
                                ImageFilePath = page.Element(Nodes.PAGE_IMAGEFILEPATH_NODE).Value,
                                State = EnumExtensions.Parse<PageState>(
                                    page.Element(Nodes.PAGE_STATE_NODE).Value)
                            };

                return (from page in pages
                        select new Page(chapter, page.URL, page.Index, page.ID, page.Name, 
                            page.Hash, page.ImageFilePath, page.State)).ToList();
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);

                DeleteCatalogFile(chapter.ID);
                return new List<Page>();
            }
        }

        private static byte[] ConvertHexStringToBytes(string hash) => hash.Length == 0 ? null : Converters.ConvertHexStringToBytes(hash);

        public static void SaveChapterPages(Chapter chapter)
        {
            try
            {
                lock (Lock)
                {
                    new DirectoryInfo(CatalogDir).Create();

                    var xml = new XElement(Nodes.CHAPTER_PAGES_NODE,
                        new XElement(Nodes.CHAPTER_SERIE_ID_NODE, chapter.Serie.ID),
                        new XElement(Nodes.PAGES_NODE,
                            from p in chapter.Pages
                            select new XElement(Nodes.PAGE_NODE,
                                new XElement(Nodes.PAGE_ID_NODE, p.ID),
                                new XElement(Nodes.PAGE_NAME_NODE, p.Name),
                                new XElement(Nodes.PAGE_INDEX_NODE, p.Index),
                                new XElement(Nodes.PAGE_HASH_NODE, 
                                    (p.Hash != null) ? Converters.ConvertBytesToHexString(p.Hash, true) : ""),
                                new XElement(Nodes.PAGE_IMAGEFILEPATH_NODE, p.ImageFilePath),
                                new XElement(Nodes.PAGE_STATE_NODE, p.State),
                                new XElement(Nodes.PAGE_URL_NODE, p.URL))));

                    XmlSave(GetCatalogFile(chapter.ID), xml);
                }
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
            }
        }

        internal static void Save(Serie serie)
        {
            SaveCatalog();
            SaveSerieChapters(serie);
            SaveServerSeries(serie.Server);

        }

        internal static void Save(Server server)
        {
            SaveCatalog();
            SaveServerSeries(server);

        }

        internal static void Save(Chapter chapter)
        {
            SaveCatalog();
            SaveSerieChapters(chapter.Serie);
            SaveChapterPages(chapter);
        }

        internal static void Save(Page page)
        {
            SaveCatalog();
            SaveChapterPages(page.Chapter);

        }

        internal static List<Chapter> LoadDownloadings()
        {
            if (!new FileInfo(DownloadingsFile).Exists)
                return new List<Chapter>();

            try
            {
                var root = XmlLoad(DownloadingsFile).Element(Nodes.DOWNLOADINGS_NODE);

                return root.Elements(Nodes.DOWNLOADING_CHAPTER_ID_NODE).Select(chapter => ulong.Parse(chapter.Value)).Select(LoadChapter).Where(ch => ch != null).ToList();
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
                DeleteFile(DownloadingsFile);
                return new List<Chapter>();
            }
        }

        internal static void SaveDownloading(IEnumerable<Chapter> chapters)
        {
            try
            {
                lock (Lock)
                {
                    new DirectoryInfo(CatalogDir).Create();

                    var xml = new XElement(Nodes.DOWNLOADINGS_NODE,
                        from chapter in chapters
                        select new XElement(Nodes.DOWNLOADING_CHAPTER_ID_NODE, chapter.ID));
                       
                    XmlSave(DownloadingsFile, xml);
                }
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
            }
        }

        private static Serie LoadSerie(ulong serieID)
        {
            var doc = LoadCatalogXml(serieID);

            if (doc == null)
                return null;

            ulong serverID;

            try
            {
                serverID = ulong.Parse(doc.Element(Nodes.SERIE_CHAPTERS_NODE).Element(
                    Nodes.SERIE_SERVER_ID_NODE).Value);

            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
                DeleteCatalogFile(serieID);
                return null;
            }

            var server = LoadServer(serverID);

            return server?.Series.FirstOrDefault(s => s.ID == serieID);
        }

        private static Chapter LoadChapter(ulong chapterID)
        {
            var doc = LoadCatalogXml(chapterID);

            if (doc == null)
                return null;

            ulong serieID;

            try
            {
                serieID = ulong.Parse(doc.Element(Nodes.CHAPTER_PAGES_NODE).Element(
                    Nodes.CHAPTER_SERIE_ID_NODE).Value);

            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
                DeleteCatalogFile(chapterID);
                return null;
            }

            var serie = LoadSerie(serieID);

            return serie?.Chapters.FirstOrDefault(c => c.ID == chapterID);
        }

        private static Server LoadServer(ulong serverID) => DownloadManager.Instance.Servers.FirstOrDefault(s => s.ID == serverID);

        internal static List<Serie> LoadBookmarks()
        {
            if (!new FileInfo(BookmarksFile).Exists)
                return new List<Serie>();

            try
            {
                var root = XmlLoad(BookmarksFile).Element(Nodes.BOOKMARKS_NODE);

                return root.Elements(Nodes.BOOKMARK_SERIE_ID_NODE).Select(bookmark => ulong.Parse(bookmark.Value)).Select(LoadSerie).Where(serie => serie != null).ToList();
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
                DeleteFile(BookmarksFile);
                return new List<Serie>();
            }
        }

        internal static void SaveBookmarks()
        {
            try
            {
                lock (Lock)
                {
                    new DirectoryInfo(CatalogDir).Create();

                    var xml = new XElement(Nodes.BOOKMARKS_NODE,
                        from serie in DownloadManager.Instance.Bookmarks.List
                        select new XElement(Nodes.BOOKMARK_SERIE_ID_NODE, serie.ID));

                    XmlSave(BookmarksFile, xml);
                }
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
            }
        }

        private static XDocument XmlLoad(string file)
        {
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                using (var zs = new GZipStream(fs, CompressionMode.Decompress))
                {
                    return XDocument.Load(zs);
                }
            }
        }

        private static void XmlSave(string file, XElement xml)
        {
            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write))
            {
                using (var zs = new GZipStream(fs, CompressionMode.Compress))
                {
                    xml.Save(zs);
                }
            }
        }
    }
}
