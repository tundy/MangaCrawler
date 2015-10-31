using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace MangaCrawlerLib
{
    /// <summary>
    /// Thread safe, copy on write semantic.
    /// </summary>
    public class Bookmarks
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Serie> _bookmarks = new List<Serie>();

        internal void Load()
        {
            _bookmarks = Catalog.LoadBookmarks();
        }

        internal void RemoveNotExisted()
        {
            _bookmarks = (from serie in _bookmarks
                           where serie.Server.Series.Contains(serie)
                           select serie).ToList();
        }

        private static void Save()
        {
            Catalog.SaveBookmarks();
        }

        public void Add(Serie serie)
        {
            var copy = _bookmarks.ToList();
            copy.Add(serie);
            _bookmarks = copy;

            DownloadManager.Instance.BookmarksVisited(serie.Chapters);

            Save();
        }

        /// <summary>
        /// Thread safe.
        /// </summary>
        public IEnumerable<Serie> List => _bookmarks;

        public void Remove(Serie serie)
        {
            var copy = _bookmarks.ToList();
            copy.Remove(serie);
            _bookmarks = copy;

            Save();
        }

        public IEnumerable<Serie> GetSeriesWithNewChapters() => (from serie in _bookmarks
                                                                 where serie.GetNewChapters().Any()
                                                                 select serie).ToList();
    }
}