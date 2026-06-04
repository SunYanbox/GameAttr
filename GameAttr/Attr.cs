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
    private readonly Lock _globalLock = new();

    private object GetLock(TKey key) => _keyLocks.GetOrAdd(key, _ => new object());

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
            if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType))
                return TValue.Zero;

            if (!byType.TryGetValue(ModifierType.BaseValue, out ConcurrentDictionary<TModId, TValue>? byModId) || byModId.IsEmpty)
                return TValue.Zero;

            TValue baseValue = Sum(byModId);

            byType.TryGetValue(ModifierType.PercentBonus, out ConcurrentDictionary<TModId, TValue>? percentMods);
            TValue percentBonus = percentMods is not null ? Sum(percentMods) : TValue.Zero;

            byType.TryGetValue(ModifierType.FlatBonus, out ConcurrentDictionary<TModId, TValue>? flatMods);
            TValue flatBonus = flatMods is not null ? Sum(flatMods) : TValue.Zero;

            return baseValue * (TValue.One + percentBonus) + flatBonus;
        }
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
            return byModId.TryRemove(modId, out _);
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
            return results.Any(x => x);
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
            return results.Any(x => x);
        }
    }

    /// <summary>Remove all modifiers for the given attribute key.</summary>
    /// <param name="key">Attribute key.</param>
    public void RemoveAllModifiers(TKey key)
    {
        lock (GetLock(key))
        {
            _modifiers.TryRemove(key, out _);
        }
    }

    /// <summary>Remove all modifiers for all attribute keys.</summary>
    public void Clear()
    {
        lock (_globalLock)
        {
            _modifiers.Clear();
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
