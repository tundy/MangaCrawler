using System;

namespace MangaCrawlerDLL
{
    internal class Page : Entity
    {
        internal override string GetDirectory()
        {
            throw new NotImplementedException();
        }

        internal Page(string url, int id) : this(url, id, string.Empty) { }
        internal Page(string url, int id, string title) : base(id)
        {
            URL = url;
            Title = title;
        }
    }
}