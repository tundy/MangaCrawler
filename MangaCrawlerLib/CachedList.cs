using System;
using System.Collections.Generic;
using System.Linq;

namespace MangaCrawlerLib
{
    /// <summary>
    /// Update catalog with @new.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="catalog"></param>
    /// <param name="new"></param>
    public delegate void Merge<in T>(T catalog, T @new);

    /// <summary>
    /// Thread-safe, copy on write semantic.
    /// </summary>
    internal abstract class CachedList<T> : IList<T> where T : Entity
    {
        protected List<T> List;
        protected object Lock = new object();

        internal void ReplaceInnerCollection(IEnumerable<T> @new) 
        {
            EnsureLoaded();

            var list = List.ToList();

            foreach (var el in List.Except(@new))
                list.Remove(el);

            var index = 0;
            foreach (var el in @new)
            {
                if (list.Count == index)
                    list.Insert(index, el);
                if (list[index] != el)
                    list.Insert(index, el);
                index++;
            }

            List = list;
        }

        internal void ReplaceInnerCollection(IEnumerable<T> @new, bool remove, Func<T, string> keySelector, 
            Merge<T> merge)
        {
            EnsureLoaded();

            var copy = List.ToList();
            var newList = @new.ToList();

            Merge(newList, copy, keySelector, merge);

            if (remove)
            {
                var toRemove = copy.Except(newList).ToList();
                newList.RemoveAll(el => toRemove.Contains(el));
            }

            List = newList;
        }

        private static void Merge(IList<T> @new, IEnumerable<T> local,
            Func<T, string> keySelector, Merge<T> merge)
        {         
            var localDict = local.ToDictionary(keySelector);

            for (var i = 0; i < @new.Count; i++)
            {
                var key = keySelector(@new[i]);
                if (!localDict.ContainsKey(key)) continue;
                merge(localDict[key], @new[i]);
                @new[i] = localDict[key];
            }
        }

        internal bool Filled => (List != null);

        public int IndexOf(T item)
        {
            EnsureLoaded();

            return List.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            throw new InvalidOperationException();
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        public T this[int index]
        {
            get
            {
                EnsureLoaded();

                return List[index];
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public void Add(T item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            EnsureLoaded();

            return List.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            EnsureLoaded();

            List.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get
            {
                EnsureLoaded();

                return List.Count;
            }
        }

        public bool IsReadOnly => false;

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            EnsureLoaded();

            return List.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            EnsureLoaded();

            return List.GetEnumerator();
        }

        public override string ToString()
        {
            var list = List;

            return list == null ? "Uninitialized" : $"Count: {list.Count}";
        }

        protected abstract void EnsureLoaded();
    }
}
