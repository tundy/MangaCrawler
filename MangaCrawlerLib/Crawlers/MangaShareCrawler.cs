using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using TomanuExtensions;

namespace MangaCrawlerLib.Crawlers
{
    internal class MangaShareCrawler : Crawler
    {
        public override string Name
        {
            get
            {
                return "Manga Share";
            }
        }

        internal override void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback)
        {
            var doc = DownloadDocument(server);

            var series = doc.DocumentNode.SelectNodes("//table[@class='datalist']/tr[@class='datarow']");

            var result = from serie in series 
                         select new Serie(server, 
                                              "http://read.mangashare.com/" + 
                                                  serie.SelectSingleNode("td[@class='datarow-0']/a").
                                                  GetAttributeValue("href", "").Split(new char[] { '/' }).Last(), 
                                              serie.SelectSingleNode("td[@class='datarow-1']/text()").InnerText);

            progressCallback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> progressCallback)
        {
            string url = $"{a_serie.URL}/chapter-001/page001.html";
            var doc = DownloadDocument(a_serie);

            var chapters = doc.DocumentNode.SelectNodes("//table[@class='datalist']/tr/td[4]/a");

            var result = (from chapter in chapters
                          select new Chapter(a_serie, chapter.GetAttributeValue("href", ""),
                              chapter.ParentNode.ParentNode.ChildNodes[3].InnerText)).ToList();

            progressCallback(100, result);

            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
        }

        internal override IEnumerable<Page> DownloadPages(Chapter chapter)
        {
            var doc = DownloadDocument(chapter);

            var pages = doc.DocumentNode.SelectNodes("//select[@name='pagejump']/option");

            var result = new List<Page>();

            var index = 0;
            foreach (var page in pages)
            {
                index++;

                var link = chapter.URL;
                var page_index = link.LastIndexOf("/page");
                link = link.Left(page_index + 5);
                link += page.GetAttributeValue("Value", "") + ".html";

                var pi = new Page(chapter, link, index, "");

                result.Add(pi);
            }

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        internal override string GetImageURL(Page page)
        {
            var doc = DownloadDocument(page);

            var node = doc.DocumentNode.SelectSingleNode("//div[@id='page']/a/img");

            if (node != null)
                return node.GetAttributeValue("src", "");

            return doc.DocumentNode.SelectSingleNode("//div[@id='page']/img").GetAttributeValue("src", "");
        }

        public override string GetServerURL()
        {
            return "http://read.mangashare.com/dir";
        }
    }
}
