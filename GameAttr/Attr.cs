using System.Collections.Concurrent;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameAttr;

/// <summary>A generic attribute system for games.</summary>
public class Attr<TKey, TModId, TValue>
    where TKey : notnull
    where TModId : notnull
    where TValue : INumber<TValue>
{
    private readonly ConcurrentDictionary<TKey, ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>> _modifiers = new();
    private readonly ConcurrentDictionary<TKey, object> _keyLocks = new();
    // Stores computed values alongside the generation at time of computation.
    // On read, validates that the stored gen matches the current _globalGen,
    // so a concurrent global mutation (under a different lock) forces re-computation.
    // This eliminates the TOCTOU of a conditional write-then-check approach,
    // since the cache entry is always written and validated on the next read.
    private readonly ConcurrentDictionary<TKey, (TValue Value, long Gen)> _valueCache = new();
    private readonly Lock _globalLock = new();

    // Generation counter to detect concurrent global mutations (RemoveModifier(TModId), Clear).
    // Incremented under _globalLock; read under per-key lock so GetValue can detect
    // cross-lock races and avoid caching stale or inconsistent values.
    private long _globalGen;

    private readonly ILogger _logger;
    private Action<AttrChangedEventArgs<TKey, TValue>>? _onAttributeChanged;
    private readonly Lock _eventLock = new();

    /// <summary>Initializes a new instance with logging disabled (NullLogger).</summary>
    public Attr()
    {
        _logger = NullLogger<Attr<TKey, TModId, TValue>>.Instance;
    }

    /// <summary>Initializes a new instance with the specified logger.</summary>
    /// <param name="logger">Logger for recording diagnostic events (e.g., subscriber exceptions).</param>
    public Attr(ILogger<Attr<TKey, TModId, TValue>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private object GetLock(TKey key) => _keyLocks.GetOrAdd(key, _ => new object());

    private void InvalidateCache(TKey key) => _valueCache.TryRemove(key, out _);

    // ── Value computation ──────────────────────────────────────────────

    /// <summary>Compute the attribute value without checking cache. Assumes the per-key lock
    /// is held. Does NOT cache the result — callers must handle caching.</summary>
    private TValue ComputeValueLocked(TKey key)
    {
        if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType)
            || !byType.TryGetValue(ModifierType.BaseValue, out ConcurrentDictionary<TModId, TValue>? byModId) || byModId.IsEmpty)
            return TValue.Zero;

        TValue baseValue = Sum(byModId);

        byType.TryGetValue(ModifierType.PercentBonus, out ConcurrentDictionary<TModId, TValue>? percentMods);
        TValue percentBonus = percentMods is not null ? Sum(percentMods) : TValue.Zero;

        byType.TryGetValue(ModifierType.FlatBonus, out ConcurrentDictionary<TModId, TValue>? flatMods);
        TValue flatBonus = flatMods is not null ? Sum(flatMods) : TValue.Zero;

        return baseValue * (TValue.One + percentBonus) + flatBonus;
    }

    // ── Event ──────────────────────────────────────────────────────────

    /// <summary>Fires when an attribute value changes due to a modifier mutation.
    /// The event is raised outside per-key locks to avoid deadlocks.</summary>
    public event Action<AttrChangedEventArgs<TKey, TValue>>? AttributeChanged
    {
        add
        {
            lock (_eventLock)
            {
                _onAttributeChanged += value;
            }
        }
        remove
        {
            lock (_eventLock)
            {
                _onAttributeChanged -= value;
            }
        }
    }

    /// <summary>Raise the <see cref="AttributeChanged"/> event if there are subscribers.
    /// Each subscriber is invoked individually so that one subscriber's exception does not
    /// prevent subsequent subscribers from receiving the event.</summary>
    private void RaiseAttributeChanged(TKey key, AttrChangeType changeType, TValue newValue)
    {
        Action<AttrChangedEventArgs<TKey, TValue>>? handler;
        lock (_eventLock)
        {
            handler = _onAttributeChanged;
        }

        if (handler is null)
            return;

        var args = new AttrChangedEventArgs<TKey, TValue>(key, changeType, newValue);
        foreach (Delegate subscriber in handler.GetInvocationList())
        {
            try
            {
                ((Action<AttrChangedEventArgs<TKey, TValue>>)subscriber)(args);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Subscriber threw an exception while handling {ChangeType} event for key {Key}",
                    changeType, key);
            }
        }
    }

    // ── Set modifier ───────────────────────────────────────────────────

    /// <summary>Set or overwrite a modifier for the given attribute key.</summary>
    /// <remarks>Cache is only invalidated when the value actually changes — setting the same
    /// value again is a no-op that avoids an unnecessary cache miss on the next read.</remarks>
    /// <param name="key">Attribute key.</param>
    /// <param name="type">Modifier type (BaseValue, PercentBonus, FlatBonus).</param>
    /// <param name="modId">Unique modifier identifier.</param>
    /// <param name="value">Modifier value.</param>
    public void SetModifier(TKey key, ModifierType type, TModId modId, TValue value)
    {
        AttrChangedEventArgs<TKey, TValue>? eventArgs = null;

        lock (GetLock(key))
        {
            ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>> byType =
                _modifiers.GetOrAdd(key, _ => new ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>());

            ConcurrentDictionary<TModId, TValue> byModId =
                byType.GetOrAdd(type, _ => new ConcurrentDictionary<TModId, TValue>());

            // Only invalidate cache when the value actually changes.
            // TryAdd succeeds → new modifier was added.
            // TryAdd fails → modifier exists; check if value differs before overwriting.
            if (byModId.TryAdd(modId, value))
            {
                InvalidateCache(key);
                if (_onAttributeChanged is not null)
                    eventArgs = new(key, AttrChangeType.SetModifier, ComputeValueLocked(key));
            }
            else if (!EqualityComparer<TValue>.Default.Equals(byModId[modId], value))
            {
                byModId[modId] = value;
                InvalidateCache(key);
                if (_onAttributeChanged is not null)
                    eventArgs = new(key, AttrChangeType.SetModifier, ComputeValueLocked(key));
            }
        }

        if (eventArgs is not null)
            RaiseAttributeChanged(eventArgs.Key, eventArgs.ChangeType, eventArgs.NewValue);
    }

    // ── Get value ──────────────────────────────────────────────────────

    /// <summary>Get the computed attribute value.</summary>
    /// <remarks>Value = base × (1 + percentBonus) + flatBonus</remarks>
    /// <param name="key">Attribute key.</param>
    /// <returns>Computed value, or <c>TValue.Zero</c> if the key has no base modifiers.</returns>
    public TValue GetValue(TKey key)
    {
        lock (GetLock(key))
        {
            // Cache hit: return cached value only if no global mutation has occurred
            // since it was computed. Each cache entry stores the generation at time
            // of computation, so a concurrent RemoveModifier(TModId) or Clear()
            // (which use a different lock and increment _globalGen) is detected here
            // and forces re-computation rather than returning stale data.
            if (_valueCache.TryGetValue(key, out var cached) && cached.Gen == Interlocked.Read(ref _globalGen))
                return cached.Value;

            // Snapshot generation before reading modifier state, so we can detect
            // concurrent global mutations (RemoveModifier(TModId), Clear) that use a
            // different lock and would otherwise race with our cache write.
            long genBefore = Interlocked.Read(ref _globalGen);

            TValue result = ComputeValueLocked(key);

            // Store the result with the genBefore stamp. On the next read for this key,
            // the entry is only returned if cached.Gen still matches _globalGen — detecting
            // any concurrent global mutation that could have made this result stale.
            return CacheValue(key, result, genBefore);
        }
    }

    /// <summary>Cache the computed value alongside the generation at computation time.
    /// Always stores the entry — stale detection happens on the next read by comparing
    /// the stored gen against the current _globalGen. This avoids the TOCTOU race of
    /// a read-then-write check pattern while preserving cross-lock safety.
    /// The computed value is always returned immediately.</summary>
    private TValue CacheValue(TKey key, TValue value, long genBefore)
    {
        _valueCache[key] = (value, genBefore);
        return value;
    }

    // ── Remove modifier (key + type + modId) ───────────────────────────

    /// <summary>Remove a specific modifier by key, type, and id.</summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="type">Modifier type.</param>
    /// <param name="modId">Modifier identifier.</param>
    /// <returns><c>true</c> if the modifier was found and removed.</returns>
    public bool RemoveModifier(TKey key, ModifierType type, TModId modId)
    {
        AttrChangedEventArgs<TKey, TValue>? eventArgs = null;
        bool removed;

        lock (GetLock(key))
        {
            if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType))
                return false;
            if (!byType.TryGetValue(type, out ConcurrentDictionary<TModId, TValue>? byModId))
                return false;

            removed = byModId.TryRemove(modId, out _);
            if (removed)
            {
                InvalidateCache(key);
                if (_onAttributeChanged is not null)
                    eventArgs = new AttrChangedEventArgs<TKey, TValue>(key, AttrChangeType.RemoveModifier, ComputeValueLocked(key));
            }
        }

        if (eventArgs is not null)
            RaiseAttributeChanged(eventArgs.Key, eventArgs.ChangeType, eventArgs.NewValue);
        return removed;
    }

    /// <summary>Remove a modifier by key and id across all modifier types.</summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="modId">Modifier identifier.</param>
    /// <returns><c>true</c> if any modifier with the given id was removed.</returns>
    public bool RemoveModifier(TKey key, TModId modId)
    {
        AttrChangedEventArgs<TKey, TValue>? eventArgs = null;
        bool anyRemoved;

        lock (GetLock(key))
        {
            if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType))
                return false;
            List<bool> results = byType.Select(x => x.Value.TryRemove(modId, out _)).ToList();
            anyRemoved = results.Any(x => x);
            if (anyRemoved)
            {
                InvalidateCache(key);
                if (_onAttributeChanged is not null)
                    eventArgs = new(key, AttrChangeType.RemoveModifier, ComputeValueLocked(key));
            }
        }

        if (eventArgs is not null)
            RaiseAttributeChanged(eventArgs.Key, eventArgs.ChangeType, eventArgs.NewValue);
        return anyRemoved;
    }

    /// <summary>Remove all modifiers matching the given id across all keys and types.</summary>
    /// <param name="modId">Modifier identifier.</param>
    /// <returns><c>true</c> if any modifier was removed.</returns>
    public bool RemoveModifier(TModId modId)
    {
        List<TKey> affected = new();
        bool anyRemoved;

        lock (_globalLock)
        {
            anyRemoved = false;
            foreach (KeyValuePair<TKey, ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>> kvp in _modifiers)
            {
                bool keyChanged = false;
                foreach (ConcurrentDictionary<TModId, TValue> byModId in kvp.Value.Values)
                {
                    if (byModId.TryRemove(modId, out _))
                        keyChanged = true;
                }
                if (keyChanged)
                {
                    anyRemoved = true;
                    affected.Add(kvp.Key);
                }
            }

            if (anyRemoved)
            {
                _valueCache.Clear();
                Interlocked.Increment(ref _globalGen);
            }
        }

        // Fire events outside the global lock. Recompute each affected key's value
        // under its per-key lock (the modifier state is consistent by now).
        foreach (TKey key in affected)
        {
            TValue newValue;
            lock (GetLock(key))
            {
                newValue = ComputeValueLocked(key);
            }
            RaiseAttributeChanged(key, AttrChangeType.RemoveModifier, newValue);
        }

        return anyRemoved;
    }

    /// <summary>Remove all modifiers for the given attribute key.</summary>
    /// <param name="key">Attribute key.</param>
    public void RemoveAllModifiers(TKey key)
    {
        AttrChangedEventArgs<TKey, TValue>? eventArgs = null;

        lock (GetLock(key))
        {
            if (_modifiers.TryRemove(key, out _))
            {
                InvalidateCache(key);
                if (_onAttributeChanged is not null)
                    eventArgs = new(key, AttrChangeType.RemoveAll, TValue.Zero);
            }
        }

        if (eventArgs is not null)
            RaiseAttributeChanged(eventArgs.Key, eventArgs.ChangeType, eventArgs.NewValue);
    }

    /// <summary>Remove all modifiers for all attribute keys.</summary>
    public void Clear()
    {
        List<TKey> affectedKeys = new();

        lock (_globalLock)
        {
            affectedKeys.AddRange(_modifiers.Keys);
            _modifiers.Clear();
            _valueCache.Clear();
            Interlocked.Increment(ref _globalGen);
        }

        foreach (TKey key in affectedKeys)
            RaiseAttributeChanged(key, AttrChangeType.Clear, TValue.Zero);
    }

    // ── Helpers ────────────────────────────────────────────────────────

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
