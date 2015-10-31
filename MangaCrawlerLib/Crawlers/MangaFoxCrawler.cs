using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace MangaCrawlerLib.Crawlers
{
    internal class MangaFoxCrawler : Crawler
    {
        public override string Name => "Manga Fox";

        internal override string GetServerMiniatureUrl()
        {
            return "http://mangafox.me/favicon.ico";
        }

        protected override string _GetSerieMiniatureUrl(Serie serie)
        {
            var web = new HtmlWeb();
            var doc = web.Load(serie.URL);
            var img = doc.DocumentNode.SelectSingleNode("//div[@id='series_info']/div[@class='cover']/img");
            return img.GetAttributeValue("src", "");
        }

        internal override void DownloadSeries(Server server, Action<int, 
            IEnumerable<Serie>> progressCallback)
        {
            var doc = DownloadDocument(server);

            var series = doc.DocumentNode.SelectNodes(
                "//div[@class='manga_list']/ul/li/a");
            var result = from serie in series
                         select new Serie(server, serie.GetAttributeValue("href", ""), serie.InnerText);


            progressCallback(100, result);
        }

        internal override void DownloadChapters(Serie a_serie, Action<int, 
            IEnumerable<Chapter>> progressCallback)
        {
            var doc = DownloadDocument(a_serie);

            var ch1 = doc.DocumentNode.SelectNodes("//ul[@class='chlist']/li/div/h3/a");
            var ch2 = doc.DocumentNode.SelectNodes("//ul[@class='chlist']/li/div/h4/a");

            var chapters = new List<HtmlNode>();
            if (ch1 != null)
                chapters.AddRange(ch1);
            if (ch2 != null)
                chapters.AddRange(ch2);

            var result = (from chapter in chapters
                          select new Chapter(a_serie, chapter.GetAttributeValue("href", ""),
                              chapter.InnerText)).ToList();

            progressCallback(100, result);

            if (result.Count != 0) return;
            if (!doc.DocumentNode.SelectSingleNode("//div[@id='chapters']/div[@class='clear']").
                InnerText.Contains("No Manga Chapter"))
            {
                throw new Exception("Serie has no chapters");
            }
        }

        internal override IEnumerable<Page> DownloadPages(Chapter chapter)
        {
            var result = new List<Page>();
            //var index = 0;

            var url = chapter.URL.Substring(0, chapter.URL.LastIndexOf('/') + 1);
            //<div class="r m">
            //<a href="8.html" class="btn prev_page"><span></span>previous page</a>
            //<div class="l">
            //    Page
            //    <select onchange="change_page(this)" class="m">
            //        <option value="1">1</option><option value="2">2</option><option value="3">3</option><option value="4">4</option><option value="5">5</option><option value="6">6</option><option value="7">7</option><option value="8">8</option><option value="9" selected="selected">9</option><option value="10">10</option><option value="11">11</option><option value="12">12</option><option value="13">13</option><option value="14">14</option><option value="15">15</option><option value="16">16</option><option value="17">17</option><option value="18">18</option><option value="19">19</option><option value="20">20</option><option value="21">21</option><option value="22">22</option><option value="23">23</option><option value="24">24</option><option value="25">25</option><option value="26">26</option><option value="27">27</option><option value="28">28</option><option value="29">29</option><option value="30">30</option><option value="31">31</option><option value="32">32</option><option value="33">33</option><option value="34">34</option><option value="35">35</option><option value="36">36</option><option value="37">37</option>					<option value="0">Comments</option>
            //    </select>
            //    of 37			</div>
            //<a href="10.html" class="btn next_page"><span></span>next page</a>
            //</div>

            var div = DownloadDocument(chapter/*, url + (++index) + ".html"*/).DocumentNode.SelectSingleNode("//div[@class='r m']/div[@class='l']");
            var start = div.InnerHtml.LastIndexOf('>') + 1;
            var tmp = div.InnerHtml.Substring(start, div.InnerHtml.Length - start).Trim();
            start = tmp.LastIndexOf(' ');
            var count = Convert.ToInt32(tmp.Substring(start, tmp.Length - start));

            while (count > 0)
            {
                result.Add(new Page(chapter, url + count + ".html", count, ""));
                count--;
            }

            /*HtmlNode a;
            do
            {
                var doc = DownloadDocument(chapter, url + (++index) + ".html");
                a = doc.DocumentNode.SelectSingleNode("//a[@class='btn next_page']");
                result.Add(new Page(chapter, url + index + ".html", index, ""));
            } while (url != a.GetAttributeValue("href", ""));*/

            // Original MangaCrawler Code:
            //HtmlDocument doc = DownloadDocument(chapter);

            //List<Page> result = new List<Page>();

            //var top_center_bar = doc.DocumentNode.SelectSingleNode("//div[@id='top_center_bar']");
            //var pages = top_center_bar.SelectNodes("div[@class='r m']/div[@class='l']/select[@class='m']/option");

            //int index = 1;

            //foreach (var page in pages)
            //{
            //    if (page.NextSibling != null)
            //    {
            //        if (page.NextSibling.InnerText == "Comments")
            //            continue;
            //    }

            //    Page pi = new Page(
            //        chapter,
            //        chapter.URL.Replace("1.html", String.Format("{0}.html", page.GetAttributeValue("value", ""))), 
            //        index, 
            //        "");

            //    index++;

            //    result.Add(pi);
            //}

            if (result.Count == 0)
                throw new Exception("Chapter has no pages");

            return result;
        }

        internal override string GetImageURL(Page page)
        {
            var doc = DownloadDocument(page);

            var node = doc.DocumentNode.SelectSingleNode("//img[@id='image']");

            return node.GetAttributeValue("src", "");
        }

        public override string GetServerURL()
        {
            return "http://mangafox.me//manga/";
        }
    }
}
