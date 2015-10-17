using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net;
using System.IO;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using HtmlAgilityPack;
using TomanuExtensions;
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

        internal override string GetSerieMiniatureUrl(Serie serie)
        {
            var web = new HtmlWeb();
            var doc = web.Load(serie.URL);
            var img = doc.DocumentNode.SelectSingleNode("//div[@class='comic_info']/div[@class='thumbnail']/img");
            return img.GetAttributeValue("src", "");
        }

        internal override void DownloadSeries(Server a_server, Action<int, IEnumerable<Serie>> a_progress_callback)
        {
            var series = DownloadDocument(a_server).DocumentNode.SelectNodes("//div[@class='group']/div[@class='title']/a");
            var result = from serie in series
                         select new Serie(a_server, serie.GetAttributeValue("href", ""), serie.InnerText);
            a_progress_callback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> a_progress_callback)
        {
            var chapters = DownloadDocument(a_serie).DocumentNode.SelectNodes("//div[@class='element']/div[@class='title']/a");
            var result = (from chapter in chapters
                          select new Chapter(a_serie, chapter.GetAttributeValue("href", ""), chapter.InnerText)).ToList();
            a_progress_callback(100, result);
            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
        }

        internal override IEnumerable<Page> DownloadPages(Chapter a_chapter)
        {
            //var doc = DownloadDocument(a_chapter, a_chapter.URL + "/page/1");
            //var doc = DownloadDocument(a_chapter);

            var doc = new HtmlDocument();
            doc.OptionReadEncoding = false;
            var request = (HttpWebRequest)WebRequest.Create(a_chapter.URL);
            request.Method = "GET";
            var document = string.Empty;
            //request.TransferEncoding = "utf-8";
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    if (stream.CanRead)
                    {
                        var stremReader = new StreamReader(stream);
                        if (!stremReader.EndOfStream)
                            document = stremReader.ReadToEnd();
                    }
                    doc.Load(stream, Encoding.UTF8);
                }
            }
            var result = new List<Page>();

            /*var test = document;            
            return result;*/

            var topbar_right = doc.DocumentNode.SelectSingleNode("//div[@class='topbar_right']");
            var title =
                topbar_right.SelectSingleNode(
                    "./div[@class='tbtitle dropdown_parent dropdown_righttbtitle dropdown_parent dropdown_right']");
            var pagesString = title.SelectSingleNode("./div[@class='text']").InnerHtml;

            var pages = Convert.ToInt32(pagesString.Substring(0, pagesString.IndexOf(' ')));
            for (var i = 0; i < pages; i++)
            {
                result.Add(new Page(a_chapter, a_chapter.URL + "/page/" + (i+1), i+1, ""));
            }

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        public override string GetServerURL()
        {
            return "http://yomanga.co/reader/directory/";
        }

        internal override string GetImageURL(Page a_page)
        {
            var doc = DownloadDocument(a_page);
            var img = doc.DocumentNode.SelectSingleNode("//a/img[@class='open']");
            return img.GetAttributeValue("src", "");
        }
    }
}