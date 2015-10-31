using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Net;
using HtmlAgilityPack;

namespace MangaCrawlerLib
{
    internal abstract class Crawler
    {
        public Image DefaultImage { get; protected set; }

        protected void SetDefaultImage(string uri)
        {
            var wc = new WebClient();
            var img = Image.FromStream(wc.OpenRead(uri));
            DefaultImage = Entity.ScaleImage(img, 96, 64);
        }

        public abstract string Name { get; }

        internal virtual string GetServerMiniatureUrl() => string.Empty;

        internal string GetSerieMiniatureUrl(Serie serie)
        {
            Limiter.AquireMiniature(serie);
            try
            {
                return _GetSerieMiniatureUrl(serie);
            }
            finally
            {
                Limiter.Release(serie);
            }
        }

        protected virtual string _GetSerieMiniatureUrl(Serie serie)
        {
            return string.Empty;
        }

        internal abstract void DownloadSeries(Server server, Action<int, IEnumerable<Serie>> progressCallback);
        internal abstract void DownloadChapters(Serie serie, Action<int, IEnumerable<Chapter>> progressCallback);
        internal abstract IEnumerable<Page> DownloadPages(Chapter chapter);
        public abstract string GetServerURL();
        internal abstract string GetImageURL(Page page);

        private static T DownloadWithRetry<T>(Func<T> func)
        {
            WebException ex1 = null;

            for (var i = 0; i < 3; i++)
            {
                try
                {
                    return func();
                }
                catch (WebException ex)
                {
                    Loggers.MangaCrawler.Error("Exception, {0}", ex);

                    ex1 = ex;
                }
            }

            throw ex1;
        }

        internal HtmlDocument DownloadDocument(Server server, string url = null, CookieContainer cookies = null) => DownloadDocument(
            url ?? server.URL,
            () => server.State = ServerState.Downloading,
            () => Limiter.Aquire(server),
            () => Limiter.Release(server),
            cookies);

        internal HtmlDocument DownloadDocument(Serie serie, string url = null, CookieContainer cookies = null) => DownloadDocument(
            url ?? serie.URL, 
            () => serie.State  = SerieState.Downloading, 
            () => Limiter.Aquire(serie),
            () => Limiter.Release(serie),
            cookies);

        internal HtmlDocument DownloadDocument(Chapter chapter, string url = null, CookieContainer cookies = null) => DownloadDocument(
            url ?? chapter.URL, 
            () => chapter.State = ChapterState.DownloadingPagesList, 
            () => Limiter.Aquire(chapter),
            () => Limiter.Release(chapter), 
            chapter.Token,
            cookies);

        internal HtmlDocument DownloadDocument(Page page, string url = null, CookieContainer cookies = null) => DownloadDocument(
            url ?? page.URL, 
            () => page.State = PageState.Downloading, 
            () => Limiter.Aquire(page),
            () => Limiter.Release(page),
            page.Chapter.Token,
            cookies);

        internal HtmlDocument DownloadDocument(string url, Action started,
            Action aquire, Action release, CookieContainer cookies = null) => DownloadDocument(url, started, aquire, release, CancellationToken.None, cookies);

        private CookieContainer _cookiePot;
        internal bool OnPreRequest2(HttpWebRequest request)
        {
            request.CookieContainer = _cookiePot;
            return true;
        }
        protected void OnAfterResponse2(HttpWebRequest request, HttpWebResponse response)
        {
            //do nothing
        }

        internal HtmlDocument DownloadDocument(string url, Action started, 
            Action aquire, Action release, CancellationToken cancellationToken, CookieContainer cookies = null)
        {
            return DownloadWithRetry(() =>
            {
                aquire();

                started?.Invoke();

                try
                {
                    var web = new HtmlWeb();
                    /*cookiePot = cookies;
                    if (cookiePot != null)
                    {
                        web.UseCookies = true;
                        web.PreRequest = OnPreRequest2;
                        //web.PostResponse = OnAfterResponse2;
                    }*/

                    var page = web.Load(Uri.EscapeUriString(url));

                    if (web.StatusCode == HttpStatusCode.NotFound)
                    {
                        Loggers.MangaCrawler.InfoFormat(
                            "Series - page was not found, url: {0}",
                            url);

                        return null;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    Thread.Sleep(DownloadManager.Instance.MangaSettings.SleepAfterEachDownloadMS);

                    return page;
                }
                finally
                {
                    release();
                }
            });
        }

        internal virtual MemoryStream GetImageStream(Page page)
        {
            return DownloadWithRetry(() =>
            {
                try
                {
                    Limiter.Aquire(page);

                    var myReq = (HttpWebRequest)WebRequest.Create(
                        Uri.EscapeUriString(page.ImageURL));

                    myReq.UserAgent = DownloadManager.Instance.MangaSettings.UserAgent;
                    myReq.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                    myReq.Referer = Uri.EscapeUriString(page.URL);

                    var buffer = new byte[4*1024];

                    var memStream = new MemoryStream();

                    using (var imageStream = myReq.GetResponse().GetResponseStream())
                    {
                        for (;;)
                        {
                            var readed = imageStream.Read(buffer, 0, buffer.Length);

                            if (readed == 0)
                                break;

                            page.Chapter.Token.ThrowIfCancellationRequested();

                            memStream.Write(buffer, 0, readed);
                        }
                    }

                    Thread.Sleep(DownloadManager.Instance.MangaSettings.SleepAfterEachDownloadMS);

                    memStream.Position = 0;
                    return memStream;
                }
                finally
                {
                    Limiter.Release(page);
                }
            });
        }

        public virtual int MaxConnectionsPerServer => DownloadManager.Instance.MangaSettings.MaximumConnectionsPerServer;

        public virtual string GetImageURLExtension(string imageURL) => Path.GetExtension(imageURL);
    }
}
