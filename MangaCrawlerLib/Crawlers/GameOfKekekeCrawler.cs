using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        internal override void DownloadSeries(Server a_server, Action<int, IEnumerable<Serie>> a_progress_callback)
        {
            var result = from serie in DownloadDocument(a_server).DocumentNode.SelectSingleNode("//ul[@class='lst']").SelectNodes("./li")
                         let a = serie.SelectSingleNode("./a")
                         select new Serie(a_server, a.GetAttributeValue("href", ""), a.GetAttributeValue("title", ""));

            a_progress_callback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> a_progress_callback)
        {
            var result = from chapter in DownloadDocument(a_serie).DocumentNode.SelectSingleNode("//div[@class='comicchapters']").SelectNodes(".//a")
                         let i = chapter.InnerHtml.IndexOf('<')
                         select new Chapter(a_serie, chapter.GetAttributeValue("href", ""), (i < 0) ? chapter.InnerText : chapter.InnerHtml.Substring(0, i));
            
            a_progress_callback(100, result);
        }

        internal override IEnumerable<Page> DownloadPages(Chapter a_chapter)
        {
            var index = 1;
            return from page in DownloadDocument(a_chapter).DocumentNode.SelectSingleNode("//div[@class='prw']").SelectNodes(".//img")
                   select new Page(a_chapter, a_chapter.URL, index++, "");
        }

        public override string GetServerURL()
        {
            return "http://kek.gameofscanlation.moe/listing/";
        }

        internal override string GetImageURL(Page a_page)
        {
            return "http://" + DownloadDocument(a_page).DocumentNode.SelectSingleNode("//div[@class='prw']").SelectNodes(".//img")[a_page.Index - 1].GetAttributeValue("src", "").TrimStart('/');
        }
    }
}
