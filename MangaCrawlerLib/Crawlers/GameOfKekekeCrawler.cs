using System;
using System.Collections.Generic;
using System.Linq;

namespace MangaCrawlerLib.Crawlers
{
    class GameOfKekekeCrawler :Crawler
    {
        public override string Name
        {
            get { return "Game of Kekeke"; }
        }

        internal override string GetServerMiniatureUrl()
        {
            return "http://gameofscanlation.moe/favicon-16x16.png";
        }

        internal override void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback)
        {
            SetDefaultImage("http://gameofscanlation.moe/android-icon-192x192.png");

            var result = from serie in DownloadDocument(server).DocumentNode.SelectSingleNode("//ul[@class='lst']").SelectNodes("./li")
                         let a = serie.SelectSingleNode("./a")
                         select new Serie(server, a.GetAttributeValue("href", ""), a.GetAttributeValue("title", ""));

            progressCallback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> progressCallback)
        {
            var result = from chapter in DownloadDocument(a_serie).DocumentNode.SelectSingleNode("//div[@class='comicchapters']").SelectNodes(".//a")
                         let i = chapter.InnerHtml.IndexOf('<')
                         select new Chapter(a_serie, chapter.GetAttributeValue("href", ""), (i < 0) ? chapter.InnerText : chapter.InnerHtml.Substring(0, i));
            
            progressCallback(100, result);
        }

        internal override IEnumerable<Page> DownloadPages(Chapter chapter)
        {
            var index = 1;
            return from page in DownloadDocument(chapter).DocumentNode.SelectSingleNode("//div[@class='prw']").SelectNodes(".//img")
                   select new Page(chapter, chapter.URL, index++, "");
        }

        public override string GetServerURL()
        {
            return "http://kek.gameofscanlation.moe/listing/";
        }

        internal override string GetImageURL(Page page)
        {
            return "http://" + DownloadDocument(page).DocumentNode.SelectSingleNode("//div[@class='prw']").SelectNodes(".//img")[page.Index - 1].GetAttributeValue("src", "").TrimStart('/');
        }
    }
}
