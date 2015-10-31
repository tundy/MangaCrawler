using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HtmlAgilityPack;

namespace MangaCrawlerLib.Crawlers
{
    class GameOfScanlationCrawler : Crawler
    {
        public override string Name
        {
            get { return "Game of Scanlation"; }
        }

        internal override string GetServerMiniatureUrl()
        {
            return "http://gameofscanlation.moe/favicon-16x16.png";
        }

        internal override void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback)
        {
            SetDefaultImage("http://gameofscanlation.moe/android-icon-192x192.png");

            var li = (from l in DownloadDocument(server).DocumentNode.SelectNodes("//li")
                      where l.InnerText.TrimStart().StartsWith("Series &#038; Releases")
                      select l.SelectNodes("./ul/li")).First();

            var result = from l in li
                         select new Serie(server, server.URL, l.SelectSingleNode(".//a").InnerText.Trim());

            progressCallback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> progressCallback)
        {
            var li = (from l in DownloadDocument(a_serie).DocumentNode.SelectNodes("//li")
                      where l.InnerText.TrimStart().StartsWith("Series &#038; Releases")
                      select l.SelectNodes("./ul/li")).First();

            var chapters = (from l in li
                            where HttpUtility.HtmlDecode(l.SelectSingleNode(".//a").InnerText.Trim()) == a_serie.Title
                            select l.SelectNodes(".//li")).First();

            var result = from ch in chapters
                         where ch.SelectNodes(".//ul") == null
                         let a = ch.SelectSingleNode("./a")
                         select new Chapter(a_serie, a.GetAttributeValue("href", ""), a.InnerText);

            progressCallback(100, result);
        }

        internal override IEnumerable<Page> DownloadPages(Chapter chapter)
        {
            var index = 1;
            var imgs = new List<HtmlNode>();
            foreach (var p in DownloadDocument(chapter).DocumentNode.SelectNodes("//div[@class='entry-content']/p"))
            {
                var x = p.SelectNodes("./img");
                if (x != null)
                {
                    imgs.AddRange(x);
                }
            }

            return from img in imgs
                   select new Page(chapter, chapter.URL, index++, "");
        }

        public override string GetServerURL()
        {
            return "http://gameofscanlation.moe/";
        }

        internal override string GetImageURL(Page page)
        {
            var imgs = new List<HtmlNode>();
            foreach (var p in DownloadDocument(page).DocumentNode.SelectNodes("//div[@class='entry-content']/p"))
            {
                var x = p.SelectNodes("./img");
                if (x != null)
                {
                    imgs.AddRange(x);
                }
            }

            var url = imgs[page.Index - 1].GetAttributeValue("src", "").TrimStart('/');
            return "http://" + url;
            /*Uri uri;
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
            {
                url = page.URL + url;
            }
            return url;*/
        }
    }
}
