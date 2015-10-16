using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MangaCrawlerDLL
{
    internal class Server : Entity
    {

        internal override string GetDirectory()
        {
            throw new NotImplementedException();
        }

        internal Server(string url, int id, string title) : base(id)
        {
            URL = url;
            Title = title;
        }
    }
}
