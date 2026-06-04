using System.Collections.Concurrent;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace GameAttr;

public class Attr<TKey, TModId, TValue>
    where TKey : notnull
    where TModId : notnull
    where TValue : INumber<TValue>
{
    private readonly ConcurrentDictionary<TKey, ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>> _modifiers = new();

    public void SetModifier(TKey key, ModifierType type, TModId modId, TValue value)
    {
        ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>> byType =
            _modifiers.GetOrAdd(key, _ => new ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>());

        ConcurrentDictionary<TModId, TValue> byModId =
            byType.GetOrAdd(type, _ => new ConcurrentDictionary<TModId, TValue>());

        byModId[modId] = value;
    }

    /// <summary>
    /// Get Attr Value
    /// <remarks>Value = base * (TValue.One + percent) + flat</remarks>
    /// </summary>
    /// <param name="key">Attr Key which typed TKey</param>
    /// <returns></returns>
    public TValue GetValue(TKey key)
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

    public bool RemoveModifier(TKey key, ModifierType type, TModId modId)
    {
        if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType))
            return false;
        if (!byType.TryGetValue(type, out ConcurrentDictionary<TModId, TValue>? byModId))
            return false;
        return byModId.TryRemove(modId, out _);
    }

    public bool RemoveModifier(TKey key, TModId modId)
    {
        if (!_modifiers.TryGetValue(key, out ConcurrentDictionary<ModifierType, ConcurrentDictionary<TModId, TValue>>? byType))
            return false;
        // Remove all mod id by modId for any attributes
        return byType.Select(x => x.Value.TryRemove(modId, out _))
            .Any(x => x);
    }

    public void RemoveAllModifiers(TKey key)
    {
        _modifiers.TryRemove(key, out _);
    }

    public void Clear()
    {
        _modifiers.Clear();
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

    public override string ToString()
    {
        StringBuilder stringBuilder = new();
        stringBuilder.Append($"{GetType().Name}( ");
        stringBuilder.Append(JsonSerializer.Serialize(_modifiers));
        stringBuilder.Append(" )");
        return stringBuilder.ToString();
    }
}
