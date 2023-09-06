using System.Collections;
using System.Collections.Generic;

namespace CodeTag
{
    public class ReadOnlyHashSet<T> : IReadOnlyCollection<T>
    {
        private readonly HashSet<T> _internal;

        public ReadOnlyHashSet(HashSet<T> hashSet)
        {
            _internal = new HashSet<T>(hashSet, hashSet.Comparer);
        }

        public IEnumerator<T> GetEnumerator() =>
            _internal.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            ((IEnumerable)_internal).GetEnumerator();

        public int Count =>
            _internal.Count;

        public bool Contains(T item) =>
            _internal.Contains(item);
    }

    public static class ReadOnlyHashSetExtensions
    {
        public static ReadOnlyHashSet<T> AsReadOnly<T>(this HashSet<T> hashSet) =>
            new(hashSet);
    }
}
