﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Drawing;
using TomanuExtensions;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace MangaCrawlerLib.Crawlers
{
    internal class TestServerCrawler : Crawler
    {
        private const int MIN_SERVER_DELAY = 50;

        private string m_name;
        private int m_max_server_delay;
        private bool m_slow_series;
        private bool m_slow_chapters;
        private int m_items_per_page;
        private List<SerieData> m_series = new List<SerieData>();
        private int m_seed;
        private Random m_random = new Random();
        private int m_max_con;

        private class SerieData
        {
            public void SetTitleURL(string a_title)
            {
                Title = a_title;
                URL = "fake serie url - " + a_title;
            }

            public string Title;
            public string URL;
            public int Seed;

            private List<ChapterData> m_chapters;

            public List<ChapterData> Chapters
            {
                get
                {
                    return m_chapters;
                }
                private set
                {
                    m_chapters = value;
                }
            }

            public IEnumerable<ChapterData> GetChapters(int a_count = -1)
            {
                return Chapters ?? (Chapters = GenerateChapters(a_count).ToList());
            }

            private IEnumerable<ChapterData> GenerateChapters(int a_count = -1)
            {
                if (Seed == 0)
                    yield break;

                var random = new Random(Seed);

                var maxc = (int)Math.Pow(random.Next(4, 15), 2);

                if (a_count != -1)
                    maxc = a_count;

                for (var c = 1; c <= maxc; c++)
                {
                    var chapter = new ChapterData();
                    chapter.SetTitleURL(Title + " - Chapter " + c.ToString());
                    chapter.Seed = random.Next();
                    yield return chapter;
                }

                {
                    var chapter = new ChapterData();
                    chapter.SetTitleURL(Title + " - empty pages");
                    yield return chapter;
                }

                {
                    var chapter = new ChapterData();
                    chapter.SetTitleURL(Title + " - error pages none");
                    chapter.Seed = random.Next();
                    yield return chapter;
                }

                {
                    var chapter = new ChapterData();
                    chapter.SetTitleURL(Title + " - error page getimageurl");
                    chapter.Seed = random.Next();
                    yield return chapter;
                }

                {
                    var chapter = new ChapterData();
                    chapter.SetTitleURL(Title + " - error page getimagestream");
                    chapter.Seed = random.Next();
                    yield return chapter;
                }

                {
                    var chapter = new ChapterData();
                    chapter.SetTitleURL(Title + " - out of order pages");
                    chapter.Seed = random.Next();
                    yield return chapter;
                }
            }
        }

        private class ChapterData
        {
            public void SetTitleURL(string a_title)
            {
                Title = a_title;
                URL = "fake chapter url - " + a_title;
            }

            public string Title;
            public string URL;
            public int Seed;

            public IEnumerable<string> GeneratePages()
            {
                if (Seed == 0)
                    yield break;

                var random = new Random(Seed);

                var maxp = (int)Math.Pow(random.Next(4, 12), 2);

                if (Title.Contains("error page getimageurl"))
                    maxp = 100;
                if (Title.Contains(" error page getimagestream"))
                    maxp = 100;

                for (var p = 1; p <= maxp; p++)
                    yield return Title + " - Page " + p.ToString();

                if (Title.Contains("out of order pages"))
                    yield return "!! 12" + Title + " - Page last";

            }
        }

        public TestServerCrawler(string a_name, int a_max_server_delay, 
            bool a_slow_series, bool a_slow_chapters, bool a_empty, int a_max_con)
        {
            m_name = a_name;
            m_seed = a_name.GetHashCode();
            var random = new Random(m_seed);
            m_slow_series = a_slow_series;
            m_slow_chapters = a_slow_chapters;

            if (a_max_server_delay != 0)
                Debug.Assert(a_max_server_delay > MIN_SERVER_DELAY);
            m_max_server_delay = a_max_server_delay;

            m_items_per_page = random.Next(4, 9) * 5;
            m_max_con = a_max_con;

            var maxs = (int)Math.Pow(random.Next(10, 70), 2);

            if (Name.Contains("error series few"))
                maxs = 3456;

            for (var s = 1; s <= maxs; s++)
            {
                var serie = new SerieData();
                serie.SetTitleURL(a_name + " - Serie" + s.ToString());
                serie.Seed = random.Next();
                m_series.Add(serie);
            }

            {
                var serie = new SerieData();
                serie.SetTitleURL(a_name + " - empty chapters");
                m_series.Add(serie);
            }

            {
                var serie = new SerieData();
                serie.SetTitleURL(a_name + " - error chapters none");
                serie.Seed = random.Next();
                m_series.Add(serie);
            }

            {
                var serie = new SerieData();
                serie.SetTitleURL(a_name + " - error chapters few");
                serie.Seed = random.Next();
                m_series.Add(serie);
            }

            {
                var serie = new SerieData();
                serie.SetTitleURL(a_name + " - few chapters");
                serie.Seed = random.Next();
                m_series.Add(serie);
            }

            if (Name.Contains("error series none"))
                m_series.Clear();

            if (a_empty)
                m_series.Clear();
        }

        public override int MaxConnectionsPerServer => m_max_con != 0 ? m_max_con : base.MaxConnectionsPerServer;

        public override string Name => m_name;

        private void Sleep()
        {
            if (m_max_server_delay == 0)
                return;
            Thread.Sleep(NextInt(MIN_SERVER_DELAY, m_max_server_delay));
        }

        internal override void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback)
        {
            Limiter.Aquire(server);
            try
            {
                Sleep();
            }
            finally
            {
                Limiter.Release(server);
            }

            server.State = ServerState.Downloading;

            if (server.Name.Contains("error series none"))
                throw new Exception();

            Debug.Assert(server.Name == m_name);

            var gen_exc = server.Name.Contains("error series few");

            var toreport = (from serie in m_series
                            select new Serie(server, serie.URL, serie.Title)).ToArray();

            var exc = false;

            var total = toreport.Length;

            if (m_slow_series)
            {
                var listlist = new List<List<Serie>>();
                while (toreport.Any())
                {
                    var part = toreport.Take(m_items_per_page).ToList();
                    toreport = toreport.Skip(m_items_per_page).ToArray();
                    listlist.Add(part);
                }

                var series =
                    new ConcurrentBag<Tuple<int, int, Serie>>();

                Parallel.ForEach(listlist,
                    new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = MaxConnectionsPerServer
                    },
                    (list) =>
                    {
                        foreach (var el in list)
                            series.Add(new Tuple<int, int, Serie>(listlist.IndexOf(list), list.IndexOf(el), el));

                        Limiter.Aquire(server);
                        try
                        {
                            Sleep();
                        }
                        finally
                        {
                            Limiter.Release(server);
                        }

                        var result = (from s in series
                                      orderby s.Item1, s.Item2
                                      select s.Item3).ToList();

                        if (exc)
                            if (gen_exc)
                                return;

                        progressCallback(
                            result.Count * 100 / total,
                            result);

                        if (exc) return;
                        if (!gen_exc) return;
                        exc = true;
                        throw new Exception();
                    });
            }
            else
            {
                if (gen_exc)
                    throw new Exception();

                progressCallback(100, toreport);
            }
        }

        private int NextInt(int a_inclusive_min, int a_exlusive_max)
        {
            lock (m_random)
            {
                return m_random.Next(a_inclusive_min, a_exlusive_max);
            }
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> progressCallback)
        {
            Limiter.Aquire(a_serie);
            try
            {
                Sleep();
            }
            finally
            {
                Limiter.Release(a_serie);
            }

            a_serie.State = SerieState.Downloading;

            Debug.Assert(a_serie.Server.Name == m_name);

            var testSerie = m_series.FirstOrDefault(s => s.Title == a_serie.Title);

            if (testSerie == null)
                throw new Exception();

            if (testSerie.Title.Contains("error chapters none"))
                throw new Exception();

            var genExc = testSerie.Title.Contains("error chapters few");

            var count = -1;

            if (genExc)
                count = m_items_per_page * 8 + m_items_per_page / 3;

            if (testSerie.Title.Contains("few chapters"))
                count = 3;

            var toreport = (from chapter in testSerie.GetChapters(count)
                            select new Chapter(a_serie, chapter.URL, chapter.Title)).ToArray();

            var total = toreport.Length;

            if (m_slow_chapters)
            {
                var listlist = new List<List<Chapter>>();
                while (toreport.Any())
                {
                    var part = toreport.Take(m_items_per_page).ToList();
                    toreport = toreport.Skip(m_items_per_page).ToArray();
                    listlist.Add(part);
                }

                var chapters =
                    new ConcurrentBag<Tuple<int, int, Chapter>>();

                var exc = false;

                Parallel.ForEach(listlist,
                    new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = MaxConnectionsPerServer
                    },
                    (list) =>
                    {
                        foreach (var el in list)
                            chapters.Add(new Tuple<int, int, Chapter>(listlist.IndexOf(list), list.IndexOf(el), el));

                        Limiter.Aquire(a_serie);
                        try
                        {
                            Sleep();
                        }
                        finally
                        {
                            Limiter.Release(a_serie);
                        }

                        var result = (from s in chapters
                                      orderby s.Item1, s.Item2
                                      select s.Item3).ToList();

                        if (genExc)
                            if (exc)
                                return;

                        progressCallback(
                            result.Count * 100 / total,
                            result);

                        if (!genExc) return;
                        if (exc) return;
                        exc = true;
                        throw new Exception();
                    });
            }
            else
            {
                progressCallback(100, toreport);

                if (genExc)
                    throw new Exception();
            }
        }

        internal override IEnumerable<Page> DownloadPages(Chapter chapter)
        {
            chapter.Token.ThrowIfCancellationRequested();

            Limiter.Aquire(chapter);
            try
            {
                Sleep();
            }
            finally
            {
                Limiter.Release(chapter);
            }

            chapter.Token.ThrowIfCancellationRequested();

            chapter.State = ChapterState.DownloadingPagesList;

            var testSerie = m_series.First(s => s.Title == chapter.Serie.Title);
            var testChapter = testSerie.GetChapters().First(c => c.Title == chapter.Title);
            var pages = testChapter.GeneratePages().ToList();

            if (chapter.Title.Contains("error pages none"))
                throw new Exception();

            var result = from page in pages
                         select new Page(chapter, "fake_page_url",
                             pages.IndexOf(page) + 1, page);

            return result;
        }

        internal override MemoryStream GetImageStream(Page page)
        {
            if (page.Chapter.Title.Contains("error page getimagestream"))
            {
                if (page.Index == 45)
                    throw new Exception();
            }

            page.Chapter.Token.ThrowIfCancellationRequested();

            using (var bmp = new Bitmap(NextInt(600, 2000), NextInt(600, 2000)))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    var str = "server: " + page.Server.Name + Environment.NewLine +
                                 "serie: " + page.Serie.Title + Environment.NewLine +
                                 "chapter: " + page.Chapter.Title + Environment.NewLine +
                                 "page: " + page.Name;

                    g.DrawString(
                        str,
                        new Font(FontFamily.GenericSansSerif, 25, FontStyle.Bold),
                        Brushes.White,
                        new RectangleF(10, 10, bmp.Width - 20, bmp.Height - 20)
                    );
                }

                Limiter.Aquire(page);
                try
                {
                    Sleep();
                }
                finally
                {
                    Limiter.Release(page);
                }

                var ms = new MemoryStream();
                bmp.SaveJPEG(ms, 75);
                return ms;
            }
        }

        internal override string GetImageURL(Page page)
        {
            page.Chapter.Token.ThrowIfCancellationRequested();

            page.State = PageState.Downloading;

            if (!page.Chapter.Title.Contains("error page getimageurl")) return "fake_image_url.jpg";
            if (page.Index == 55)
                throw new Exception();

            return "fake_image_url.jpg";
        }

        public override string GetServerURL()
        {
            return m_name;
        }

        public void Debug_InsertSerie(int a_index)
        {
            var sd = new SerieData {Seed = m_random.Next()};
            sd.SetTitleURL(">>> added serie " + m_random.Next());

            m_series.Insert(a_index, sd);
        }

        public void Debug_RemoveSerie(Serie a_serie)
        {
            m_series.RemoveAll(sd => sd.Title == a_serie.Title);
        }

        public void Debug_InsertChapter(Serie a_serie, int a_index)
        {
            var serie_data = m_series.First(sd => sd.Title == a_serie.Title);

            var cd = new ChapterData {Seed = m_random.Next()};
            cd.SetTitleURL(">>> added chapter " + m_random.Next());
            serie_data.Chapters.Insert(a_index, cd);
        }

        public void Debug_RemoveChapter(Chapter a_chapter)
        {
            var serie_data = m_series.First(sd => sd.Title == a_chapter.Serie.Title);
            serie_data.Chapters.RemoveAll(cd => cd.Title == a_chapter.Title);
        }

        public void Debug_RenameSerie(Serie a_serie)
        {
            var serie_data = m_series.First(sd => sd.Title == a_serie.Title);
            serie_data.Title += " " + m_random.Next();
        }

        public void Debug_RenameChapter(Chapter a_chapter)
        {
            var serie_data = m_series.First(sd => sd.Title == a_chapter.Serie.Title);
            var chapter_data = serie_data.Chapters.First(c => c.Title == a_chapter.Title);
            chapter_data.Title += " " + m_random.Next();
        }

        public void Debug_ChangeSerieURL(Serie a_serie)
        {
            var serie_data = m_series.First(sd => sd.Title == a_serie.Title);
            serie_data.URL += " " + m_random.Next();
        }

        public void Debug_ChangeChapterURL(Chapter a_chapter)
        {
            var serie_data = m_series.First(sd => sd.Title == a_chapter.Serie.Title);
            var chapter_data = serie_data.Chapters.First(c => c.Title == a_chapter.Title);
            chapter_data.URL += " " + m_random.Next();
        }

        public void Debug_DuplicateSerieName(Serie a_serie)
        {
            var sd = new SerieData
            {
                Seed = m_random.Next(),
                Title = a_serie.Title,
                URL = ">>> duplicated name serie " + a_serie.URL
            };

            var serie = m_series.First(s => s.Title == a_serie.Title);
            m_series.Insert(m_series.IndexOf(serie) + 1, sd);
        }

        public void Debug_DuplicateChapterName(Chapter a_chapter)
        {
            var serie_data = m_series.First(sd => sd.Title == a_chapter.Serie.Title);

            var cd = new ChapterData
            {
                Seed = m_random.Next(),
                Title = a_chapter.Title,
                URL = ">>> duplicated name chapter " + a_chapter.URL
            };

            var chapter = serie_data.Chapters.First(s => s.Title == a_chapter.Title);
            serie_data.Chapters.Insert(serie_data.Chapters.IndexOf(chapter) + 1, cd);
        }

        internal void Debug_DuplicateSerieURL(Serie a_serie)
        {
            var sd = new SerieData
            {
                Seed = m_random.Next(),
                Title = ">>> duplicated name serie " + a_serie.Title,
                URL = a_serie.URL
            };

            var serie = m_series.First(s => s.Title == a_serie.Title);
            m_series.Insert(m_series.IndexOf(serie) + 1, sd);
        }

        internal void Debug_DuplicateChapterURL(Chapter a_chapter)
        {
            var serie_data = m_series.First(sd => sd.Title == a_chapter.Serie.Title);

            var cd = new ChapterData
            {
                Seed = m_random.Next(),
                Title = ">>> duplicated name chapter " + a_chapter.Title,
                URL = a_chapter.URL
            };

            var chapter = serie_data.Chapters.First(s => s.Title == a_chapter.Title);
            serie_data.Chapters.Insert(serie_data.Chapters.IndexOf(chapter) + 1, cd);
        }

        internal void Debug_MakeSerieError(Serie a_serie)
        {
            a_serie.State = SerieState.Error;
        }

        internal void Debug_MakeChapterError(Chapter a_chapter)
        {
            a_chapter.State = ChapterState.Error;
        }
    }
}
