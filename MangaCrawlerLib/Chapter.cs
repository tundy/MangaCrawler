using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Diagnostics;
using TomanuExtensions.Utils;
using Ionic.Zip;
using System.Threading.Tasks;

namespace MangaCrawlerLib
{
    public class Chapter : Entity 
    {
        #region PagesCachedList
        private class PagesCachedList : CachedList<Page>
        {
            private readonly Chapter _chapter;

            public PagesCachedList(Chapter chapter)
            {
                _chapter = chapter;
            }

            protected override void EnsureLoaded()
            {
                lock (Lock)
                {
                    if (List != null)
                        return;

                    List = Catalog.LoadChapterPages(_chapter);
                }
            }
        }
        #endregion

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private CancellationTokenSource _cancellationTokenSource;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ChapterState _state;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly PagesCachedList _pages;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly object _stateLock = new object();

        public Serie Serie { get; }
        public string Title { get; internal set; }
        public bool Visited { get; internal set; }

        internal Chapter(Serie serie, string url, string title)
            : this(serie, url, title, Catalog.NextID(), ChapterState.Initial, 0, false)
        {
        }

        internal Chapter(Serie serie, string url, string title, ulong id, ChapterState chapterState,
            ulong limiterOrder, bool visited)
            : base(id)
        {
            Visited = visited;
            _pages = new PagesCachedList(this);
            LimiterOrder = limiterOrder;
            Serie = serie;
            URL = HtmlDecode(url);
            _state = chapterState;

            if (_state == ChapterState.Cancelling)
                _state = ChapterState.Initial;
            if (_state == ChapterState.DownloadingPages)
                _state = ChapterState.Initial;
            if (_state == ChapterState.DownloadingPagesList)
                _state = ChapterState.Initial;
            if (_state == ChapterState.Waiting)
                _state = ChapterState.Initial;
            if (_state == ChapterState.Zipping)
                _state = ChapterState.Initial;

            title = title.Trim();
            title = title.Replace("\t", " ");
            while (title.IndexOf("  ") != -1)
                title = title.Replace("  ", " ");
            Title = HtmlDecode(title);
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public IList<Page> Pages => _pages;

        public int PagesDownloaded => Pages.Count(p => p.State == PageState.Downloaded);

        public Server Server => Serie.Server;

        internal override Crawler Crawler => Serie.Crawler;

        public override bool IsDownloading => (State == ChapterState.Waiting) ||
                                              (State == ChapterState.DownloadingPages) ||
                                              (State == ChapterState.DownloadingPagesList) ||
                                              (State == ChapterState.Cancelling) ||
                                              (State == ChapterState.Zipping);

        public override string ToString() => $"{Serie} - {Title}";

        public void CancelDownloading()
        {
            if (State == ChapterState.Cancelling)
                return;

            lock (_stateLock)
            {
                if (IsDownloading)
                    State = ChapterState.Cancelling;
            }
        }

        public override string GetDirectory()
        {
            var mangaRootDir = DownloadManager.Instance.MangaSettings.GetMangaRootDir(true);

            return mangaRootDir +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Serie.Server.Name) +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Serie.Title) +
                   Path.DirectorySeparatorChar +
                   FileUtils.RemoveInvalidFileCharacters(Title) +
                   Path.DirectorySeparatorChar;
        }

        internal void DownloadPagesList()
        {
            var pages = Crawler.DownloadPages(this).ToList();

            _pages.ReplaceInnerCollection(pages);

            State = ChapterState.DownloadingPages;

            foreach (var page in Pages)
            {
                page.State = PageState.Waiting;

                Debug.Assert(page.Index == Pages.IndexOf(page) + 1);
            }

            Catalog.Save(this);
        }

        internal void DownloadPagesAndImages()
        {
            try
            {
                Limiter.BeginChapter(this);

                try
                {
                    DownloadPagesList();

                    var names = Pages.Select(p => p.Name);
                    var sortedNames = Pages.Select(p => p.Name).OrderBy(n => n, new NaturalOrderStringComparer());
                    var error = false;

                    var pns = DownloadManager.Instance.MangaSettings.PageNamingStrategy;
                    if (pns == PageNamingStrategy.IndexToPreserveOrder)
                    {
                        if (!names.SequenceEqual(sortedNames))
                            pns = PageNamingStrategy.AlwaysUseIndex;
                    }
                    else if (pns == PageNamingStrategy.PrefixToPreserverOrder)
                    {
                        if (!names.SequenceEqual(sortedNames))
                            pns = PageNamingStrategy.AlwaysUsePrefix;
                    }

                    for (var i = 0; i < Pages.Count; i++)
                    {
                        Pages[i].LimiterOrder = Catalog.NextID();

                        Debug.Assert(Pages[i].Index == i + 1);
                    }

                    Parallel.ForEach(new SequentialPartitioner<Page>(Pages),

                        new ParallelOptions()
                        {
                            MaxDegreeOfParallelism = Crawler.MaxConnectionsPerServer
                        },
                        (page, state) =>
                        {
                            try
                            {
                                page.DownloadAndSavePageImage(pns);

                                Catalog.Save(this);
                            }
                            catch (OperationCanceledException)
                            {
                                state.Break();
                            }
                            catch (Exception ex2)
                            {
                                Loggers.MangaCrawler.Error($"Exception #1, chapter: {this} state: {State}", ex2);

                                error = true;
                            }
                        }
                    );

                    Token.ThrowIfCancellationRequested();
   
                    if (PagesDownloaded != Pages.Count)
                        State = ChapterState.Error;
                    else if (Pages.Any(p => p.State != PageState.Downloaded))
                        State = ChapterState.Error;
                    else if (error)
                        State = ChapterState.Error;

                    Catalog.Save(this);

                    if (DownloadManager.Instance.MangaSettings.UseCBZ)
                        if (State != ChapterState.Error)
                            CreateCBZ();

                    Visited = true;
                }
                finally
                {
                    Limiter.EndChapter(this);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Assert(State == ChapterState.Cancelling);
                Debug.Assert(_cancellationTokenSource.IsCancellationRequested);

                State = ChapterState.Cancelled;
            }
            catch (Exception ex1)
            {
                Loggers.MangaCrawler.Error($"Exception #2, chapter: {this} state: {State}", ex1);

                State = ChapterState.Error;

                try
                {
                    DownloadManager.Instance.DownloadChapters(Serie, true);
                }
                catch (Exception ex2)
                {
                    Loggers.MangaCrawler.Error($"Exception #3, chapter: {this} state: {State}", ex2);
                }
            }
            finally
            {
                lock (_stateLock)
                {
                    if ((State != ChapterState.Error) && (State != ChapterState.Cancelled))
                    {
                        Debug.Assert(
                            (State == ChapterState.DownloadingPages) || 
                            (State == ChapterState.Zipping));
                        State = ChapterState.Downloaded;
                    }
                }
            }

            Catalog.Save(this);
        }

        public bool CanReadCBZ() => File.Exists(Serie.GetDirectory() + "/" + Title + ".cbz");

        public string PathCBZ() => Serie.GetDirectory() + "/" + Title + ".cbz";

        private void CreateCBZ()
        {
            Debug.Assert(State == ChapterState.DownloadingPages);

            Loggers.MangaCrawler.InfoFormat(
                "Chapter: {0} state: {1}",
                this, State);

            if (Pages.Count == 0)
            {
                Loggers.MangaCrawler.InfoFormat("Pages.Count = 0 - nothing to zip");
                return;
            }

            State = ChapterState.Zipping;

            var dir = new DirectoryInfo(Pages.First().ImageFilePath).Parent;

            Debug.Assert(dir != null, "dir != null");
            // ReSharper disable once PossibleNullReferenceException
            var zipFile = dir.FullName + ".cbz";

            try
            {
                using (var zip = new ZipFile())
                {
                    zip.AlternateEncodingUsage = ZipOption.AsNecessary;
                    zip.AlternateEncoding = Encoding.UTF8;

                    foreach (var page in Pages)
                    {
                        zip.AddFile(page.ImageFilePath, "");

                        Token.ThrowIfCancellationRequested();
                    }

                    zip.Save(zipFile);
                }

                if (!DownloadManager.Instance.MangaSettings.DeleteDirWithImagesWhenCBZ) return;
                foreach (var page in Pages.Where(page => !string.IsNullOrWhiteSpace(page.ImageFilePath)).Where(page => File.Exists(page.ImageFilePath)))
                {
                    File.Delete(page.ImageFilePath);
                }

                if (Directory.EnumerateFiles(GetDirectory()).Any()) return;
                if (!Directory.EnumerateDirectories(GetDirectory()).Any())
                    Directory.Delete(GetDirectory());
            }
            catch (Exception ex)
            {
                State = ChapterState.Error;

                Loggers.MangaCrawler.Error($"Exception, chapter: {this} state: {State}", ex);
            }
        }

        internal CancellationToken Token => _cancellationTokenSource.Token;

        public ChapterState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _state;
                }
            }
            internal set
            {
                lock (_stateLock)
                {
                    switch (value)
                    {
                        case ChapterState.Initial:
                        {
                            break;
                        }
                        case ChapterState.Waiting:
                        {
                            Debug.Assert((State == ChapterState.Cancelled) ||
                                         (State == ChapterState.Downloaded) ||
                                         (State == ChapterState.Error) ||
                                         (State == ChapterState.Initial));
                            _cancellationTokenSource = new CancellationTokenSource();
                            break;
                        }
                        case ChapterState.DownloadingPagesList:
                        {
                            Token.ThrowIfCancellationRequested();
                            Debug.Assert((State == ChapterState.Waiting) ||
                                         (State == ChapterState.DownloadingPagesList));
                            break;
                        }
                        case ChapterState.DownloadingPages:
                        {
                            Token.ThrowIfCancellationRequested();
                            Debug.Assert(State == ChapterState.DownloadingPagesList);
                            break;
                        }
                        case ChapterState.Zipping:
                        {
                            Token.ThrowIfCancellationRequested();
                            Debug.Assert(State == ChapterState.DownloadingPages);
                            break;
                        }
                        case ChapterState.Cancelled:
                        {
                            Debug.Assert(State == ChapterState.Cancelling);
                            break;
                        }
                        case ChapterState.Cancelling:
                        {
                            Debug.Assert((State == ChapterState.DownloadingPages) ||
                                         (State == ChapterState.DownloadingPagesList) ||
                                         (State == ChapterState.Waiting) ||
                                         (State == ChapterState.Zipping));
                            _cancellationTokenSource.Cancel();
                            break;
                        }
                        case ChapterState.Error:
                        {
                            Debug.Assert((State == ChapterState.DownloadingPages) ||
                                         (State == ChapterState.DownloadingPagesList) ||
                                         (State == ChapterState.Waiting) ||
                                         (State == ChapterState.Error) ||
                                         (State == ChapterState.Zipping));
                            break;
                        }
                        case ChapterState.Downloaded:
                        {
                            Debug.Assert((State == ChapterState.DownloadingPages) ||
                                         (State == ChapterState.Zipping));
                            break;
                        }
                        default:
                        {
                            throw new InvalidOperationException($"Unknown state: {value}");
                        }
                    }

                    _state = value;
                }
            }
        }

        public bool CanReadFirstPage()
        {
            if (!Pages.Any()) return CanReadCBZ();
            if (string.IsNullOrWhiteSpace(Pages.First().ImageFilePath)) return CanReadCBZ();

            try
            {
                if (new FileInfo(Pages.First().ImageFilePath).Exists)
                    return true;
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error($"Exception, chapter: {this} state: {State}", ex);

                return false;
            }

            return CanReadCBZ();
        }
    }
}
