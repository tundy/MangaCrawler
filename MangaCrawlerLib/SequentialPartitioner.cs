using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace MangaCrawlerLib
{
    internal class SequentialPartitioner<T> : Partitioner<T>
    {
        private readonly IList<T> _source;

        public SequentialPartitioner(IList<T> source)
        {
            _source = source;
        }

        public override bool SupportsDynamicPartitions => true;

        public override IList<IEnumerator<T>> GetPartitions(int partitionCount)
        {
            var dp = GetDynamicPartitions();
            return (from i in Enumerable.Range(0, partitionCount)
                    select dp.GetEnumerator()).ToList();
        }

        public override IEnumerable<T> GetDynamicPartitions()
        {
            return GetDynamicPartitions(_source.GetEnumerator());
        }

        private static IEnumerable<T> GetDynamicPartitions(IEnumerator<T> enumerator)
        {
            while (true)
            {
                T el;
                lock (enumerator)
                {
                    if (enumerator.MoveNext())
                        el = enumerator.Current;
                    else
                        yield break;
                }

                yield return el;
            }
        }
    }

}
