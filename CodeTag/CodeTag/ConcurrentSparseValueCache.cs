using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CodeTag
{
    public sealed class ConcurrentSparseValueCache<TKey, TValue>
    {
        private readonly ConcurrentHashSet<TKey> _usesDefaultValue;
        private readonly ConcurrentDictionary<TKey, TValue> _hasActualValue;
        private readonly TValue _defaultValue;

        public ConcurrentSparseValueCache(TValue defaultValue, IEqualityComparer<TKey>? keyComparer = null)
        {
            keyComparer ??= EqualityComparer<TKey>.Default;
            _usesDefaultValue = new ConcurrentHashSet<TKey>(keyComparer);
            _hasActualValue = new ConcurrentDictionary<TKey, TValue>(keyComparer);
            _defaultValue = defaultValue;
        }

        public TValue GetValue(TKey key, Func<TValue?> valueFactory)
        {
            if (TryGetValue(key, out var result))
                return result;

            result = valueFactory();

            if (result is null || (result is IEnumerable enumerableResult && !enumerableResult.GetEnumerator().MoveNext()))
            {
                _usesDefaultValue.Add(key);
                return _defaultValue;
            }

            _hasActualValue.AddOrUpdate(key, result, (_,_) => result);
            return result;
        }

        public bool TryGetValue(TKey key, out TValue result)
        {
            if (_usesDefaultValue.Contains(key))
            {
                result = _defaultValue;
                return true;
            }

            if (_hasActualValue.TryGetValue(key, out var value))
            {
                result = value;
                return true;
            }

            result = default!;
            return false;
        }

        public void Add(TKey key, TValue value)
        {
            _hasActualValue.AddOrUpdate(key, value, (_,_) => value);
        }

        public void AddEmpty(TKey key)
        {
            _usesDefaultValue.Add(key);
        }
    }
}
