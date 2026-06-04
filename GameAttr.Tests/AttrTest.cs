using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameAttr.Tests;

[TestClass]
[TestSubject(typeof(Attr<,,>))]
public class AttrTest
{
    #region Base Value

    [TestMethod]
    public void GetValue_WithSingleBaseValue_ReturnsThatValue()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        Assert.AreEqual(100, attr.GetValue("atk"));
    }

    [TestMethod]
    public void GetValue_WithMultipleBaseValues_SumsCorrectly()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base1", 100);
        attr.SetModifier("hp", ModifierType.BaseValue, "base2", 200);
        Assert.AreEqual(300, attr.GetValue("hp"));
    }

    [TestMethod]
    public void GetValue_KeyDoesNotExist_ReturnsZero()
    {
        Attr<string, string, float> attr = new();
        Assert.AreEqual(0, attr.GetValue("nonexistent"));
    }

    [TestMethod]
    public void GetValue_KeyWithNoBaseValue_ReturnsZero()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.PercentBonus, "buff", 0.5f);
        Assert.AreEqual(0, attr.GetValue("atk"));
    }

    #endregion

    #region Percent Bonus

    [TestMethod]
    public void GetValue_WithPercentBonus_ComputesCorrectly()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 1000);
        attr.SetModifier("atk", ModifierType.PercentBonus, "buff", 0.3f);
        Assert.AreEqual(1300, attr.GetValue("atk"));
    }

    [TestMethod]
    public void GetValue_WithPositiveAndNegativePercentBonuses_StacksCorrectly()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 1000);
        attr.SetModifier("atk", ModifierType.PercentBonus, "buff1", 0.3f);
        attr.SetModifier("atk", ModifierType.PercentBonus, "debuff", -0.15f);
        Assert.AreEqual(1150, attr.GetValue("atk"));
    }

    [TestMethod]
    public void GetValue_WithMultiplePercentBonuses_StacksAdditively()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.PercentBonus, "buff1", 0.1f);
        attr.SetModifier("atk", ModifierType.PercentBonus, "buff2", 0.2f);
        attr.SetModifier("atk", ModifierType.PercentBonus, "buff3", 0.3f);
        // 100 * (1 + 0.1 + 0.2 + 0.3) = 160
        Assert.AreEqual(160, attr.GetValue("atk"));
    }

    #endregion

    #region Flat Bonus

    [TestMethod]
    public void GetValue_WithFlatBonus_AddsToBaseValue()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.FlatBonus, "bonus", 50);
        Assert.AreEqual(150, attr.GetValue("atk"));
    }

    [TestMethod]
    public void GetValue_WithMultipleFlatBonuses_SumsAdditively()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.FlatBonus, "bonus1", 30);
        attr.SetModifier("atk", ModifierType.FlatBonus, "bonus2", 20);
        Assert.AreEqual(150, attr.GetValue("atk"));
    }

    #endregion

    #region Combined Modifiers

    [TestMethod]
    public void GetValue_WithAllModifierTypes_AppliesPercentThenFlat()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.PercentBonus, "buff", 0.2f);
        attr.SetModifier("atk", ModifierType.FlatBonus, "bonus", 30);
        // 100 * (1 + 0.2) + 30 = 150
        Assert.AreEqual(150, attr.GetValue("atk"));
    }

    #endregion

    #region Remove Single Modifier (by key + type + modId)

    [TestMethod]
    public void RemoveModifier_ByKeyTypeAndModId_RemovesModifierAndUpdatesValue()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("hp", ModifierType.BaseValue, "extra", 100);
        Assert.AreEqual(200, attr.GetValue("hp"));

        Assert.IsTrue(attr.RemoveModifier("hp", ModifierType.BaseValue, "extra"));
        Assert.AreEqual(100, attr.GetValue("hp"));
    }

    [TestMethod]
    public void RemoveModifier_ByKeyTypeAndModId_NotExist_ReturnsFalse()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        Assert.IsFalse(attr.RemoveModifier("hp", ModifierType.BaseValue, "nonexistent"));
    }

    [TestMethod]
    public void RemoveModifier_ByKeyTypeAndModId_KeyNotExist_ReturnsFalse()
    {
        Attr<string, string, float> attr = new();
        Assert.IsFalse(attr.RemoveModifier("hp", ModifierType.BaseValue, "mod1"));
    }

    [TestMethod]
    public void RemoveModifier_ByKeyTypeAndModId_TypeNotExist_ReturnsFalse()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        Assert.IsFalse(attr.RemoveModifier("hp", ModifierType.PercentBonus, "mod1"));
    }

    #endregion

    #region Remove Modifiers by Key + ModId (across types)

    [TestMethod]
    public void RemoveModifier_ByKeyAndModId_RemovesFromAllTypes()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "mod1", 100);
        attr.SetModifier("hp", ModifierType.PercentBonus, "mod1", 0.5f);
        Assert.AreEqual(150, attr.GetValue("hp"));

        Assert.IsTrue(attr.RemoveModifier("hp", "mod1"));
        Assert.AreEqual(0, attr.GetValue("hp"));
    }

    [TestMethod]
    public void RemoveModifier_ByKeyAndModId_NotExist_ReturnsFalse()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        Assert.IsFalse(attr.RemoveModifier("hp", "nonexistent"));
    }

    [TestMethod]
    public void RemoveModifier_ByKeyAndModId_KeyNotExist_ReturnsFalse()
    {
        Attr<string, string, float> attr = new();
        Assert.IsFalse(attr.RemoveModifier("hp", "mod1"));
    }

    [TestMethod]
    public void RemoveModifier_ByKeyAndModId_OnlyRemovesSpecifiedKey()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "shared", 100);
        attr.SetModifier("atk", ModifierType.BaseValue, "shared", 200);
        Assert.IsTrue(attr.RemoveModifier("hp", "shared"));
        Assert.AreEqual(0, attr.GetValue("hp"));
        Assert.AreEqual(200, attr.GetValue("atk"));
    }

    #endregion

    #region Remove Modifier by ModId Only (global)

    [TestMethod]
    public void RemoveModifier_ByModIdOnly_RemovesFromAllKeys()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "shared", 100);
        attr.SetModifier("atk", ModifierType.BaseValue, "shared", 200);
        Assert.AreEqual(100, attr.GetValue("hp"));
        Assert.AreEqual(200, attr.GetValue("atk"));

        Assert.IsTrue(attr.RemoveModifier("shared"));
        Assert.AreEqual(0, attr.GetValue("hp"));
        Assert.AreEqual(0, attr.GetValue("atk"));
    }

    [TestMethod]
    public void RemoveModifier_ByModIdOnly_NotExist_ReturnsFalse()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        Assert.IsFalse(attr.RemoveModifier("nonexistent"));
    }

    [TestMethod]
    public void RemoveModifier_ByModIdOnly_EmptyAttr_ReturnsFalse()
    {
        Attr<string, string, float> attr = new();
        Assert.IsFalse(attr.RemoveModifier("any"));
    }

    #endregion

    #region Remove All Modifiers for Key

    [TestMethod]
    public void RemoveAllModifiers_RemovesAllModifiersForSpecifiedKey()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("hp", ModifierType.PercentBonus, "buff", 0.5f);
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);
        attr.RemoveAllModifiers("hp");
        Assert.AreEqual(0, attr.GetValue("hp"));
        Assert.AreEqual(200, attr.GetValue("atk"));
    }

    [TestMethod]
    public void RemoveAllModifiers_KeyNotExist_DoesNothing()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        attr.RemoveAllModifiers("nonexistent");
        Assert.AreEqual(100, attr.GetValue("hp"));
    }

    #endregion

    #region Clear All

    [TestMethod]
    public void Clear_RemovesAllModifiers()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);
        attr.Clear();
        Assert.AreEqual(0, attr.GetValue("hp"));
        Assert.AreEqual(0, attr.GetValue("atk"));
    }

    [TestMethod]
    public void Clear_EmptyAttr_DoesNotThrow()
    {
        Attr<string, string, float> attr = new();
        attr.Clear();
        Assert.AreEqual(0, attr.GetValue("any"));
    }

    #endregion

    #region Enum Keys

    private enum Attrs
    {
        HP,
        DEF
    }

    [TestMethod]
    public void GetValue_WithEnumKey_ReturnsCorrectValue()
    {
        Attr<Attrs, string, float> attr = new();
        attr.SetModifier(Attrs.HP, ModifierType.BaseValue, "base", 100);
        Assert.AreEqual(100, attr.GetValue(Attrs.HP));
    }

    [TestMethod]
    public void GetValue_WithEnumKey_UnsetEnum_ReturnsZero()
    {
        Attr<Attrs, string, float> attr = new();
        attr.SetModifier(Attrs.HP, ModifierType.BaseValue, "base", 100);
        Assert.AreEqual(0, attr.GetValue(Attrs.DEF));
    }

    #endregion

    #region Modifier Overwrite

    [TestMethod]
    public void SetModifier_SameModId_OverwritesValue()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);
        Assert.AreEqual(200, attr.GetValue("atk"));
    }

    #endregion

    #region String Representation

    [TestMethod]
    public void ToString_ContainsModifierValues()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        string result = attr.ToString();
        Assert.IsTrue(result.Contains("100"));
        Assert.IsTrue(result.EndsWith(")"));
    }

    #endregion
}
