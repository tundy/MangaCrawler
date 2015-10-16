using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaCrawlerDLL
{
    internal abstract class Crawler
    {
        internal abstract string Name { get; }
        internal abstract void GetSeries(Server server, Action<int, IEnumerable<Serie>> a_progress_callback);
        internal abstract void GetChapters(Chapter chapter, Action<int, IEnumerable<Chapter>> a_progress_callback);
        internal abstract IEnumerable<Page> DownloadPages(Chapter a_chapter);
        internal abstract string GetServerURL();
        internal abstract string GetImageURL(Page page);
    }
}
