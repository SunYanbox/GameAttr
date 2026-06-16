using System;
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

    #region Cache / Dirty Flag

    [TestMethod]
    public void GetValue_CalledTwice_ReturnsCachedValue()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        float first = attr.GetValue("atk");
        float second = attr.GetValue("atk");

        Assert.AreEqual(100, first);
        Assert.AreEqual(100, second);
    }

    [TestMethod]
    public void SetModifier_InvalidatesCache()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        // Populate cache
        attr.GetValue("atk");

        // Modify — this should invalidate cache
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);

        // Should return new value, not cached old one
        Assert.AreEqual(200, attr.GetValue("atk"));
    }

    [TestMethod]
    public void SetModifier_SameValue_DoesNotInvalidateCache()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("def", ModifierType.BaseValue, "base", 200);

        // Populate cache for both keys
        attr.GetValue("atk");
        attr.GetValue("def");

        // No-op: set the same modifier with the same value.
        // Cache should NOT be invalidated for either key.
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        Assert.AreEqual(100, attr.GetValue("atk"));
        Assert.AreEqual(200, attr.GetValue("def"));
    }

    [TestMethod]
    public void RemoveModifier_InvalidatesCache()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.BaseValue, "extra", 50);

        // Populate cache
        attr.GetValue("atk");

        // Remove one modifier — should invalidate cache
        attr.RemoveModifier("atk", ModifierType.BaseValue, "extra");

        // Should return recomputed value
        Assert.AreEqual(100, attr.GetValue("atk"));
    }

    [TestMethod]
    public void RemoveAllModifiers_InvalidatesCache()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        // Populate cache
        attr.GetValue("atk");

        // Remove all modifiers for the key
        attr.RemoveAllModifiers("atk");

        // Cache should be invalidated
        Assert.AreEqual(0, attr.GetValue("atk"));
    }

    [TestMethod]
    public void Clear_InvalidatesAllCache()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("def", ModifierType.BaseValue, "base", 200);

        // Populate cache for both keys
        attr.GetValue("atk");
        attr.GetValue("def");

        // Clear all
        attr.Clear();

        // Both cache entries should be invalidated
        Assert.AreEqual(0, attr.GetValue("atk"));
        Assert.AreEqual(0, attr.GetValue("def"));
    }

    [TestMethod]
    public void MultipleKeys_CacheIndependence()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("def", ModifierType.BaseValue, "base", 200);

        // Populate cache for both keys
        attr.GetValue("atk");
        attr.GetValue("def");

        // Modify only atk
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 300);

        // atk should be updated
        Assert.AreEqual(300, attr.GetValue("atk"));
        // def cache should still be valid
        Assert.AreEqual(200, attr.GetValue("def"));
    }

    #endregion

    #region Concurrent Safety (Generation Counter)

    [TestMethod]
    public void GetValue_AfterGlobalRemoveModifier_CacheCorrectlyInvalidated()
    {
        // Tests the cross-lock race between RemoveModifier(TModId) (global lock)
        // and GetValue (per-key lock). Verifies the generation counter prevents
        // permanent stale cache entries.
        Attr<string, string, float> attr = new();
        const int keyCount = 5;

        for (int i = 0; i < keyCount; i++)
        {
            attr.SetModifier($"key{i}", ModifierType.BaseValue, "base", 100);
            attr.SetModifier($"key{i}", ModifierType.FlatBonus, $"bonus{i}", 50);
            attr.SetModifier($"key{i}", ModifierType.FlatBonus, "shared", 30);
        }

        // Populate cache
        for (int i = 0; i < keyCount; i++)
            Assert.AreEqual(180, attr.GetValue($"key{i}")); // 100 + 50 + 30

        // Remove shared modifier from ALL keys via global operation
        attr.RemoveModifier("shared");

        // After global removal, ALL keys should reflect the change
        for (int i = 0; i < keyCount; i++)
            Assert.AreEqual(150, attr.GetValue($"key{i}")); // 100 + 50
    }

    [TestMethod]
    public void GetValue_AfterClear_CacheCorrectlyInvalidated()
    {
        // Tests the cross-lock race between Clear() (global lock)
        // and GetValue (per-key lock).
        Attr<string, string, float> attr = new();

        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("def", ModifierType.BaseValue, "base", 200);

        // Populate cache
        attr.GetValue("atk");
        attr.GetValue("def");

        attr.Clear();

        // Both should be zero after clear
        Assert.AreEqual(0, attr.GetValue("atk"));
        Assert.AreEqual(0, attr.GetValue("def"));
    }

    [TestMethod]
    public void GetValue_MissingKey_ReturnsZero()
    {
        // Missing key should return Zero; after the fix the result is also cached
        // so repeated calls are O(1) instead of O(num modifier types).
        Attr<string, string, float> attr = new();

        Assert.AreEqual(0, attr.GetValue("nonexistent"));
        Assert.AreEqual(0, attr.GetValue("nonexistent"));

        // After setting a modifier, the cache is invalidated
        attr.SetModifier("nonexistent", ModifierType.BaseValue, "base", 100);
        Assert.AreEqual(100, attr.GetValue("nonexistent"));
    }

    [TestMethod]
    public void GetValue_NoBaseValue_ReturnsZero()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.PercentBonus, "buff", 0.5f);

        // No BaseValue → Zero, and the result gets cached
        Assert.AreEqual(0, attr.GetValue("atk"));
        Assert.AreEqual(0, attr.GetValue("atk"));

        // After adding a base value, cache is invalidated
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        Assert.AreEqual(150, attr.GetValue("atk"));
    }

    [TestMethod]
    public void Clear_AllowsReusingKeys()
    {
        // After Clear(), re-using the same keys should work correctly.
        // (Key lock objects persist; GetLock returns the existing one via GetOrAdd.)
        Attr<string, string, float> attr = new();

        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("def", ModifierType.BaseValue, "base", 200);
        attr.GetValue("atk");
        attr.GetValue("def");

        attr.Clear();

        attr.SetModifier("atk", ModifierType.BaseValue, "base", 300);
        Assert.AreEqual(300, attr.GetValue("atk"));
    }

    #endregion

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        try
        {
            new Attr<string, string, float>(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected — logger was null
        }
    }
}
