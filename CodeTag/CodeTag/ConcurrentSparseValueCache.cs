using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CodeTag
{
    public sealed class ConcurrentSparseValueCache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, byte> _missingValues;
        private readonly ConcurrentDictionary<TKey, TValue> _presentValues;
        private readonly TValue _defaultValue;

        public ConcurrentSparseValueCache(TValue defaultValue, IEqualityComparer<TKey>? keyComparer = null)
        {
            keyComparer ??= EqualityComparer<TKey>.Default;
            _missingValues = new ConcurrentDictionary<TKey, byte>(keyComparer);
            _presentValues = new ConcurrentDictionary<TKey, TValue>(keyComparer);
            _defaultValue = defaultValue;
        }

        public TValue GetValue(TKey key, Func<TValue?> valueFactory)
        {
            if (_missingValues.ContainsKey(key))
                return _defaultValue;

            if (_presentValues.TryGetValue(key, out var value))
                return value;

            var computedValue = valueFactory();

            if (computedValue is null)
            {
                _missingValues.TryAdd(key, 0);
                return _defaultValue;
            }

            _presentValues.TryAdd(key, computedValue);
            return computedValue;
        }
    }
}
