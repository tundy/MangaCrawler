using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using TomanuExtensions.Utils;

namespace MangaCrawlerLib
{
    public class Page : Entity
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private PageState _pageState;

        public Chapter Chapter { get; protected set; }
        public int Index { get; protected set; }
        public byte[] Hash { get; protected set; }
        public string ImageURL { get; protected set; }
        public string Name { get; protected set; }
        public string ImageFilePath { get; protected set; }

        internal Page(Chapter a_chapter, string url, int index, string name)
            : this(a_chapter, url, index, Catalog.NextID(), name, null, null, PageState.Initial)
        {
        }

        internal Page(Chapter a_chapter, string url, int index, ulong id, string name, byte[] hash, 
            string imageFilePath, PageState pageState) : base(id)
        {
            Hash = hash;
            ImageFilePath = imageFilePath;
            _pageState = pageState;

            Chapter = a_chapter;
            URL = HtmlDecode(url);
            Index = index;

            if (State == PageState.Downloading)
                _pageState = PageState.Initial;
            if (State == PageState.Waiting)
                _pageState = PageState.Initial;

            if (name != "")
            {
                name = name.Trim();
                name = name.Replace("\t", " ");
                while (name.IndexOf("  ") != -1)
                    name = name.Replace("  ", " ");
                name = HtmlDecode(name);
                Name = FileUtils.RemoveInvalidFileCharacters(name);
            }
            else
                Name = Index.ToString();
        }

        internal override Crawler Crawler => Chapter.Crawler;

        public Server Server => Chapter.Server;

        public Serie Serie => Chapter.Serie;

        public override string ToString() => $"{Chapter} - {Index}/{Chapter.Pages.Count}";

        internal void GetImageURL()
        {
            if (ImageURL == null)
                ImageURL = HtmlDecode(Crawler.GetImageURL(this));
        }

        internal MemoryStream GetImageStream()
        {
            GetImageURL();
            return Crawler.GetImageStream(this);  
        }

        internal void DownloadAndSavePageImage(PageNamingStrategy pageNamingStrategy)
        {
            new DirectoryInfo(Chapter.GetDirectory()).Create();

            var tempFile = new FileInfo(Path.GetTempFileName());

            try
            {
                using (var fileStream = new FileStream(tempFile.FullName, FileMode.Create))
                {
                    MemoryStream ms;

                    try
                    {
                        ms = GetImageStream();
                    }
                    catch (Exception ex1)
                    {
                        Loggers.MangaCrawler.Error("Exception #1", ex1);
                        throw;
                    }

                    try
                    {
                        try
                        {
                            System.Drawing.Image.FromStream(ms).Dispose();
                            ms.Position = 0;
                        }
                        catch (Exception ex2)
                        {
                            Loggers.MangaCrawler.Error("Exception #2", ex2);
                            throw;
                        }

                        ms.CopyTo(fileStream);

                        ms.Position = 0;
                        byte[] hash;
                        TomanuExtensions.Utils.Hash.CalculateSHA256(ms, out hash);
                        Hash = hash;
                    }
                    finally
                    {
                        ms.Dispose();
                    }
                }

                var fileName = Rename(pageNamingStrategy, Name);

                ImageFilePath = Path.Combine(
                    Chapter.GetDirectory(),
                    FileUtils.RemoveInvalidFileCharacters(
                        Path.GetFileNameWithoutExtension(fileName) + Crawler.GetImageURLExtension(ImageURL).ToLower()));

                var imageFile = new FileInfo(ImageFilePath);

                if (imageFile.Exists)
                    imageFile.Delete();

                tempFile.MoveTo(imageFile.FullName);

                State = PageState.Downloaded;
            }
            finally
            {
                if (tempFile.Exists)
                    if (tempFile.FullName != ImageFilePath)
                        tempFile.Delete();
            }
        }

        private string Rename(PageNamingStrategy pageNamingStrategy, string name)
        {
            Debug.Assert((pageNamingStrategy != PageNamingStrategy.IndexToPreserveOrder) || 
                         (pageNamingStrategy != PageNamingStrategy.PrefixToPreserverOrder));

            var index = Index.ToString();
            if (DownloadManager.Instance.MangaSettings.PadPageNamesWithZeros)
                index = index.PadLeft(Chapter.Pages.Count.ToString().Length, '0');

            switch (pageNamingStrategy)
            {
                case PageNamingStrategy.AlwaysUseIndex:
                    return index;
                case PageNamingStrategy.AlwaysUsePrefix:
                    return $"{index} - {Name}";
                default:
                    return name;
            }
        }

        internal bool RequiredDownload(string chapterDir)
        {
            try
            {
                if (ImageFilePath == null)
                    return true;
                // ReSharper disable once PossibleNullReferenceException
                if (new FileInfo(ImageFilePath).Directory.FullName + "\\" != chapterDir)
                    return true;
                if (!new FileInfo(ImageFilePath).Exists)
                    return true;
                if (Hash == null)
                    return true;

                byte[] hash;
                TomanuExtensions.Utils.Hash.CalculateSHA256(new FileInfo(ImageFilePath).OpenRead(), out hash);

                return !Hash.SequenceEqual(hash);
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Error("Exception", ex);
                return true;
            }
        }

        public PageState State
        {
            get
            {
                return _pageState;
            }
            internal set
            {
                switch (value)
                {
                    case PageState.Downloaded:
                    {
                        Debug.Assert(State == PageState.Downloading);
                        break;
                    }
                    case PageState.Downloading:
                    {
                        Debug.Assert((State == PageState.Waiting) ||
                                     (State == PageState.Downloading));
                        break;
                    }
                    case PageState.Error:
                    {
                        Debug.Assert(State == PageState.Downloading);
                        break;
                    }
                    case PageState.Waiting:
                    {
                        Debug.Assert((State == PageState.Downloaded) ||
                                     (State == PageState.Initial) ||
                                     (State == PageState.Error));
                        break;
                    }
                    default:
                    {
                        throw new InvalidOperationException($"Unknown state: {value}");
                    }
                }

                _pageState = value;
            }
        }

        public override bool IsDownloading => (State == PageState.Downloading) ||
                                              (State == PageState.Waiting);

        public override string GetDirectory() => Chapter.GetDirectory();
    }
}
