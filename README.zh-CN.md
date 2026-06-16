# GameAttr 游戏属性库

[![License: MPL-2.0](https://img.shields.io/badge/License-MPL--2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Commit Convention](https://img.shields.io/badge/Commit-Conventional%20Commits-F05032?logo=git)](https://www.conventionalcommits.org/)
[![Tests](https://img.shields.io/badge/Tests-MSTest-006600?logo=microsoft)](GameAttr.Tests)
[![Coverage](https://img.shields.io/codecov/c/github/SunYanbox/GameAttr)](https://codecov.io/gh/SunYanbox/GameAttr)

> **一个通用的、解耦的游戏属性库** — 使用公式 `最终值 = 基础值 × (1 + 百分比加成) + 固定值加成` 计算属性值，这一修饰模型源自 **龙与地下城** 和 **暗黑破坏神2** 等经典 RPG。线程安全，完全测试覆盖，开箱即用。


[English Documentation](README.md)

---

## 特性

- **泛型设计** — `Attr<TKey, TModId, TValue>` 支持任意键类型、修饰符 ID 类型和数值类型（`INumber<T>`）。
- **三种修饰符** — `BaseValue`、`PercentBonus`、`FlatBonus` 完整覆盖基础值、百分比加成与固定值加成的属性叠加体系。
- **线程安全** — 逐键锁定实现并发的修饰符读写，全局锁定用于批量操作。
- **缓存读取** — `GetValue` 采用代数计数器缓存计算结果，修饰符未变更时重复读取近乎零竞争。
- **灵活的移除方式** — 支持按 `(key, type, modId)`、按 `(key, modId)` 跨类型、按 `(modId)` 全局移除。
- **变更事件系统** — `AttributeChanged` 事件在每次有效变更时触发；某个订阅者的异常不会影响其他订阅者。
- **日志支持** — `AttrLoggingConfiguration` 通过环境变量配置 `ILoggerFactory`；订阅者异常会被记录而不会导致程序崩溃。
- **零耦合** — 纯逻辑库，不依赖任何游戏引擎或框架，可在隔离环境中运行测试。
- **完善的文档** — 所有公开 API 均附带 XML 文档注释。
- **100% 测试覆盖率** — MSTest 测试套件覆盖基础值、百分比加成、固定值加成、移除语义、枚举键、覆盖写入、边界情况以及事件行为。

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
<PackageReference Include="GameAttr" Version="0.2.0" />
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

// 订阅属性变更事件
attr.AttributeChanged += args =>
{
    Console.WriteLine($"[{args.Key}] {args.ChangeType} → {args.NewValue}");
};
```

---

## API 概览

### `Attr<TKey, TModId, TValue>`

| 方法 / 事件 | 说明 |
|---|---|
| `SetModifier(key, type, modId, value)` | 设置或覆盖一个修饰符 |
| `GetValue(key)` | 获取计算后的属性值（缓存读 — 修饰符未变更时直接返回缓存） |
| `RemoveModifier(key, type, modId)` | 移除指定的修饰符 |
| `RemoveModifier(key, modId)` | 根据 ID 跨类型移除某个键下的所有匹配修饰符 |
| `RemoveModifier(modId)` | 根据 ID 全局移除所有键下的匹配修饰符 |
| `RemoveAllModifiers(key)` | 移除某个键的所有修饰符 |
| `Clear()` | 清空所有修饰符 |
| `ToString()` | 输出所有修饰符的 JSON 快照 |
| `AttributeChanged` | **事件** — 修饰符变更导致属性值变化时触发（详见下文） |

### `ModifierType`

| 枚举值 | 说明 |
|---|---|
| `BaseValue` | 属性的基础值，所有加成均基于基础值之和计算 |
| `PercentBonus` | 基于基础值的百分比加成（如 `0.1` = +10%），多个百分比加法叠加 |
| `FlatBonus` | 固定数值加成，在百分比计算后加算 |

### `AttrChangeType`

| 枚举值 | 说明 |
|---|---|
| `SetModifier` | 设置或覆盖修饰符导致数值变化 |
| `RemoveModifier` | 移除单个修饰符导致数值变化 |
| `RemoveAll` | 移除某个键的所有修饰符 |
| `Clear` | 清空所有键的所有修饰符 |

### `AttrChangedEventArgs<TKey, TValue>`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Key` | `TKey` | 发生变更的属性键 |
| `ChangeType` | `AttrChangeType` | 变更类型 |
| `NewValue` | `TValue` | 变更后重新计算的值 |

### 事件安全保证

- 事件在 **逐键锁和全局锁之外** 触发 — 订阅处理程序可以安全调用 `GetValue`（缓存已在事件触发前失效，返回最新数据）。
- 通过 `GetInvocationList()` 逐个调用每个订阅者 — 某个订阅者的异常 **不会** 阻止其他订阅者接收事件。
- 将修饰符设置为与原值相同的值不执行任何操作，**不会** 触发 `AttributeChanged` 事件。

### 日志配置

`AttrLoggingConfiguration.CreateLoggerFactory()` 根据环境变量创建 `ILoggerFactory`，用于记录订阅者异常，避免程序崩溃。

| 环境变量 | 默认值 | 说明 |
|---|---|---|
| `GAMEATTR_LOG_CONSOLE` | `true` | 设为 `"false"` 禁用控制台日志 |
| `GAMEATTR_LOG_FILE` | `false` | 设为 `"true"` 启用文件日志，输出到 `gameattr.log` |

```csharp
// 配置日志
ILoggerFactory loggerFactory = AttrLoggingConfiguration.CreateLoggerFactory();
ILogger<Attr<string, string, float>> logger = loggerFactory.CreateLogger<Attr<string, string, float>>();

// 将日志记录器传入构造函数 — 订阅者异常会被记录
var attr = new Attr<string, string, float>(logger);
```

> **注意：** 传入日志记录器是可选项。如果使用无参构造函数，日志功能将被禁用（使用 NullLogger），订阅者异常会被静默忽略。

---

## 线程安全

- **逐键锁定** — `SetModifier`、`GetValue`、`RemoveModifier`、`RemoveAllModifiers` 操作不同的键互不阻塞。
- **全局锁定** — `Clear()` 和 `RemoveModifier(modId)` 使用全局锁确保多键操作的原子性。
- **事件锁** — `AttributeChanged` 的订阅/取消订阅由专用锁序列化；事件的触发在 **逐键锁和全局锁之外** 执行，防止处理程序回调属性时发生死锁。
- **GetValue 缓存** — 每次计算结果带有代数标记，修饰符写入时递增代数；再次读取时若代数未变则直接返回缓存，无需获取写锁，显著降低读多写少场景下的竞争开销。
- 内部使用 `ConcurrentDictionary` 实现无锁读取。

---

## 运行测试与覆盖率

前置依赖：

```bash
dotnet add package coverlet.collector
```

**Windows** 下可直接运行便捷脚本生成本地 HTML 报告：

```bash
cd GameAttr.Tests
.\run-coverage.cmd
```

脚本会自动运行测试并收集覆盖率数据，通过 ReportGenerator 生成 HTML 报告并打开浏览器显示。

或手动运行：

```bash
dotnet test GameAttr.Tests/GameAttr.Tests.csproj --collect:"XPlat Code Coverage"
```

> 每次 CI 运行会自动将覆盖率数据上传至 [Codecov](https://codecov.io/gh/SunYanbox/GameAttr)。上方徽章反映 `main` 分支的最新覆盖率。

---

## 协议

[Mozilla Public License 2.0](LICENSE) © Suntion
