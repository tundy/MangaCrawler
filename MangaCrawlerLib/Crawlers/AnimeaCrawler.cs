using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using TomanuExtensions;
using System.Threading;
using HtmlAgilityPack;

namespace MangaCrawlerLib.Crawlers
{
    internal class AnimeaCrawler : Crawler
    {
        public override string Name => "Animea";

        internal override string GetServerMiniatureUrl() => "http://www.anime-source.com/icon.ico";

        internal override void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback)
        {
            var doc = DownloadDocument(server);

            string url;
            if (server.URL.StartsWith("http://"))
            {
                url = server.URL.Substring("http://".Length, server.URL.Length - "http://".Length);
                url = "http://" + url.Substring(0, url.IndexOf('/'));
            }
            else
            {
                url = server.URL.Substring(0, server.URL.IndexOf('/'));
            }

            var links = doc.DocumentNode.SelectNodes("//a[@class='tooltip_manga book_closed']");

            var result = from a in links
                         let link = a.GetAttributeValue("href", "")
                         where !string.IsNullOrEmpty(link) && link != "/.html"
                         select new Serie(server, url + link, a.InnerText);

            progressCallback(100, result);

            /*var halfPages = doc.DocumentNode.SelectNodes("//div[@class='maincontent']/div[@class='full-page']/div[@class='half-page']");
            var uls = new List<HtmlNode>();

            foreach (var halfpage in halfPages)
            {
                var ul = halfpage.SelectNodes(".//ul[@class='mangalisttext']");
                uls.AddRange(ul);
            }


            var series = new ConcurrentBag<Tuple<int, int, string, string>>();

            var seriesProgress = 0;
            var lastPage = uls.Count;

            Action<int> update = progress =>
            {
                var result = from serie in series
                             orderby serie.Item1, serie.Item2
                             select new Serie(server, serie.Item4, serie.Item3);

                progressCallback(progress, result.ToArray());
            };


            Parallel.For(0, lastPage,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = MaxConnectionsPerServer
                },
                (page, state) =>
                {
                    try
                    {
                        {

                            var i = 0;
                            var ul = uls[page];
                            //foreach (var ul in uls)
                            {
                                var links = ul.SelectNodes(".//a");
                                foreach (var m in links)
                                {
                                    var link = m.GetAttributeValue("href", "");
                                    if (!string.IsNullOrEmpty(link) && link != "/.html")
                                    {
                                        series.Add(new Tuple<int, int, string, string>(page, i++, m.InnerText, url + link));
                                    }
                                }
                                Interlocked.Increment(ref seriesProgress);
                                update(seriesProgress / lastPage * 100 );
                            }

                        }
                    }
                    catch
                    {
                        state.Break();
                        throw;
                    }
                });


            update(100);
        }*/

            /*
        internal override void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback)
        {
            var doc = DownloadDocument(server);

            var lastPage = int.Parse(
                doc.DocumentNode.SelectNodes("//ul[@class='paging']//li/a").Reverse().
                    Skip(1).First().InnerText);

            var series =
                new ConcurrentBag<Tuple<int, int, string, string>>();

            var seriesProgress = 0;

            Action<int> update = progress =>
            {
                var result = from serie in series
                             orderby serie.Item1, serie.Item2
                             select new Serie(server, serie.Item4, serie.Item3);

                progressCallback(progress, result.ToArray());
            };

            Parallel.For(0, lastPage,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = MaxConnectionsPerServer
                },
                (page, state) =>
                {
                    try
                    {
                        var url = GetServerURL();
                        if (page > 0)
                            url += $"?page={page}";

                        var pageDoc = DownloadDocument(
                            server, url);

                        var pageSeries = pageDoc.DocumentNode.SelectNodes(
                            "//ul[@class='mangalist']/li/div/a");
                       
                        for (var i = 0; i < pageSeries.Count; i++)
                        {
                            var s = new Tuple<int, int, string, string>(
                                page, 
                                i, 
                                pageSeries[i].InnerText, 
                                "http://manga.animea.net" + pageSeries[i].GetAttributeValue("href", ""));

                            series.Add(s);
                        }

                        Interlocked.Increment(ref seriesProgress);
                        update(seriesProgress * 100 / lastPage);
                    }
                    catch
                    {
                        state.Break();
                        throw;
                    }
                });

            update(100);*/
        }
        

        protected override string _GetSerieMiniatureUrl(Serie serie)
        {

            var web = new HtmlWeb();
            var doc = web.Load(serie.URL);
            var img = doc.DocumentNode.SelectSingleNode("//img[@class='cover_mp']");
            return img.GetAttributeValue("src", "");
        }

        internal override void DownloadChapters(Serie serie, Action<int, IEnumerable<Chapter>> progressCallback)
        {
            var doc = DownloadDocument(serie);
            var ul = doc.DocumentNode.SelectSingleNode("//ul[@class='chapterlistfull']");
            var links = ul.SelectNodes(".//a");
            if (links == null)
            {
                throw new Exception("Serie has no chapters");
            }


            string url;
            if (serie.Server.URL.StartsWith("http://"))
            {
                url = serie.Server.URL.Substring("http://".Length, serie.Server.URL.Length - "http://".Length);
                url = "http://" + url.Substring(0, url.IndexOf('/'));
            }
            else
            {
                url = serie.Server.URL.Substring(0, serie.Server.URL.IndexOf('/'));
            }

            var result = from a in links select new Chapter(serie, url + a.GetAttributeValue("href", ""), a.InnerText);
            progressCallback(100, result);

            /*while (true)
            {
                var doc = DownloadDocument(serie);

                var chapters = doc.DocumentNode.SelectNodes("//ul[@class='chapters_list']/li/a");

                if (chapters == null)
                {
                    if (doc.DocumentNode.SelectSingleNode("//ul[@class='chapters_list']/li[@class='notice']").InnerText.Contains("No chapters found"))
                    {
                        progressCallback(100, new Chapter[0]);
                        return;
                    }

                    if (doc.DocumentNode.SelectSingleNode("//ul[@class='chapters_list']/li[@class='notice']").InnerText.Contains("This manga has been licensed and is not available for "))
                    {
                        progressCallback(100, new Chapter[0]);
                        return;
                    }

                    var mature_skip_link = doc.DocumentNode.SelectSingleNode("//li[@class='notice']/strong/a");
                    serie.URL = serie.URL + mature_skip_link.GetAttributeValue("href", "");
                    continue;
                }

                var result = (from chapter in chapters select new Chapter(serie, "http://manga.animea.net" + chapter.GetAttributeValue("href", ""), chapter.InnerText)).ToList();

                progressCallback(100, result);

                if (result.Count == 0)
                    throw new Exception("Serie has no chapters");
                break;
            }*/
        }

        internal override IEnumerable<Page> DownloadPages(Chapter chapter)
        {
            var doc = DownloadDocument(chapter);

            var pages = doc.DocumentNode.SelectSingleNode("//select[@name='page']").SelectNodes("option");

            var result = new List<Page>();

            foreach (var page in pages)
            {
                var url =  chapter.URL.RemoveFromRight(5) + "-page-" +
                    page.GetAttributeValue("value", "") + ".html";

                result.Add(
                    new Page(
                        chapter,
                        url, 
                        pages.IndexOf(page) + 1,
                        page.NextSibling.InnerText));
            }

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        public override string GetServerURL() => "http://manga.animea.net/series_old.php"; //http://manga.animea.net/browse.html

        internal override string GetImageURL(Page page)
        {
            var doc = DownloadDocument(page);
            var image = doc.DocumentNode.SelectSingleNode("//img[@class='mangaimg']");
            return image.GetAttributeValue("src", "");
        }
    }
}
