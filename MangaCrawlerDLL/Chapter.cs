using System;

namespace MangaCrawlerDLL
{
    internal class Chapter : Entity
    {
        internal override string GetDirectory()
        {
            throw new NotImplementedException();
        }

        internal Chapter(string url, int id, string title) : base(id)
        {
            URL = url;
            Title = title;
        }
    }
}