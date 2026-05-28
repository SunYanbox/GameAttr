namespace GameAttr;

/// <summary>
/// Specifies the type of an attribute modifier, determining how it affects the final attribute value.
/// </summary>
public enum ModifierType
{
    /// <summary>
    /// The base value of the attribute, also used as the target for base value bonuses.
    /// Modifiers of this type serve as the foundation for other calculations.
    /// </summary>
    BaseValue,

    /// <summary>
    /// Percentage bonus calculated based on <see cref="BaseValue"/>.
    /// e.g. 0.1 increases the base value by 10 percent.
    /// </summary>
    PercentBonus,

    /// <summary>
    /// Flat numeric bonus added directly to the attribute value.
    /// e.g. +50 directly adds 50 points.
    /// </summary>
    FlatBonus
}