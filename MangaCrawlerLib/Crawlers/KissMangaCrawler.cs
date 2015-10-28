using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using HtmlAgilityPack;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using TomanuExtensions;
using System.Threading;
using System.Text.RegularExpressions;

namespace MangaCrawlerLib.Crawlers
{
    internal class KissMangaCrawler : Crawler
    {
        public override string Name => "Kissmanga";

        internal override string GetServerMiniatureUrl()
        {
            return "http://kissmanga.com/Content/images/favicon.ico";
        }

        protected override string _GetSerieMiniatureUrl(Serie serie)
        {
            var web = new HtmlWeb();
            var doc = web.Load(serie.URL);
            var rightside = doc.DocumentNode.SelectSingleNode("//div[@id='rightside']");
            var rightbox = rightside.SelectSingleNode("./div[@class='rightBox']");
            var img = rightbox.SelectSingleNode(".//img");
            return img.GetAttributeValue("src", string.Empty);
        }

        internal override void DownloadSeries(Server a_server, Action<int, IEnumerable<Serie>> a_progress_callback)
        {
            var cookies = new CookieContainer();
            var target = new Uri(a_server.URL);
            cookies.Add(target, new Cookie("vns_doujinshi", "1"));
            var doc = DownloadDocument(a_server, null, cookies);

            var last_page = Int32.Parse(
                doc.DocumentNode.SelectSingleNode("//ul[@class='pager']//li[5]/a").GetAttributeValue("page", ""));

            var series = new ConcurrentBag<Tuple<int, int, string, string>>();

            var series_progress = 0;

            Action<int> update = progress =>
            {
                var result = from serie in series
                    orderby serie.Item1, serie.Item2
                    select new Serie(a_server, serie.Item4, serie.Item3);

                a_progress_callback(progress, result.ToArray());
            };

            Parallel.For(0, last_page + 1,
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
                        {
                            url += String.Format("?page={0}", page);
                        }

                        var page_doc = DownloadDocument(a_server, url);

                        var page_series = page_doc.DocumentNode.SelectNodes("//table[@class='listing']/tr/td[1]/a");

                        if (page_series != null)
                        {
                            for (var i = 0; i < page_series.Count; i++)
                            {
                                var s = new Tuple<int, int, string, string>(
                                    page, 
                                    i, 
                                    page_series[i].InnerText,
                                    "http://kissmanga.com" + page_series[i].GetAttributeValue("href", ""));

                                series.Add(s);
                            }
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

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> a_progress_callback)
        {
            HtmlDocument doc = DownloadDocument(a_serie);

            var chapters = doc.DocumentNode.SelectNodes("//table[@class='listing']/tr/td/a");

            if (chapters == null)
            {
                var banner = doc.DocumentNode.SelectSingleNode("//div[@class='banner']/div/p");
                if (banner != null)
                {
                    if (banner.InnerText.ToLower().Contains("This series has been categorized as 'mature'".ToLower()))
                    {
                        var yes = doc.DocumentNode.SelectSingleNode("//a[@id='aYes']");
                        if (yes != null)
                        {
                            a_serie.URL = yes.GetAttributeValue("href", "");
                            DownloadChapters(a_serie, a_progress_callback);
                            return;
                        }
                    }
                }
            }

            var result = (from chapter in chapters
                          select new Chapter(
                              a_serie,
                              "http://kissmanga.com" + chapter.GetAttributeValue("href", ""), 
                              chapter.InnerText)).ToList();

            a_progress_callback(100, result);

            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
        }

        internal override IEnumerable<Page> DownloadPages(Chapter a_chapter)
        {
            HtmlDocument doc = DownloadDocument(a_chapter);

            var pages = doc.DocumentNode.SelectNodes("//div[@id='divImage']/p/img").Count();

            var result = new List<Page>();

            for (int page = 1; page <= pages; page++)
            {
                result.Add(
                    new Page(
                        a_chapter,
                        a_chapter.URL,
                        page,
                        page.ToString()));
            }

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        public override string GetServerURL()
        {
            return "http://kissmanga.com/MangaList";
        }

        internal override string GetImageURL(Page a_page)
        {
            /* string s = null;
            foreach (HtmlNode js in DownloadDocument(a_page).DocumentNode.SelectNodes("//script[@type='text/javascript']"))
            {
                if (js.InnerText.TrimStart().StartsWith("var lstImages = new Array();"))
                {
                    s = js.InnerText.Split(';')[a_page.Index].Split('"')[1];
             * break
                }
            }
            return s;*/
            return (from js in DownloadDocument(a_page).DocumentNode.SelectNodes("//script[@type='text/javascript']") where js.InnerText.TrimStart().StartsWith("var lstImages = new Array();") select js.InnerText.Split(';')[a_page.Index].Split('"')[1]).FirstOrDefault();

            //Original MangaCrawler Code:
            //var doc = DownloadDocument(a_page);
            //var pages = doc.DocumentNode.SelectNodes("//div[@id='divImage']/p/img");
            //var image = pages.ElementAt(a_page.Index - 1);
            //return image.GetAttributeValue("src", "");
        }

        public override string GetImageURLExtension(string a_image_url)
        {
            var ext = base.GetImageURLExtension(a_image_url);
            var match = Regex.Match(ext, "\\.(?i)(jpg|gif|png|bmp)");
            return match.Value;
        }
    }
}
