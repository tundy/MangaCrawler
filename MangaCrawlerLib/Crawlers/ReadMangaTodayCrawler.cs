using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MangaCrawlerLib.Crawlers
{
    class ReadMangaTodayCrawler : Crawler
    {
        public override string Name
        {
            get
            {
                return "Read Manga Today";
            }
        }

        public override string GetServerURL()
        {
            return "http://www.readmanga.today/manga-list";
        }

        protected override string _GetSerieMiniatureUrl(Serie serie)
        {
            var web = new HtmlWeb();
            var doc = web.Load(serie.URL);
            var img = doc.DocumentNode.SelectSingleNode("//div[@class='col-md-3']/img");

            return img.GetAttributeValue("src", "");
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> a_progress_callback)
        {
            var doc = DownloadDocument(a_serie);
            var ul = doc.DocumentNode.SelectSingleNode("//ul[@class='chp_lst']");
            var links = ul.SelectNodes(".//a");
            /*var result = new List<Chapter>();
            foreach(var link in links)
            {
                HtmlNode span = null;
                if(link != null)
                    span = link.ChildNodes.First();
                if(span != null)
                    result.Add(new Chapter(a_serie, link.GetAttributeValue("href", ""), span.InnerText));
            }*/
            var result = from a in links let span = a.SelectSingleNode("./span[@class='val']") select new Chapter(a_serie, a.GetAttributeValue("href", ""), span.InnerText);
            a_progress_callback(100, result);
        }

        internal override IEnumerable<Page> DownloadPages(Chapter a_chapter)
        {
            var doc = DownloadDocument(a_chapter/*, a_chapter.URL + "/" + 1*/);
            var ul = doc.DocumentNode.SelectSingleNode("//ul[@class='list-switcher-2']");
            var select = ul.SelectSingleNode(".//select[contains(@class,'jump-menu')]");

            //var temp = select.InnerHtml.Split(new string[] { "value" }, StringSplitOptions.None);
            //var count = temp.Length - 1;

            var nodes = select.SelectNodes("./option");
            var count = nodes.Count;
            var pages = new List<Page>();
            for (var i = 1; i < count+1; i++)
            //var i = 1;
            //foreach(var node in nodes)
            {
                pages.Add(new Page(a_chapter, a_chapter.URL + "/" + i, i, ""));
            }
            //var i = 1;
            //var pages = from option in @select.SelectNodes("./option") select new Page(a_chapter, option.GetAttributeValue("value", ""), i++, string.Empty);

            return pages;
        }

        internal override string GetServerMiniatureUrl()
        {
            return "http://www.readmanga.today/assets/img/favicon.ico";
        }

        internal override void DownloadSeries(Server a_server, Action<int, IEnumerable<Serie>> a_progress_callback)
        {
            var doc = DownloadDocument(a_server);
            var div = doc.DocumentNode.SelectSingleNode("//div[@class='manga-letters']");
            var links = div.SelectNodes("./a");

            var series = new ConcurrentBag<Tuple<int, int, string, string>>();

            var series_progress = 0;

            Action<int> update = progress =>
            {
                var result = from serie in series
                             orderby serie.Item1, serie.Item2
                             select new Serie(a_server, serie.Item4, serie.Item3);

                a_progress_callback(progress, result.ToArray());
            };

            var last_page = links.Count;

            Parallel.For(0, last_page,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = MaxConnectionsPerServer
                },
                (page, state) =>
                {
                    try
                    {
                        var a = links[page];
                        //foreach (var a in links)
                        {
                            doc = DownloadDocument(a_server, a.GetAttributeValue("href", ""));
                            var mangas = doc.DocumentNode.SelectNodes("//span[@class='manga-item']");
                            var i = 0;
                            foreach(var manga in mangas)
                            {
                                var m = manga.SelectSingleNode("./a");
                                series.Add(new Tuple<int, int, string, string>(page, i++, m.InnerText, m.GetAttributeValue("href", "")));
                            }

                            Interlocked.Increment(ref series_progress);
                            update(series_progress * 100 / last_page);
                        }
                    }
                    catch
                    {
                        state.Break();
                        throw;
                    }
                });


            update(100);
        }

        internal override string GetImageURL(Page a_page)
        {
            var doc = DownloadDocument(a_page);
            var div = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'page_chapter')]");
            return div.SelectSingleNode("./img").GetAttributeValue("src", "");
        }
    }
}
