using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace MangaCrawlerLib.Crawlers
{
    internal class SpectrumNexusCrawler : Crawler
    {
        public override string Name
        {
            get 
            {
                return "Spectrum Nexus";
            }
        }

        internal override string GetServerMiniatureUrl()
        {
            return "http://www.thespectrum.net/favicon.ico";
        }

        internal override void DownloadSeries(Server a_server, Action<int, 
            IEnumerable<Serie>> a_progress_callback)
        {
            var doc = DownloadDocument(a_server);

            var series = doc.DocumentNode.SelectNodes("//div[@class='mangaJump']/select").Elements().ToList();

            for (var i = series.Count - 1; i >= 0; i--)
            {
                if (series[i].NodeType != HtmlNodeType.Text)
                    continue;
                var str = series[i].InnerText;
                str = str.Trim();
                str = str.Replace("\n", "");
                if (str == "")
                    series.RemoveAt(i);
            }
            
            var splitter = series.FirstOrDefault(s => s.InnerText.Contains("---"));
            if (splitter != null)
            {
                var splitter_index = series.IndexOf(splitter);
                series.RemoveRange(0, splitter_index + 1);
            }

            var result = new List<Serie>();

            for (var i = 0; i < series.Count; i += 2)
            {
                var si = new Serie(
                    a_server,
                    "http://www.thespectrum.net" + series[i].GetAttributeValue("value", ""), 
                    series[i + 1].InnerText);

                result.Add(si);
            }

            a_progress_callback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> a_progress_callback)
        {
            var doc = DownloadDocument(a_serie);

            var nodes = doc.DocumentNode.SelectNodes("//b");
            var node = nodes.Where(n => n.InnerText.StartsWith("Current Status")).FirstOrDefault();

            if (node == null)
                node = nodes.Where(n => n.InnerText.StartsWith("View Comic Online")).FirstOrDefault();

            if (node == null)
            {
                var note = doc.DocumentNode.SelectSingleNode("//div[@class='mainbgtop']/p/em");
                if (note != null)
                {
                    if (note.InnerText.Contains("has been taken down as per request from the publisher"))
                    {
                        a_progress_callback(100, new Chapter[0]);
                        return;
                    }
                }
            }

            for (;;)
            {
                if (node == null)
                    break;

                if (node.Name == "a")
                    if (node.GetAttributeValue("href", "").Contains("thespectrum.net"))
                        break;

                node = node.NextSibling;

                if (node.InnerText.Contains("Sorry! Series removed as requested"))
                {
                    a_progress_callback(100, new Chapter[0]);
                    return;
                }
            }

            var href = node.GetAttributeValue("href", "");

            doc = DownloadDocument(a_serie, href);

            var chapters = doc.DocumentNode.SelectNodes("//select[@name='ch']/option");

            var result = (from chapter in chapters
                          select new Chapter(
                              a_serie,
                              href + "?ch=" + chapter.GetAttributeValue("value", "").Replace(" ", "+") + "&page=1",
                              chapter.NextSibling.InnerText)).Reverse().ToList();

            a_progress_callback(100, result);

            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
        }

        internal override IEnumerable<Page> DownloadPages(Chapter a_chapter)
        {
            var doc = DownloadDocument(a_chapter);

            var pages = doc.DocumentNode.SelectNodes("//select[@name='page']/option");

            var result = new List<Page>();

            var index = 0;
            foreach (var page in pages)
            {
                index++;

                result.Add(new Page(a_chapter,
                                    a_chapter.URL + "&page=" + page.GetAttributeValue("value", ""),
                                    index, page.NextSibling.InnerText));
            }

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        internal override string GetImageURL(Page a_page)
        {
            var doc = DownloadDocument(a_page);
            var img = doc.DocumentNode.SelectSingleNode("//div[@class='imgContainer']/a/img");
            return img.GetAttributeValue("src", "");
        }

        public override string GetServerURL()
        {
            return "http://www.thespectrum.net/";
        }
    }
}
