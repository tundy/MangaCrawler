using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using HtmlAgilityPack;
using System.Web;

namespace MangaCrawlerLib
{
    internal abstract class Crawler
    {
        public abstract string Name { get; }

        internal virtual string GetMiniature()
        {
            return string.Empty;
        }
        internal abstract void DownloadSeries(Server a_server, Action<int, IEnumerable<Serie>> a_progress_callback);
        internal abstract void DownloadChapters(Serie a_serie, Action<int, IEnumerable<Chapter>> a_progress_callback);
        internal abstract IEnumerable<Page> DownloadPages(Chapter a_chapter);
        public abstract string GetServerURL();
        internal abstract string GetImageURL(Page a_page);

        private static T DownloadWithRetry<T>(Func<T> a_func)
        {
            WebException ex1 = null;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    return a_func();
                }
                catch (WebException ex)
                {
                    Loggers.MangaCrawler.Error("Exception, {0}", ex);

                    ex1 = ex;
                    continue;
                }
            }

            throw ex1;
        }

        internal HtmlDocument DownloadDocument(Server a_server, string a_url = null, CookieContainer cookies = null)
        {
            return DownloadDocument(
                (a_url == null) ? a_server.URL : a_url,
                () => a_server.State = ServerState.Downloading,
                () => Limiter.Aquire(a_server),
                () => Limiter.Release(a_server),
                cookies);
        }

        internal HtmlDocument DownloadDocument(Serie a_serie, string a_url = null, CookieContainer cookies = null)
        {
            return DownloadDocument(
                (a_url == null) ? a_serie.URL : a_url, 
                () => a_serie.State  = SerieState.Downloading, 
                () => Limiter.Aquire(a_serie),
                () => Limiter.Release(a_serie),
                cookies);
        }

        internal HtmlDocument DownloadDocument(Chapter a_chapter, string a_url = null, CookieContainer cookies = null)
        {
            return DownloadDocument(
                (a_url == null) ? a_chapter.URL : a_url, 
                () => a_chapter.State = ChapterState.DownloadingPagesList, 
                () => Limiter.Aquire(a_chapter),
                () => Limiter.Release(a_chapter), 
                a_chapter.Token,
                cookies);
        }

        internal HtmlDocument DownloadDocument(Page a_page, string a_url = null, CookieContainer cookies = null)
        {
            return DownloadDocument(
                (a_url == null) ? a_page.URL : a_url, 
                () => a_page.State = PageState.Downloading, 
                () => Limiter.Aquire(a_page),
                () => Limiter.Release(a_page),
                a_page.Chapter.Token,
                cookies);
        }

        internal HtmlDocument DownloadDocument(string a_url, Action a_started,
            Action a_aquire, Action a_release, CookieContainer cookies = null)
        {
            return DownloadDocument(a_url, a_started, a_aquire, a_release, CancellationToken.None, cookies);
        }

        private CookieContainer cookiePot;
        internal bool OnPreRequest2(HttpWebRequest request)
        {
            request.CookieContainer = cookiePot;
            return true;
        }
        protected void OnAfterResponse2(HttpWebRequest request, HttpWebResponse response)
        {
            //do nothing
        }

        internal HtmlDocument DownloadDocument(string a_url, Action a_started, 
            Action a_aquire, Action a_release, CancellationToken a_token, CookieContainer cookies = null)
        {
            return DownloadWithRetry(() =>
            {
                a_aquire();

                if (a_started != null)
                    a_started();

                try
                {
                    var web = new HtmlWeb();
                    cookiePot = cookies;
                    if (cookiePot != null)
                    {
                        web.UseCookies = true;
                        web.PreRequest = new HtmlWeb.PreRequestHandler(OnPreRequest2);
                        web.PostResponse = new HtmlWeb.PostResponseHandler(OnAfterResponse2);
                    }
                    var page = web.Load(Uri.EscapeUriString(a_url));

                    if (web.StatusCode == HttpStatusCode.NotFound)
                    {
                        Loggers.MangaCrawler.InfoFormat(
                            "Series - page was not found, url: {0}",
                            a_url);

                        return null;
                    }

                    a_token.ThrowIfCancellationRequested();

                    Thread.Sleep(DownloadManager.Instance.MangaSettings.SleepAfterEachDownloadMS);

                    return page;
                }
                finally
                {
                    a_release();
                }
            });
        }

        internal virtual MemoryStream GetImageStream(Page a_page)
        {
            return DownloadWithRetry(() =>
            {
                try
                {
                    Limiter.Aquire(a_page);

                    HttpWebRequest myReq = (HttpWebRequest)WebRequest.Create(
                        Uri.EscapeUriString(a_page.ImageURL));

                    myReq.UserAgent = DownloadManager.Instance.MangaSettings.UserAgent;
                    myReq.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                    myReq.Referer = Uri.EscapeUriString(a_page.URL);

                    byte[] buffer = new byte[4*1024];

                    MemoryStream mem_stream = new MemoryStream();

                    using (Stream image_stream = myReq.GetResponse().GetResponseStream())
                    {
                        for (;;)
                        {
                            int readed = image_stream.Read(buffer, 0, buffer.Length);

                            if (readed == 0)
                                break;

                            a_page.Chapter.Token.ThrowIfCancellationRequested();

                            mem_stream.Write(buffer, 0, readed);
                        }
                    }

                    Thread.Sleep(DownloadManager.Instance.MangaSettings.SleepAfterEachDownloadMS);

                    mem_stream.Position = 0;
                    return mem_stream;
                }
                finally
                {
                    Limiter.Release(a_page);
                }
            });
        }

        public virtual int MaxConnectionsPerServer
        {
            get
            {
                return DownloadManager.Instance.MangaSettings.MaximumConnectionsPerServer;
            }
        }

        public virtual string GetImageURLExtension(string a_image_url)
        {
            return Path.GetExtension(a_image_url);
        }
    }
}
