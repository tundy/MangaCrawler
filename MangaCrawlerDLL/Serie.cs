using System;

namespace MangaCrawlerDLL
{
    internal class Serie : Entity
    {
        internal override string GetDirectory()
        {
            throw new NotImplementedException();
        }

        internal Serie(string url, int id, string title) : base(id)
        {
            URL = url;
            Title = title;
        }
    }
}