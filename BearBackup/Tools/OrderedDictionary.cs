using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace BearBackup.Tools;

public class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dictionary;
    private readonly List<TKey> _orderedKeys;

    public OrderedDictionary()
    {
        _dictionary = [];
        _orderedKeys = [];
    }

    public OrderedDictionary(KeyValuePair<TKey, TValue>[] keyValuePairs)
    {
        _dictionary = [];
        _orderedKeys = [];

        foreach ((var key, var value) in keyValuePairs)
        {
            _dictionary.Add(key, value);
            _orderedKeys.Add(key);
        }
    }

    public TValue this[TKey key]
    {
        get => _dictionary[key];
        set => _dictionary[key] = value;
    }

    public ICollection<TKey> Keys => _orderedKeys;
    public ICollection<TValue> Values => _orderedKeys.Select(key => _dictionary[key]).ToList();
    public int Count => _dictionary.Count;
    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        _dictionary.Add(key, value);
        _orderedKeys.Add(key);
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        _dictionary.Add(item.Key, item.Value);
        _orderedKeys.Add(item.Key);
    }

    public void Insert(int index, TKey key, TValue value)
    {
        if (index < 0 || index > _orderedKeys.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _dictionary.Add(key, value);
        _orderedKeys.Insert(index, key);
    }

    public void Insert(int index, KeyValuePair<TKey, TValue> item)
    {
        if (index < 0 || index > _orderedKeys.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _dictionary.Add(item.Key, item.Value);
        _orderedKeys.Insert(index, item.Key);
    }

    public int IndexOf(TKey key)
    {
        return _orderedKeys.IndexOf(key);
    }

    public bool ChangeKey(TKey oldKey, TKey newKey)
    {
        if (!_dictionary.ContainsKey(oldKey)) return false;
        if (!oldKey.Equals(newKey) && _dictionary.ContainsKey(newKey)) return false;

        var value = _dictionary[oldKey];
        _dictionary.Remove(oldKey);
        _dictionary.Add(newKey, value);

        var index = _orderedKeys.IndexOf(oldKey);
        _orderedKeys[index] = newKey;

        return true;
    }

    public void Clear()
    {
        _dictionary.Clear();
        _orderedKeys.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return _dictionary.Contains(item);
    }

    public bool ContainsKey(TKey key)
    {
        return _dictionary.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        _orderedKeys.Select(key => new KeyValuePair<TKey, TValue>(key, _dictionary[key]))
                    .ToList()
                    .CopyTo(array, arrayIndex);
    }

    public bool Remove(TKey key)
    {
        var result = _dictionary.Remove(key);
        _orderedKeys.Remove(key);

        return result;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        var result = _dictionary.Remove(item.Key);
        _orderedKeys.Remove(item.Key);

        return result;
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _orderedKeys.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var key = _orderedKeys[index];
        var result = _dictionary.Remove(key);
        _orderedKeys.RemoveAt(index);

        return result;
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        return _dictionary.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return new OrderedDictionaryEnumerator<TKey, TValue>(_dictionary, _orderedKeys);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public class OrderedDictionaryEnumerator<TKey, TValue> : IEnumerator<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dictionary;
    private readonly List<TKey> _orderKeyList;
    private int _currentPos;

    public KeyValuePair<TKey, TValue> Current =>
        new KeyValuePair<TKey, TValue>(_orderKeyList[_currentPos], _dictionary[_orderKeyList[_currentPos]]);

    object IEnumerator.Current => Current;

    public OrderedDictionaryEnumerator(Dictionary<TKey, TValue> dictionary, List<TKey> orderKeyList)
    {
        _dictionary = dictionary;
        _orderKeyList = orderKeyList;
        _currentPos = -1;
    }

    public bool MoveNext()
    {
        if (_currentPos < _orderKeyList.Count - 1)
        {
            _currentPos++;
            return true;
        }
        else
        {
            return false;
        }
    }

    public void Reset()
    {
        _currentPos = -1;
    }

    public void Dispose() { }
}
