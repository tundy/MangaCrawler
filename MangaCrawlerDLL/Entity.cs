using System;

namespace MangaCrawlerDLL
{
    internal abstract class Entity
    {
        internal int ID { get; set; }
        internal string URL { get; set; }
        internal string Miniature { get; set; }
        internal string Title { get; set; }

        internal abstract string GetDirectory();

        protected Entity(int id)
        {
            ID = id;
        }
    }
}