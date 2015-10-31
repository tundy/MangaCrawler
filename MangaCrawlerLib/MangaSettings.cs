using System;
using System.Linq;
using System.IO;
using TomanuExtensions;
using System.Xml.Linq;

namespace MangaCrawlerLib
{
    public class MangaSettings
    {
        private string _mangaRootDir = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory) +
            Path.DirectorySeparatorChar + "Manga Crawler";

        private bool _useCBZ;

        private bool _deleteDirWithImagesWhenCBZ;

        private TimeSpan _checkTimePeriod = new TimeSpan(hours: 0, minutes: 15, seconds: 0);

        private PageNamingStrategy _pageNamingStrategy = PageNamingStrategy.DoNotChange;

        private bool _padPageNamesWithZeros;

        private string _userAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:26.0) Gecko/20100101 Firefox/26.0";

        // Sync with MangaCrawler/app.config
        public int MaximumConnections { get; set; }

        public int MaximumConnectionsPerServer { get; set; }

        public int SleepAfterEachDownloadMS { get; set; }

        public event Action Changed;

        private const string XML_MANGASETTINGS = "MangaSettings";
        private const string XML_MANGAROOTDIR = "MangaRootDir";
        private const string XML_USECBZ = "UseCBZ";
        private const string XML_DELETEDIRWITHIMAGESWHENCBZ = "DeleteDirWithImagesWhenCBZ";
        private const string XML_CHECKTIMEPERIOD = "CheckTimePeriod";
        private const string XML_PAGENAMINGSTRATEGY = "PageNamingStrategy";
        private const string XML_PADPAGENAMESWITHZEROS = "PadPageNamesWithZeros";

        public MangaSettings()
        {
            MaximumConnections = 100;
            MaximumConnectionsPerServer = 4;
            SleepAfterEachDownloadMS = 0;
        }

        public string GetMangaRootDir(bool removeSlashOnEnd)
        {
            var result = _mangaRootDir;

            if (!removeSlashOnEnd) return result;
            if (result.Last() == Path.DirectorySeparatorChar)
                result = result.RemoveFromRight(1);

            return result;
        }

        public void SetMangaRootDir(string mangaRootDir)
        {
            _mangaRootDir = mangaRootDir;
            OnChanged();
        }

        private void OnChanged()
        {
            Changed?.Invoke();
        }

        public bool UseCBZ
        {
            get
            {
                return _useCBZ;
            }
            set
            {
                _useCBZ = value;
                OnChanged();
            }
        }

        public bool DeleteDirWithImagesWhenCBZ
        {
            get
            {
                return _deleteDirWithImagesWhenCBZ;
            }
            set
            {
                _deleteDirWithImagesWhenCBZ = value;
                OnChanged();
            }
        }

        public TimeSpan CheckTimePeriod
        {
            get
            {
                return _checkTimePeriod;
            }
            private set
            {
                _checkTimePeriod = value;
                OnChanged();
            }
        }

        public PageNamingStrategy PageNamingStrategy
        {
            get
            {
                return _pageNamingStrategy;
            }
            set
            {
                _pageNamingStrategy = value;
                OnChanged();
            }
        }

        public bool PadPageNamesWithZeros
        {
            get
            {
                return _padPageNamesWithZeros;
            }
            set
            {
                _padPageNamesWithZeros = value;
                OnChanged();
            }
        }

        public string UserAgent => _userAgent;

        public bool IsMangaRootDirValid
        {
            get
            {
                try
                {
                    new DirectoryInfo(GetMangaRootDir(false));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static MangaSettings Load(XElement node)
        {
            if (node.Name != XML_MANGASETTINGS)
                throw new Exception();

            return new MangaSettings()
            {
                _mangaRootDir = node.Element(XML_MANGAROOTDIR).Value, 
                UseCBZ = bool.Parse(node.Element(XML_USECBZ).Value), 
                DeleteDirWithImagesWhenCBZ = bool.Parse(node.Element(XML_DELETEDIRWITHIMAGESWHENCBZ).Value), 
                CheckTimePeriod = TimeSpan.Parse(node.Element(XML_CHECKTIMEPERIOD).Value), 
                PageNamingStrategy = (PageNamingStrategy)Enum.Parse(
                    typeof(PageNamingStrategy), node.Element(XML_PAGENAMINGSTRATEGY).Value),
                PadPageNamesWithZeros = bool.Parse(node.Element(XML_PADPAGENAMESWITHZEROS).Value)
            };
        }

        public XElement GetAsXml()
        {
            return new XElement(XML_MANGASETTINGS,
                new XElement(XML_MANGAROOTDIR, _mangaRootDir), 
                new XElement(XML_USECBZ, UseCBZ), 
                new XElement(XML_DELETEDIRWITHIMAGESWHENCBZ, DeleteDirWithImagesWhenCBZ), 
                new XElement(XML_CHECKTIMEPERIOD, CheckTimePeriod.ToString("hh\\:mm\\:ss")), 
                new XElement(XML_PAGENAMINGSTRATEGY, PageNamingStrategy), 
                new XElement(XML_PADPAGENAMESWITHZEROS, PadPageNamesWithZeros));
        }
    }
}
