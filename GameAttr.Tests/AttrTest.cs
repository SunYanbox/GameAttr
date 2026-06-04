using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameAttr.Tests;

[TestClass]
[TestSubject(typeof(Attr<,,>))]
public class AttrTest
{
    #region Test Modifiers

    [TestMethod]
    public void TestModifiersByString()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base value", 1000);
        Assert.AreEqual(1000, attr.GetValue("atk"),
            $"base value should be 1000, not {attr.GetValue("atk")}"
        );

        attr.SetModifier("atk", ModifierType.PercentBonus, "gain 1", 0.3f);
        Assert.AreEqual(1000 * 1.3f, attr.GetValue("atk"),
            $"final value should be 1300, not {attr.GetValue("atk")}");

        attr.SetModifier("atk", ModifierType.PercentBonus, "gain 2", -0.15f);
        Assert.AreEqual(1000 * (1.0f + 0.3f - 0.15f), attr.GetValue("atk"),
            $"final value should be 1150, not {attr.GetValue("atk")}");

        // Console.WriteLine(attr);
    }

    #endregion

    #region Test string type Key and string modId

    [TestMethod]
    public void TestBaseAttrAndNotExistAttrByString()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base value", 100);
        Assert.AreEqual(100, attr.GetValue("hp"),
            $"base value should be 100, not {attr.GetValue("hp")}"
            );
        attr.SetModifier("hp", ModifierType.BaseValue, "base value 1", 100);
        Assert.AreEqual(200, attr.GetValue("hp"),
            $"base value should be 200, not {attr.GetValue("hp")}"
        );
        Assert.AreEqual(0, attr.GetValue("not exist attr"),
            $"not exist attr should be 0, not {attr.GetValue("not exist attr")}"
        );
    }

    [TestMethod]
    public void TestRemoveModIdByString()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base value", 100);
        Assert.AreEqual(100, attr.GetValue("hp"),
            $"base value should be 100, not {attr.GetValue("hp")}"
        );

        attr.SetModifier("hp", ModifierType.BaseValue, "base value 1", 100);
        Assert.AreEqual(200, attr.GetValue("hp"),
            $"base value should be 200, not {attr.GetValue("hp")}"
        );

        Assert.IsTrue(attr.RemoveModifier("hp", "base value 1"), "remove modifier(base value 1) should return true");
        Assert.IsFalse(attr.RemoveModifier("hp", "base value 1"), "remove not exist modifier(base value 1) should return false");

        Assert.AreEqual(100, attr.GetValue("hp"),
            $"After remove modifier(base value 1) base value should be 100, not {attr.GetValue("hp")}"
        );
    }

    #endregion

    #region Test Enum

    private enum Attrs
    {
        HP,
        DEF
    }

    [TestMethod]
    public void TestBaseAttrByEnum()
    {
        Attr<Attrs, string, float> attr = new();
        attr.SetModifier(Attrs.HP, ModifierType.BaseValue, "base value", 100);
        Assert.AreEqual(100, attr.GetValue(Attrs.HP),
            $"base value should be 100, not {attr.GetValue(Attrs.HP)}"
        );
        attr.SetModifier(Attrs.HP, ModifierType.BaseValue, "base value 1", 100);
        Assert.AreEqual(200, attr.GetValue(Attrs.HP),
            $"base value should be 200, not {attr.GetValue(Attrs.HP)}"
        );

        Assert.AreEqual(0, attr.GetValue(Attrs.DEF),
            $"base value should be 0, not {attr.GetValue(Attrs.DEF)}"
        );
    }

    #endregion
}