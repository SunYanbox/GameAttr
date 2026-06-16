namespace GameAttr;

/// <summary>Event arguments for <see cref="Attr{TKey, TModId, TValue}.AttributeChanged"/>.</summary>
/// <typeparam name="TKey">Attribute key type.</typeparam>
/// <typeparam name="TValue">Attribute value type.</typeparam>
public class AttrChangedEventArgs<TKey, TValue>
{
    /// <summary>The attribute key that changed.</summary>
    public TKey Key { get; }

    /// <summary>What kind of change occurred.</summary>
    public AttrChangeType ChangeType { get; }

    /// <summary>The recomputed value after the change.</summary>
    public TValue NewValue { get; }

    /// <summary>Create event arguments for an attribute change.</summary>
    public AttrChangedEventArgs(TKey key, AttrChangeType changeType, TValue newValue)
    {
        Key = key;
        ChangeType = changeType;
        NewValue = newValue;
    }
}
