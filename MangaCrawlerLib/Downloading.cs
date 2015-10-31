using System.Collections.Generic;
using System.Linq;
using TomanuExtensions;

namespace MangaCrawlerLib
{
    /// <summary>
    /// Thread safe, copy on write semantic.
    /// </summary>
    public class Downloading
    {
        private List<Chapter> _downloading = new List<Chapter>();

        internal void Load()
        {
            IEnumerable<Chapter> downloading = from chapter in Catalog.LoadDownloadings()
                                               orderby chapter.LimiterOrder
                                               select chapter;

            DownloadManager.Instance.DownloadPages(downloading.ToList());
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public IEnumerable<Chapter> List
        {
            get
            {
                _downloading = (from chapter in _downloading
                                 where chapter.State != ChapterState.Cancelled
                                 select chapter).ToList();

                return _downloading;
            }
        }  

        public void Save()
        {
            Catalog.SaveDownloading(_downloading.Where(c => c.IsDownloading).ToList());
        }

        public void Remove(Chapter chapter)
        {
            _downloading = _downloading.Except(chapter).ToList();
        }

        internal void Add(Chapter chapter)
        {
            if (_downloading.Contains(chapter))
                return;

            var copy = _downloading.ToList();
            copy.Add(chapter);
            _downloading = copy;
        }
    }
}
