# GameAttr 游戏属性库

[![License: MPL-2.0](https://img.shields.io/badge/License-MPL--2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Commit Convention](https://img.shields.io/badge/Commit-Conventional%20Commits-F05032?logo=git)](https://www.conventionalcommits.org/)
[![Tests](https://img.shields.io/badge/Tests-MSTest-006600?logo=microsoft)](GameAttr.Tests)

> **一个通用的、解耦的游戏属性库** — 使用公式 `最终值 = 基础值 × (1 + 百分比加成) + 固定值加成` 计算属性值，这一修饰模型源自 **龙与地下城** 和 **暗黑破坏神2** 等经典 RPG。线程安全，完全测试覆盖，开箱即用。


[English Documentation](README.md)

---

## 特性

- **泛型设计** — `Attr<TKey, TModId, TValue>` 支持任意键类型、修饰符 ID 类型和数值类型（`INumber<T>`）。
- **三种修饰符** — `BaseValue`、`PercentBonus`、`FlatBonus` 完整覆盖基础值、百分比加成与固定值加成的属性叠加体系。
- **线程安全** — 逐键锁定实现并发的修饰符读写，全局锁定用于批量操作。
- **灵活的移除方式** — 支持按 `(key, type, modId)`、按 `(key, modId)` 跨类型、按 `(modId)` 全局移除。
- **零耦合** — 纯逻辑库，不依赖任何游戏引擎或框架，可在隔离环境中运行测试。
- **完善的文档** — 所有公开 API 均附带 XML 文档注释。
- **100% 测试覆盖率** — MSTest 测试套件覆盖基础值、百分比加成、固定值加成、移除语义、枚举键、覆盖写入和边界情况。

---

## 计算公式

```
最终值 = 基础值 × (1 + 百分比加成) + 固定值加成
```

| 修饰符类型 | 含义 | 示例 |
|---|---|---|
| `BaseValue` | 所有基础值修饰符之和 | 初始攻击力 1000 |
| `PercentBonus` | 所有百分比修饰符之和，加法叠加 | 0.3 → +30% |
| `FlatBonus` | 所有固定值修饰符之和，百分比计算后加上 | +50 |

**示例：** 若 `BaseValue = 1000`，`PercentBonus = 0.3`，`FlatBonus = 50`：  
`最终值 = 1000 × (1 + 0.3) + 50 = 1350`

---

## 快速开始

```xml
<!-- 通过 NuGet 引用或直接引用项目 -->
<PackageReference Include="GameAttr" Version="1.0.0" />
```

```csharp
using GameAttr;

// 创建一个属性容器：字符串键、字符串修饰符 ID、float 值
Attr<string, string, float> attr = new();

// 设置基础值
attr.SetModifier("atk", ModifierType.BaseValue, "base", 1000);

// 应用加成
attr.SetModifier("atk", ModifierType.PercentBonus, "buff1", 0.2);  // +20%
attr.SetModifier("atk", ModifierType.PercentBonus, "buff2", 0.1);  // +10%
attr.SetModifier("atk", ModifierType.FlatBonus, "flat", 50);       // +50

// 获取计算后的最终值
float final = attr.GetValue("atk");  // 1000 * (1 + 0.3) + 50 = 1350

// 移除某个修饰符
attr.RemoveModifier("atk", ModifierType.PercentBonus, "buff1");
```

---

## API 概览

### `Attr<TKey, TModId, TValue>`

| 方法 | 说明 |
|---|---|
| `SetModifier(key, type, modId, value)` | 设置或覆盖一个修饰符 |
| `GetValue(key)` | 获取计算后的属性值 |
| `RemoveModifier(key, type, modId)` | 移除指定的修饰符 |
| `RemoveModifier(key, modId)` | 根据 ID 跨类型移除某个键下的所有匹配修饰符 |
| `RemoveModifier(modId)` | 根据 ID 全局移除所有键下的匹配修饰符 |
| `RemoveAllModifiers(key)` | 移除某个键的所有修饰符 |
| `Clear()` | 清空所有修饰符 |
| `ToString()` | 输出所有修饰符的 JSON 快照 |

### `ModifierType`

| 枚举值 | 说明 |
|---|---|
| `BaseValue` | 属性的基础值，所有加成均基于基础值之和计算 |
| `PercentBonus` | 基于基础值的百分比加成（如 `0.1` = +10%），多个百分比加法叠加 |
| `FlatBonus` | 固定数值加成，在百分比计算后加算 |

---

## 线程安全

- **逐键锁定** — `SetModifier`、`GetValue`、`RemoveModifier`、`RemoveAllModifiers` 操作不同的键互不阻塞。
- **全局锁定** — `Clear()` 和 `RemoveModifier(modId)` 使用全局锁确保多键操作的原子性。
- 内部使用 `ConcurrentDictionary` 实现无锁读取。

---

## 运行测试与覆盖率

前置依赖：

```bash
dotnet add package coverlet.collector
dotnet tool install --global dotnet-reportgenerator-globaltool
```

**Windows** 下可直接运行便捷脚本：

```bash
cd GameAttr.Tests
.\run-coverage.cmd
```

脚本会自动运行测试并收集覆盖率数据，通过 ReportGenerator 生成 HTML 报告并打开浏览器显示。

或手动运行：

```bash
dotnet test GameAttr.Tests/GameAttr.Tests.csproj --collect:"XPlat Code Coverage"
```

---

## 协议

[Mozilla Public License 2.0](LICENSE) © Suntion
