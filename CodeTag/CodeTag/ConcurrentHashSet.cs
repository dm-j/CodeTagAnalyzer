using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CodeTag
{
    public class ConcurrentHashSet<T>
    {
        private readonly ConcurrentDictionary<T, byte> _internal;

        public ConcurrentHashSet(IEqualityComparer<T>? keyComparer = null)
        {
            keyComparer ??= EqualityComparer<T>.Default;
            _internal = new ConcurrentDictionary<T, byte>(keyComparer);
        }

        public bool Add(T item) =>
            _internal.TryAdd(item, 0);

        public bool Contains(T item) =>
            _internal.ContainsKey(item);
    }
}
