using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using TomanuExtensions;

namespace MangaCrawlerLib.Crawlers
{
    internal class AnimeSourceCrawler : Crawler
    {
        public override string Name
        {
            get
            {
                return "Anime Source";
            }
        }

        internal override void DownloadSeries(Server server, 
            Action<int, IEnumerable<Serie>> progressCallback)
        {
            var doc = DownloadDocument(server);

            var series = doc.DocumentNode.SelectNodes(
                "/html/body/center/table/tr/td/table[5]/tr/td/table/tr/td/table/tr/td/table/tr/td[2]");

            var result = from serie in series
                         where (serie.ChildNodes[7].InnerText.Trim() != "2")
                         orderby serie.SelectSingleNode("font").FirstChild.InnerText
                         select new Serie(server,
                                              "http://www.anime-source.com/banzai/" + 
                                              serie.SelectSingleNode("a[2]").GetAttributeValue("href", ""),
                                              serie.SelectSingleNode("font").FirstChild.InnerText);

            progressCallback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> progressCallback)
        {
            var doc = DownloadDocument(a_serie);

            var chapters = doc.DocumentNode.SelectNodes(
                "/html/body/center/table/tr/td/table[5]/tr/td/table/tr/td/table/tr/td/blockquote/a");

            var result = (from chapter in chapters.Skip(1)
                          select new Chapter(a_serie,
                                                 "http://www.anime-source.com/banzai/" + chapter.GetAttributeValue("href", ""),
                                                 chapter.InnerText)).Reverse().ToList();

            progressCallback(100, result);

            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
        }

        internal override IEnumerable<Page> DownloadPages(Chapter chapter)
        {
            var doc = DownloadDocument(chapter);

            var pages = doc.DocumentNode.SelectNodes("//select[@name='pageid']/option");

            var result = new List<Page>();

            if (pages == null)
            {
                var pages_str = doc.DocumentNode.SelectSingleNode(
                    "/html/body/center/table/tr/td/table[5]/tr/td/table/tr/td/table/tr/td/font[2]").ChildNodes[4].InnerText;

                var pages_count = int.Parse(pages_str.Split(new char[] { '/' }).Last());

                for (var page = 1; page <= pages_count; page++)
                {
                    var pi = new Page(chapter, chapter.URL + "&page=" + page, page, "");

                    result.Add(pi);
                }
            }
            else
            {
                var index = 0;
                foreach (var page in pages)
                {
                    index++;

                    var pi = new Page(chapter, 
                                       "http://www.anime-source.com/banzai/" + page.GetAttributeValue("value", ""),
                                       index, "");

                    result.Add(pi);
                }
            }

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        internal override string GetImageURL(Page page)
        {
            var doc = DownloadDocument(page);

            string xpath;
            if (page.Chapter.Pages.Count == page.Index)
                xpath = "/html/body/center/table/tr/td/table[5]/tr/td/div/img";
            else
                xpath = "/html/body/center/table/tr/td/table[5]/tr/td/div/a/img";

            var node = doc.DocumentNode.SelectSingleNode(xpath);

            if (node == null)
            {
                node = doc.DocumentNode.SelectSingleNode(
                    "/html/body/center/table/tr/td/table[5]/tr/td/table/tr/td/table/tr/td/font[2]/p[2]/img");

                if (node == null)
                    node = doc.DocumentNode.SelectSingleNode(
                        "/html/body/center/table/tr/td/table[5]/tr/td/table/tr/td/table/tr/td/font[2]/p/img");

                return node.GetAttributeValue("src", "");
            }
            else
                return "http://www.anime-source.com" + node.GetAttributeValue("src", "");
        }
        
        public override string GetServerURL()
        {
            return "http://www.anime-source.com/banzai/modules.php?name=Manga";
        }
    }
}
