using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace MangaCrawlerLib.Crawlers
{
    class YoMangaCrawler : Crawler
    {
        public override string Name => "Yo Manga";

        internal override string GetServerMiniatureUrl()
        {
            return "http://yomanga.co/favicon-16x16.png";
        }

        protected override string _GetSerieMiniatureUrl(Serie serie)
        {
            var doc = new HtmlDocument();
            var request = (HttpWebRequest) WebRequest.Create(serie.URL);
            request.Method = "GET";

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    if (stream != null && stream.CanRead)
                    {
                        var stremReader = new StreamReader(stream /*, Encoding.UTF8*/);
                        doc.LoadHtml(stremReader.ReadToEnd());
                    }
                }
            }
            var info = doc.DocumentNode.SelectSingleNode("//div[@class='comic info']");
            var img = info.SelectSingleNode("./div[@class='thumbnail']/img");
            return img.GetAttributeValue("src", "");
        }

        internal override void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback)
        {
            var series = DownloadDocument(server).DocumentNode.SelectNodes("//div[@class='group']/div[@class='title']/a");
            var result = from serie in series
                         select new Serie(server, serie.GetAttributeValue("href", ""), serie.InnerText);
            progressCallback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> progressCallback)
        {
            var chapters = DownloadDocument(a_serie).DocumentNode.SelectNodes("//div[@class='element']/div[@class='title']/a");
            var result = (from chapter in chapters
                          select new Chapter(a_serie, chapter.GetAttributeValue("href", ""), chapter.InnerText)).ToList();
            progressCallback(100, result);
            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
        }

        internal override IEnumerable<Page> DownloadPages(Chapter chapter)
        {
            //var doc = DownloadDocument(chapter, chapter.URL + "/page/1");
            //var doc = DownloadDocument(chapter);



            var doc = new HtmlDocument();
            //var h = new HttpDownloader(chapter.URL, null, null);
            //doc.OptionReadEncoding = false;
            var request = (HttpWebRequest)WebRequest.Create(chapter.URL);
            request.Method = "GET";
            
            //request.TransferEncoding = "utf-8";
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    if (stream != null && stream.CanRead)
                    {
                        var stremReader = new StreamReader(stream/*, Encoding.UTF8*/);
                        doc.LoadHtml(stremReader.ReadToEnd());
                    }
                }
            }
            var result = new List<Page>();

            /*var test = document;            
            return result;*/

            /*if (!string.IsNullOrEmpty(document))
            {
                var byteArray = Encoding.UTF8.GetBytes(document);
                var stream = new MemoryStream(byteArray);
                doc.Load(stream, Encoding.UTF8);
            }*/

            var topbar_right = doc.DocumentNode.SelectSingleNode("//div[@class='topbar_right']");
            //var title = (from tag in topbar_right.SelectNodes("./div") where tag.Attributes.Contains("class") && tag.Attributes["class"].Value.Contains("tbtitle") select tag).First();
            var title = topbar_right.SelectSingleNode("./div[contains(@class, 'tbtitle dropdown_parent')]");
            //return null;
            var pagesString = title.SelectSingleNode("./div[@class='text']").InnerHtml;

            var pages = Convert.ToInt32(pagesString.Substring(0, pagesString.IndexOf(' ')));
            for (var i = 0; i < pages; i++)
            {
                result.Add(new Page(chapter, chapter.URL + "/page/" + (i+1), i+1, ""));
            }

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        public override string GetServerURL()
        {
            return "http://yomanga.co/reader/directory/";
        }

        internal override string GetImageURL(Page page)
        {
            var doc = DownloadDocument(page);
            var img = doc.DocumentNode.SelectSingleNode("//a/img[@class='open']");
            return img.GetAttributeValue("src", "");
        }
    }
}