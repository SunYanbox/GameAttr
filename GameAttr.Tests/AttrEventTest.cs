using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameAttr.Tests;

[TestClass]
[TestSubject(typeof(Attr<,,>))]
public class AttrEventTest
{
    #region SetModifier events

    [TestMethod]
    public void SetModifier_NewModifier_FiresEvent()
    {
        Attr<string, string, float> attr = new();
        AttrChangedEventArgs<string, float>? received = null;
        attr.AttributeChanged += args => received = args;

        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        Assert.IsNotNull(received);
        Assert.AreEqual("atk", received!.Key);
        Assert.AreEqual(AttrChangeType.SetModifier, received.ChangeType);
        Assert.AreEqual(100f, received.NewValue);
    }

    [TestMethod]
    public void SetModifier_OverwriteValue_FiresEventWithNewValue()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        AttrChangedEventArgs<string, float>? received = null;
        attr.AttributeChanged += args => received = args;

        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);

        Assert.IsNotNull(received);
        Assert.AreEqual(200f, received!.NewValue);
    }

    [TestMethod]
    public void SetModifier_SameValue_DoesNotFireEvent()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        int fireCount = 0;
        attr.AttributeChanged += _ => fireCount++;

        // Set same value again — should be a no-op
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        Assert.AreEqual(0, fireCount);
    }

    [TestMethod]
    public void SetModifier_EventFiresWithCorrectComputedValue()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("hp", ModifierType.PercentBonus, "buff", 0.5f);

        AttrChangedEventArgs<string, float>? received = null;
        attr.AttributeChanged += args => received = args;

        attr.SetModifier("hp", ModifierType.FlatBonus, "bonus", 30);

        Assert.IsNotNull(received);
        // 100 * (1 + 0.5) + 30 = 180
        Assert.AreEqual(180f, received!.NewValue);
    }

    #endregion

    #region RemoveModifier events

    [TestMethod]
    public void RemoveModifier_ByKeyTypeModId_FiresEvent()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("hp", ModifierType.BaseValue, "extra", 50);

        AttrChangedEventArgs<string, float>? received = null;
        attr.AttributeChanged += args => received = args;

        attr.RemoveModifier("hp", ModifierType.BaseValue, "extra");

        Assert.IsNotNull(received);
        Assert.AreEqual(AttrChangeType.RemoveModifier, received!.ChangeType);
        Assert.AreEqual(100f, received.NewValue); // only base remains
    }

    [TestMethod]
    public void RemoveModifier_ByKeyTypeModId_NotExist_DoesNotFireEvent()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);

        int fireCount = 0;
        attr.AttributeChanged += _ => fireCount++;

        attr.RemoveModifier("hp", ModifierType.BaseValue, "nonexistent");

        Assert.AreEqual(0, fireCount);
    }

    [TestMethod]
    public void RemoveModifier_ByKeyModId_FiresEvent()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "mod1", 100);
        attr.SetModifier("hp", ModifierType.PercentBonus, "mod1", 0.5f);

        AttrChangedEventArgs<string, float>? received = null;
        attr.AttributeChanged += args => received = args;

        attr.RemoveModifier("hp", "mod1");

        Assert.IsNotNull(received);
        Assert.AreEqual(AttrChangeType.RemoveModifier, received!.ChangeType);
        Assert.AreEqual(0f, received.NewValue);
    }

    [TestMethod]
    public void RemoveModifier_ByModIdOnly_FiresEventsForAllAffectedKeys()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "shared", 100);
        attr.SetModifier("atk", ModifierType.BaseValue, "shared", 200);

        List<AttrChangedEventArgs<string, float>> received = new();
        attr.AttributeChanged += args => received.Add(args);

        attr.RemoveModifier("shared");

        Assert.AreEqual(2, received.Count);
        Assert.IsTrue(received.Any(r => r.Key == "hp" && r.NewValue == 0f));
        Assert.IsTrue(received.Any(r => r.Key == "atk" && r.NewValue == 0f));
    }

    [TestMethod]
    public void RemoveModifier_ByModIdOnly_NotExist_DoesNotFireEvent()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);

        int fireCount = 0;
        attr.AttributeChanged += _ => fireCount++;

        attr.RemoveModifier("nonexistent");

        Assert.AreEqual(0, fireCount);
    }

    #endregion

    #region RemoveAllModifiers events

    [TestMethod]
    public void RemoveAllModifiers_FiresEvent()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);

        AttrChangedEventArgs<string, float>? received = null;
        attr.AttributeChanged += args => received = args;

        attr.RemoveAllModifiers("hp");

        Assert.IsNotNull(received);
        Assert.AreEqual(AttrChangeType.RemoveAll, received!.ChangeType);
        Assert.AreEqual(0f, received.NewValue);
    }

    [TestMethod]
    public void RemoveAllModifiers_KeyNotExist_DoesNotFireEvent()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);

        int fireCount = 0;
        attr.AttributeChanged += _ => fireCount++;

        attr.RemoveAllModifiers("nonexistent");

        Assert.AreEqual(0, fireCount);
    }

    #endregion

    #region Clear events

    [TestMethod]
    public void Clear_FiresEventForAllAffectedKeys()
    {
        Attr<string, string, float> attr = new();
        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);

        List<AttrChangedEventArgs<string, float>> received = new();
        attr.AttributeChanged += args => received.Add(args);

        attr.Clear();

        Assert.AreEqual(2, received.Count);
        Assert.IsTrue(received.All(r => r.ChangeType == AttrChangeType.Clear));
        Assert.IsTrue(received.All(r => r.NewValue == 0f));
    }

    [TestMethod]
    public void Clear_EmptyAttr_DoesNotFireEvent()
    {
        Attr<string, string, float> attr = new();

        int fireCount = 0;
        attr.AttributeChanged += _ => fireCount++;

        attr.Clear();

        Assert.AreEqual(0, fireCount);
    }

    #endregion

    #region Multiple subscribers

    [TestMethod]
    public void SetModifier_MultipleSubscribers_AllReceiveEvent()
    {
        Attr<string, string, float> attr = new();
        int count1 = 0, count2 = 0;
        attr.AttributeChanged += _ => count1++;
        attr.AttributeChanged += _ => count2++;

        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        Assert.AreEqual(1, count1);
        Assert.AreEqual(1, count2);
    }

    #endregion

    #region Event unsubscription

    [TestMethod]
    public void AttributeChanged_Unsubscribed_DoesNotReceiveEvent()
    {
        Attr<string, string, float> attr = new();
        int fireCount = 0;
        Action<AttrChangedEventArgs<string, float>> handler = _ => fireCount++;
        attr.AttributeChanged += handler;

        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        Assert.AreEqual(1, fireCount);

        attr.AttributeChanged -= handler;
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);
        Assert.AreEqual(1, fireCount); // unchanged
    }

    #endregion

    #region Multiple keys events

    [TestMethod]
    public void SetModifier_MultipleKeys_EventsHaveCorrectKeyAndValue()
    {
        Attr<string, string, float> attr = new();
        List<AttrChangedEventArgs<string, float>> received = new();
        attr.AttributeChanged += args => received.Add(args);

        attr.SetModifier("hp", ModifierType.BaseValue, "base", 100);
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);
        attr.SetModifier("def", ModifierType.BaseValue, "base", 50);

        Assert.AreEqual(3, received.Count);
        Assert.AreEqual(100f, received[0].NewValue);
        Assert.AreEqual(200f, received[1].NewValue);
        Assert.AreEqual(50f, received[2].NewValue);
    }

    #endregion

    #region Event and cache interaction

    [TestMethod]
    public void SetModifier_FiresEvent_BeforeCacheIsRead()
    {
        // Events fire after mutation and after cache invalidation,
        // so GetValue during event handler returns the new value.
        Attr<string, string, float> attr = new();
        attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        float? valueFromEvent = null;
        attr.AttributeChanged += args =>
        {
            // GetValue inside event handler should return fresh value
            valueFromEvent = attr.GetValue(args.Key);
        };

        attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);

        Assert.AreEqual(200f, valueFromEvent);
    }

    #endregion
}
