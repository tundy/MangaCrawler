using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using TomanuExtensions;
using System.Threading;

namespace MangaCrawlerLib.Crawlers
{
    internal class AnimeaCrawler : Crawler
    {
        public override string Name
        {
            get 
            {
                return "Animea";
            }
        }

        internal override string GetServerMiniatureUrl()
        {
            return "http://www.anime-source.com/icon.ico";
        }

        internal override void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback)
        {
            var doc = DownloadDocument(server);

            var last_page = int.Parse(
                doc.DocumentNode.SelectNodes("//ul[@class='paging']//li/a").Reverse().
                    Skip(1).First().InnerText);

            var series =
                new ConcurrentBag<Tuple<int, int, string, string>>();

            var series_progress = 0;

            Action<int> update = (progress) =>
            {
                var result = from serie in series
                             orderby serie.Item1, serie.Item2
                             select new Serie(server, serie.Item4, serie.Item3);

                progressCallback(progress, result.ToArray());
            };

            Parallel.For(0, last_page,
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

                        var page_doc = DownloadDocument(
                            server, url);

                        var page_series = page_doc.DocumentNode.SelectNodes(
                            "//ul[@class='mangalist']/li/div/a");
                       
                        for (var i = 0; i < page_series.Count; i++)
                        {
                            var s = new Tuple<int, int, string, string>(
                                page, 
                                i, 
                                page_series[i].InnerText, 
                                "http://manga.animea.net" + page_series[i].GetAttributeValue("href", ""));

                            series.Add(s);
                        }

                        Interlocked.Increment(ref series_progress);
                        update(series_progress * 100 / last_page);
                    }
                    catch
                    {
                        state.Break();
                        throw;
                    }
                });

            update(100);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> progressCallback)
        {
            var doc = DownloadDocument(a_serie);

            var chapters = doc.DocumentNode.SelectNodes("//ul[@class='chapters_list']/li/a");

            if (chapters == null)
            {
                if (doc.DocumentNode.SelectSingleNode("//ul[@class='chapters_list']/li[@class='notice']").
                    InnerText.Contains("No chapters found"))
                {
                    progressCallback(100, new Chapter[0]);
                    return;
                }

                if (doc.DocumentNode.SelectSingleNode("//ul[@class='chapters_list']/li[@class='notice']").
                    InnerText.Contains("This manga has been licensed and is not available for "))
                {
                    progressCallback(100, new Chapter[0]);
                    return;
                }
                
                var mature_skip_link = doc.DocumentNode.SelectSingleNode("//li[@class='notice']/strong/a");
                a_serie.URL = a_serie.URL + mature_skip_link.GetAttributeValue("href", "");
                DownloadChapters(a_serie, progressCallback);
                return;
            }

            var result = (from chapter in chapters
                          select new Chapter(
                              a_serie,
                              "http://manga.animea.net" + chapter.GetAttributeValue("href", ""), 
                              chapter.InnerText)).ToList();

            progressCallback(100, result);

            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
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

        public override string GetServerURL()
        {
            //return "http://manga.animea.net/browse.html";
            return "http://manga.animea.net/series_old.php";
        }

        internal override string GetImageURL(Page page)
        {
            var doc = DownloadDocument(page);
            var image = doc.DocumentNode.SelectSingleNode("//img[@class='mangaimg']");
            return image.GetAttributeValue("src", "");
        }
    }
}
