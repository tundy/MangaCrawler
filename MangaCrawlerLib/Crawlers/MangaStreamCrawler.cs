using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using TomanuExtensions;

namespace MangaCrawlerLib.Crawlers
{
    internal class MangaStreamCrawler : Crawler
    {
        public override string Name
        {
            get
            {
                return "Manga Stream";
            }
        }

        internal override string GetServerMiniatureUrl()
        {
            return "http://mangastream.com/favicon.ico";
        }

        internal override void DownloadSeries(Server a_server, Action<int, IEnumerable<Serie>> a_progress_callback)
        {
            var doc = DownloadDocument(a_server);

            var series = doc.DocumentNode.SelectNodes(
                "//table[@class='table table-striped']/tr/td/strong/a");

            var result = from serie in series
                         select new Serie(a_server,
                                          serie.GetAttributeValue("href", ""),
                                          serie.InnerText);

            a_progress_callback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> a_progress_callback)
        {
            var doc = DownloadDocument(a_serie);

            var chapters = doc.DocumentNode.SelectNodes(
                "//table[@class='table table-striped']/tr/td/a");

            var result = (from chapter in chapters
                          select new Chapter(a_serie,
                                             chapter.GetAttributeValue("href", ""),
                                             chapter.InnerText)).ToList();
            
            a_progress_callback(100, result);

            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
        }

        internal override IEnumerable<Page> DownloadPages(Chapter a_chapter)
        {
            var doc = DownloadDocument(a_chapter);

            var pages = doc.DocumentNode.SelectNodes(
                "//div[@class='controls']/div[2]/ul/li/a");

            var result = new List<Page>();

            var link = pages.First().GetAttributeValue("href", "");
            link = link.Remove(link.LastIndexOf("/") + 1);

            var first_page = Int32.Parse(pages.First().GetAttributeValue("href", "").Split("/").Last());
            var last_page = Int32.Parse(pages.Last().GetAttributeValue("href", "").Split("/").Last());

            for (var i = first_page; i <= last_page; i++)
                result.Add(new Page(a_chapter, link + i.ToString(), i, i.ToString()));

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        internal override string GetImageURL(Page a_page)
        {
            var doc = DownloadDocument(a_page);
            var image = doc.DocumentNode.SelectSingleNode("//img[@id='manga-page']");
            return image.GetAttributeValue("src", "");
        }

        public override string GetServerURL()
        {
            return "http://mangastream.com/manga";
        }
    }
}
