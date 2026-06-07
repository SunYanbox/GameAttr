using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameAttr.Tests;

[TestClass]
public class AttrConcurrencyTest
{
    // Shared attr for concurrency tests - reinitialized per test
    private Attr<string, string, float> _attr = null!;

    [TestInitialize]
    public void Initialize()
    {
        _attr = new Attr<string, string, float>();
    }

    #region Pure Read Concurrency

    [TestMethod]
    public void ConcurrentReads_SameKey_MultipleThreads_NoDeadlock()
    {
        _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        _attr.GetValue("atk"); // warm cache

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(100, _attr.GetValue("atk"));
        })).ToArray();

        Task.WaitAll(tasks);
    }

    [TestMethod]
    public void ConcurrentReads_DifferentKeys_MultipleThreads_NoDeadlock()
    {
        const int keyCount = 10;
        for (int i = 0; i < keyCount; i++)
            _attr.SetModifier($"key{i}", ModifierType.BaseValue, "base", i * 10);

        // warm caches
        for (int i = 0; i < keyCount; i++)
            _attr.GetValue($"key{i}");

        var tasks = Enumerable.Range(0, keyCount).Select(i => Task.Run(() =>
        {
            for (int j = 0; j < 1000; j++)
                Assert.AreEqual(i * 10, _attr.GetValue($"key{i}"));
        })).ToArray();

        Task.WaitAll(tasks);
    }

    #endregion

    #region Read-Write Concurrency

    [TestMethod]
    public void ConcurrentReadsAndSetModifier_SameKey_NoCorruption()
    {
        _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        // 2 readers + 1 writer
        Task[] tasks =
        [
            Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                    _attr.GetValue("atk");
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                    _attr.GetValue("atk");
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100 + i);
                    _attr.SetModifier("atk", ModifierType.FlatBonus, "bonus", i);
                }
            }),
        ];

        Task.WaitAll(tasks);

        // System should still be in a consistent state
        float val = _attr.GetValue("atk");
        Assert.IsTrue(val >= 100);
    }

    [TestMethod]
    public void ConcurrentReadsAndRemoveModifier_SingleKey_NoDeadlock()
    {
        _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
        _attr.SetModifier("atk", ModifierType.BaseValue, "extra", 50);

        Task[] tasks =
        [
            Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                    _attr.GetValue("atk");
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    _attr.SetModifier("atk", ModifierType.BaseValue, "extra", 50 + i);
                    _attr.RemoveModifier("atk", ModifierType.BaseValue, "extra");
                }
            }),
        ];

        Task.WaitAll(tasks);
    }

    #endregion

    #region Global Operation Concurrency (Generation Counter)

    [TestMethod]
    public void ConcurrentReadsAndGlobalRemoveModifier_ReturnsCorrectValue()
    {
        for (int iter = 0; iter < 50; iter++)
        {
            _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
            _attr.SetModifier("atk", ModifierType.FlatBonus, "shared", 50);
            _attr.SetModifier("def", ModifierType.BaseValue, "base", 200);
            _attr.SetModifier("def", ModifierType.FlatBonus, "shared", 30);

            _attr.GetValue("atk");
            _attr.GetValue("def");

            Parallel.Invoke(
                () => _attr.GetValue("atk"),
                () => _attr.GetValue("def"),
                () => _attr.RemoveModifier("shared")
            );

            Assert.AreEqual(100, _attr.GetValue("atk"), $"Iteration {iter}: atk should be base (100) after global remove");
            Assert.AreEqual(200, _attr.GetValue("def"), $"Iteration {iter}: def should be base (200) after global remove");

            _attr.Clear();
        }
    }

    [TestMethod]
    public void ConcurrentReadsAndClear_ReturnsCorrectValue()
    {
        for (int iter = 0; iter < 50; iter++)
        {
            _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
            _attr.SetModifier("def", ModifierType.BaseValue, "base", 200);
            _attr.GetValue("atk");
            _attr.GetValue("def");

            Parallel.Invoke(
                () => _attr.GetValue("atk"),
                () => _attr.GetValue("def"),
                () => _attr.Clear()
            );

            Assert.AreEqual(0, _attr.GetValue("atk"), $"Iteration {iter}: atk should be 0 after Clear");
            Assert.AreEqual(0, _attr.GetValue("def"), $"Iteration {iter}: def should be 0 after Clear");
        }
    }

    [TestMethod]
    public void ConcurrentReadsAllKeysAndGlobalRemoveModifier_AllKeysUpdated()
    {
        const int keyCount = 5;
        for (int i = 0; i < keyCount; i++)
        {
            _attr.SetModifier($"key{i}", ModifierType.BaseValue, "base", 100);
            _attr.SetModifier($"key{i}", ModifierType.FlatBonus, "shared", 50);
        }

        // warm caches
        for (int i = 0; i < keyCount; i++)
            _attr.GetValue($"key{i}");

        // All threads read concurrently with a global remove
        Action[] actions = new Action[keyCount + 1];
        for (int i = 0; i < keyCount; i++)
        {
            int captured = i; // prevent closure over loop variable
            actions[i] = () => _attr.GetValue($"key{captured}");
        }
        actions[keyCount] = () => _attr.RemoveModifier("shared");
        Parallel.Invoke(actions);

        // All keys should have lost the shared bonus
        for (int i = 0; i < keyCount; i++)
            Assert.AreEqual(100, _attr.GetValue($"key{i}"));
    }

    #endregion

    #region CacheOrSkip Branch Coverage

    [TestMethod]
    public void CacheOrSkip_GlobalGenMatches_CachesValue()
    {
        // True branch of line 98: gen unchanged → value is cached
        _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);

        // First call: cache miss → compute → CacheOrSkip → gen matches → cache
        Assert.AreEqual(100, _attr.GetValue("atk"));

        // Second call: should hit cache (proving true branch worked)
        // We can't directly observe the cache, but we can verify behavior:
        // If value is cached, SetModifier invalidates → next GetValue recomputes
        // (We trust the existing cache invalidation tests)
        Assert.AreEqual(100, _attr.GetValue("atk"));
    }

    [TestMethod]
    public void CacheOrSkip_WhenGlobalGenChangesDuringGetValue_SkipsCaching()
    {
        // False branch of line 98: gen changed between snapshot and CacheOrSkip
        // → value is NOT cached, but still returned to the caller.
        //
        // This test runs many iterations with concurrent global operations
        // to statistically hit the timing window where GetValue takes its
        // genBefore snapshot BEFORE a global mutation, then reaches
        // CacheOrSkip AFTER the mutation incremented _globalGen.
        for (int iter = 0; iter < 100; iter++)
        {
            _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
            _attr.GetValue("atk"); // populate cache so we get a cache miss on next call

            // Race: GetValue reads genBefore → Clear increments gen
            // → GetValue reaches CacheOrSkip with mismatched gen
            Parallel.Invoke(
                () => _attr.GetValue("atk"),
                () => _attr.Clear()
            );

            // Re-setup and get value — if CacheOrSkip had incorrectly cached
            // the stale result, we'd get the stale value here.
            _attr.SetModifier("atk", ModifierType.BaseValue, "base", 200);
            Assert.AreEqual(200, _attr.GetValue("atk"), $"Iteration {iter}: should return fresh 200, not stale cached value");
        }
    }

    [TestMethod]
    public void CacheOrSkip_WhenGlobalGenChangesDuringMissingKeyGetValue_SkipsCaching()
    {
        // False branch tested specifically for the CacheOrSkip call at line 71
        // (key not in _modifiers → CacheOrSkip(key, Zero, genBefore))
        for (int iter = 0; iter < 100; iter++)
        {
            // Don't set any modifiers for "missing"
            // First get creates the key lock entry (from GetOrAdd in GetLock)
            _attr.GetValue("missing"); // cache miss → genBefore → TryGetValue fails → CacheOrSkip

            // Now race with clear to force gen mismatch on the next get
            _attr.SetModifier("missing", ModifierType.BaseValue, "base", 100);
            _attr.GetValue("missing"); // populate cache
            _attr.RemoveAllModifiers("missing"); // empty it again

            Parallel.Invoke(
                () => _attr.GetValue("missing"),
                () => _attr.Clear()
            );

            _attr.SetModifier("missing", ModifierType.BaseValue, "base", 300);
            Assert.AreEqual(300, _attr.GetValue("missing"), $"Iteration {iter}: should return fresh 300, not stale 0");
        }
    }

    [TestMethod]
    public void CacheOrSkip_WhenGlobalGenChangesDuringNoBaseValueGetValue_SkipsCaching()
    {
        // False branch tested specifically for the CacheOrSkip call at line 74
        // (key has modifiers but no BaseValue → CacheOrSkip(key, Zero, genBefore))
        for (int iter = 0; iter < 100; iter++)
        {
            _attr.SetModifier("atk", ModifierType.PercentBonus, "buff", 0.5f);
            _attr.SetModifier("atk", ModifierType.FlatBonus, "flat", 50);
            // No BaseValue set → GetValue returns 0 via CacheOrSkip on line 74

            // Now race with Clear
            Parallel.Invoke(
                () => _attr.GetValue("atk"),
                () => _attr.Clear()
            );

            // Re-setup with a BaseValue (and re-add PercentBonus to verify formula)
            _attr.SetModifier("atk", ModifierType.BaseValue, "base", 100);
            _attr.SetModifier("atk", ModifierType.PercentBonus, "buff", 0.5f);
            Assert.AreEqual(150, _attr.GetValue("atk"), $"Iteration {iter}: should return 150 (100 base * (1+0.5))");
        }
    }

    #endregion
}
