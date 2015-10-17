using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net;
using System.IO;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using TomanuExtensions;

namespace MangaCrawlerLib.Crawlers
{
    class YoMangaCrawler : Crawler
    {
        public override string Name
        {
            get
            {
                return "Yo Manga";
            }
        }

        internal override void DownloadSeries(Server a_server, Action<int, IEnumerable<Serie>> a_progress_callback)
        {
            var doc = DownloadDocument(a_server);

            /*<dl class="kg-series-list uk-description-list uk-description-list-line uk-text-center uk-width-1-1 uk-width-medium-1-4 uk-width-large-1-5">
                <a href="series/Prison-School/index.php" class="">
                    <dt>
                        <div class="kg-series-list-image uk-container-center" style="background-image: url(img/ltr/prison-school.jpg);">
                        <span class="kg-status-Ongoing uk-text-small">Ongoing</span></div>
                    </dt>
                    <dd class="">Prison School</dd>
                </a>
            </dl>*/

            var series = doc.DocumentNode.SelectNodes("//a[@class='']");

            var result = from serie in series let name = serie.SelectSingleNode(".//dd[@class='']").InnerText select new Serie(a_server, a_server.URL.Substring(0, a_server.URL.LastIndexOf('/') + 1) + serie.GetAttributeValue("href", ""), name);
            //var result = from serie in series select new Serie(a_server, serie.GetAttributeValue("href", ""), serie.ChildNodes.FindFirst(".//dd[@class='']").InnerText);

            a_progress_callback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> a_progress_callback)
        {
            var doc = DownloadDocument(a_serie);
            /*<dl class="kg-series-chapter uk-description-list uk-description-list-line uk-width-1-1" itemscope="" itemtype="http://schema.org/ItemList">
				<meta itemprop="itemListOrder" content="Descending">
				<dt> | <span itemprop="itemListElement"><a href="chapters/1/1.php" title="Chapter 1">Chapter 1</a></span></dt>
				<dd><span class="uk-text-small">by <span itemprop="provider"><a href="../../index.php" title=":p">Sam</a></span>, 2015.08.07</span></dd>
            </dl>*/

            var chapters = doc.DocumentNode.SelectNodes("//dl[@class='kg-series-chapter uk-description-list uk-description-list-line uk-width-1-1']");

            var result = new List<Chapter>();
            foreach (var chapter in chapters)
            {
                var link = chapter.SelectSingleNode(".//a");
                var url = link.GetAttributeValue("href", "");
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                {
                    url = a_serie.URL.Substring(0, a_serie.URL.LastIndexOf('/') + 1) + url;
                }

                result.Add(new Chapter(a_serie, url, link.InnerText));
            }

            //var result = chapters.Select(chapter => chapter.SelectSingleNode(".//a")).Select(link => new Chapter(a_serie, a_serie.URL.Substring(0, a_serie.URL.LastIndexOf('/') + 1) + link.GetAttributeValue("href", ""), link.InnerText)).ToList();

            a_progress_callback(100, result);

            if (result.Count == 0)
                throw new Exception("Serie has no chapters");
        }

        internal override IEnumerable<Page> DownloadPages(Chapter a_chapter)
        {
            HtmlNode a;
            var result = new List<Page>();
            var url = a_chapter.URL.Substring(0, a_chapter.URL.LastIndexOf('/') + 1);
            var index = 1;

            do
            {
                var doc = DownloadDocument(a_chapter, url + index + ".php");
                a = doc.DocumentNode.SelectSingleNode("//div[@class='kg-reader-inner']/a");
                result.Add(new Page(a_chapter, url + index + ".php", index, ""));
                index++;
            } while (
                !(a.GetAttributeValue("href", "").EndsWith("/1.php") ||
                  a.GetAttributeValue("href", "").EndsWith("/series.php") ||
                  a.GetAttributeValue("href", "").EndsWith("/index.php") ||
                  a.GetAttributeValue("href", "").EndsWith("/") ||
                  !a.GetAttributeValue("href", "").EndsWith(".php")
                )
                );

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        public override string GetServerURL()
        {
            return "http://yomanga.co/series.php";
        }

        internal override string GetImageURL(Page a_page)
        {
            var doc = DownloadDocument(a_page);

            var img = doc.DocumentNode.SelectSingleNode("//div[@class='kg-reader-inner']/a/img");

            return a_page.URL.Substring(0, a_page.URL.LastIndexOf('/') + 1) + img.GetAttributeValue("src", "");
        }
    }
}