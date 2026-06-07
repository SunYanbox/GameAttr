using System.Collections.Concurrent;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace GameAttr;

/// <summary>A generic attribute system for games.</summary>
public class Attr<TKey, TModId, TValue>
    where TKey : notnull
    where TModId : notnull
    where TValue : INumber<TValue>
{
    private readonly ConcurrentDictionary<TKey, ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>> _modifiers = new();
    private readonly ConcurrentDictionary<TKey, object> _keyLocks = new();
    private readonly ConcurrentDictionary<TKey, TValue> _valueCache = new();
    private readonly Lock _globalLock = new();

    // Generation counter to detect concurrent global mutations (RemoveModifier(TModId), Clear).
    // Incremented under _globalLock; read under per-key lock so GetValue can detect
    // cross-lock races and avoid caching stale or inconsistent values.
    private long _globalGen;

    private object GetLock(TKey key) => _keyLocks.GetOrAdd(key, _ => new object());

    private void InvalidateCache(TKey key) => _valueCache.TryRemove(key, out _);

    /// <summary>Set or overwrite a modifier for the given attribute key.</summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="type">Modifier type (BaseValue, PercentBonus, FlatBonus).</param>
    /// <param name="modId">Unique modifier identifier.</param>
    /// <param name="value">Modifier value.</param>
    public void SetModifier(TKey key, ModifierType type, TModId modId, TValue value)
    {
        lock (GetLock(key))
        {
            ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>> byType =
                _modifiers.GetOrAdd(key, _ => new ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>());

            ConcurrentDictionary<TModId, TValue> byModId =
                byType.GetOrAdd(type, _ => new ConcurrentDictionary<TModId, TValue>());

            byModId[modId] = value;

            // Invalidate cache so GetValue recomputes on next read
            InvalidateCache(key);
        }
    }

    /// <summary>Get the computed attribute value.</summary>
    /// <remarks>Value = base × (1 + percentBonus) + flatBonus</remarks>
    /// <param name="key">Attribute key.</param>
    /// <returns>Computed value, or <c>TValue.Zero</c> if the key has no base modifiers.</returns>
    public TValue GetValue(TKey key)
    {
        lock (GetLock(key))
        {
            // Cache hit: return cached value without recomputing
            // The ? annotation acknowledges [MaybeNullWhen(false)] from TryGetValue.
            // For struct TValue, ? is a nullable annotation (not Nullable<T>),
            // and the compiler treats the returned value as non-null when true.
            if (_valueCache.TryGetValue(key, out TValue? cached))
                return cached;

            // Snapshot generation before reading modifier state, so we can detect
            // concurrent global mutations (RemoveModifier(TModId), Clear) that use a
            // different lock and would otherwise race with our cache write.
            long genBefore = Interlocked.Read(ref _globalGen);

            if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType))
                return CacheOrSkip(key, TValue.Zero, genBefore);

            if (!byType.TryGetValue(ModifierType.BaseValue, out ConcurrentDictionary<TModId, TValue>? byModId) || byModId.IsEmpty)
                return CacheOrSkip(key, TValue.Zero, genBefore);

            TValue baseValue = Sum(byModId);

            byType.TryGetValue(ModifierType.PercentBonus, out ConcurrentDictionary<TModId, TValue>? percentMods);
            TValue percentBonus = percentMods is not null ? Sum(percentMods) : TValue.Zero;

            byType.TryGetValue(ModifierType.FlatBonus, out ConcurrentDictionary<TModId, TValue>? flatMods);
            TValue flatBonus = flatMods is not null ? Sum(flatMods) : TValue.Zero;

            TValue result = baseValue * (TValue.One + percentBonus) + flatBonus;

            // Only cache if no global mutation occurred while we were reading and computing.
            // Without this check, a concurrent RemoveModifier(TModId) or Clear() could clear
            // the cache and remove modifiers, then we'd resurrect a stale/inconsistent entry.
            return CacheOrSkip(key, result, genBefore);
        }
    }

    /// <summary>Cache the computed value if no global mutation interrupted the computation,
    /// otherwise skip caching to avoid permanently storing a stale or inconsistent value.
    /// In either case, return the computed value so the caller gets an answer immediately.</summary>
    private TValue CacheOrSkip(TKey key, TValue value, long genBefore)
    {
        if (Interlocked.Read(ref _globalGen) == genBefore)
            _valueCache[key] = value;
        return value;
    }

    /// <summary>Remove a specific modifier by key, type, and id.</summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="type">Modifier type.</param>
    /// <param name="modId">Modifier identifier.</param>
    /// <returns><c>true</c> if the modifier was found and removed.</returns>
    public bool RemoveModifier(TKey key, ModifierType type, TModId modId)
    {
        lock (GetLock(key))
        {
            if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType))
                return false;
            if (!byType.TryGetValue(type, out ConcurrentDictionary<TModId, TValue>? byModId))
                return false;

            bool removed = byModId.TryRemove(modId, out _);
            if (removed) InvalidateCache(key);
            return removed;
        }
    }

    /// <summary>Remove a modifier by key and id across all modifier types.</summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="modId">Modifier identifier.</param>
    /// <returns><c>true</c> if any modifier with the given id was removed.</returns>
    public bool RemoveModifier(TKey key, TModId modId)
    {
        lock (GetLock(key))
        {
            if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType))
                return false;
            List<bool> results = byType.Select(x => x.Value.TryRemove(modId, out _)).ToList();
            bool anyRemoved = results.Any(x => x);
            if (anyRemoved) InvalidateCache(key);
            return anyRemoved;
        }
    }

    /// <summary>Remove all modifiers matching the given id across all keys and types.</summary>
    /// <param name="modId">Modifier identifier.</param>
    /// <returns><c>true</c> if any modifier was removed.</returns>
    public bool RemoveModifier(TModId modId)
    {
        lock (_globalLock)
        {
            List<bool> results = _modifiers.Values
                .SelectMany(byType => byType.Values)
                .Select(byModId => byModId.TryRemove(modId, out _))
                .ToList();
            bool anyRemoved = results.Any(x => x);
            if (anyRemoved)
            {
                _valueCache.Clear();
                // Signal concurrent readers that a global mutation occurred so they
                // do not cache stale values computed under a per-key lock.
                Interlocked.Increment(ref _globalGen);
            }
            return anyRemoved;
        }
    }

    /// <summary>Remove all modifiers for the given attribute key.</summary>
    /// <param name="key">Attribute key.</param>
    public void RemoveAllModifiers(TKey key)
    {
        lock (GetLock(key))
        {
            if (_modifiers.TryRemove(key, out _))
                InvalidateCache(key);
        }
    }

    /// <summary>Remove all modifiers for all attribute keys.</summary>
    public void Clear()
    {
        lock (_globalLock)
        {
            _modifiers.Clear();
            _valueCache.Clear();
            _keyLocks.Clear();  // Prevent unbounded growth of lock entries for abandoned keys
            Interlocked.Increment(ref _globalGen);
        }
    }

    private static TValue Sum(ConcurrentDictionary<TModId, TValue> dict)
    {
        TValue sum = TValue.Zero;
        foreach (TValue val in dict.Values)
        {
            sum += val;
        }
        return sum;
    }

    /// <summary>Return a JSON snapshot of all modifiers.</summary>
    public override string ToString()
    {
        lock (_globalLock)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append($"{GetType().Name}( ");
            stringBuilder.Append(JsonSerializer.Serialize(_modifiers));
            stringBuilder.Append(" )");
            return stringBuilder.ToString();
        }
    }
}
