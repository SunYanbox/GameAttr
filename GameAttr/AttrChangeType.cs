namespace GameAttr;

/// <summary>Specifies the type of change that occurred to an attribute's value.</summary>
public enum AttrChangeType
{
    /// <summary>A modifier was set or overwritten, causing the value to change.</summary>
    SetModifier,

    /// <summary>A single modifier was removed, causing the value to change.</summary>
    RemoveModifier,

    /// <summary>All modifiers for a key were removed.</summary>
    RemoveAll,

    /// <summary>All modifiers for all keys were cleared.</summary>
    Clear
}
