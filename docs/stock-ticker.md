# 盯盘组件（二次开发说明）

本仓库是 [AF Media Bar](https://github.com/Fervent-Tempo/AF-Media-Bar) 的个人二次开发版本，在原作者的任务栏组件体系里新增了一个**股票盯盘组件**（`TaskBarMediaControl.Stock.cs`）。它与既有的性能指标、蓝牙电量同一层：占用任务栏媒体栏静置层的一个格子，参与排序、显隐与两种朝向。

> 说明：这个改动只服务于个人使用，没有提交给原作者仓库。上游 `AGENTS.md` 的协作流程（先与核心开发者沟通、提交/推送需授权）在本分支不适用，但代码风格、注释语言与文档约定仍然沿用。

## 功能

- 在任务栏媒体栏里显示一只自选股：上行是证券简称，下行是最新价与涨跌幅，红涨绿跌（可反色）。
- 组件宽度只放得下一只，按「轮换间隔」在自选列表里轮流显示；**点击组件立即切到下一只**。
- 不需要正在播放音乐：没有媒体时它照样显示（它只看自己的开关，不落在"无媒体保留列表"里）。
- 悬停显示完整读数（名称、价格、涨跌幅、行情时间、第几只/共几只）。
- A 股交易时段（工作日 9:15–11:35、12:55–15:05）按设置的间隔刷新，其余时间自动降频到 60 秒。

## 设置入口

设置 → 小组件 → 盯盘组件：

| 项 | 说明 |
|---|---|
| 显示盯盘组件 | 总开关；关闭后组件从媒体栏移除，轮询也停止 |
| 自选股 | 空格或逗号分隔；纯数字按首位推断市场（6 开头沪市，0/1/2/3 开头深市） |
| 刷新间隔 | 2–60 秒，默认 5 秒 |
| 轮换间隔 | 2–120 秒，默认 6 秒 |
| 显示证券简称 / 显示涨跌幅 | 极窄媒体栏时用 |
| 绿涨红跌 | 默认红涨绿跌（A 股习惯），打开后反色 |

默认自选：`sh000001`（上证指数）、`sh600519`（贵州茅台）、`sz300750`（宁德时代）。

**已装过旧版本的机器**：旧的 `settings.json` 里没有这个组件的布局项，它会被当成"未启用"补齐，因此需要在上面那一页把开关打开一次；全新配置直接可见。

## 数据源

- 实时行情：腾讯 `https://qt.gtimg.cn/?q=sh600519,sz000001`（GB18030 报文，`~` 分隔），无需 token，无 CORS 限制。
- 只取用：简称、最新价、昨收、涨跌幅、行情时间。拿不到数据的标的不会进快照，组件保留上一只的读数。

## 改动清单

新增：

| 文件 | 职责 |
|---|---|
| `src/AFMediaBar/Classes/Settings/StockTickerSettings.cs` | 设置契约：自选、节奏、显示项、代码归一化 |
| `src/AFMediaBar/Classes/Models/StockTickerSnapshot.cs` | 只读快照模型（`StockQuote` / `StockTickerSnapshot`） |
| `src/AFMediaBar/Classes/Services/Stock/StockQuoteService.cs` | 应用级行情源（`IHostedService`，自建定时器，产出快照 + `Changed` 事件） |
| `src/AFMediaBar/Components/MediaBar/TaskBarMediaControl.Stock.cs` | 控件侧：订阅快照、轮换、着色、提示、点击 |
| `src/AFMediaBar/ViewModels/Pages/StockTickerSettingsViewModel.cs` | 设置页编辑器 |

改动（均为"追加一个组件"所必需的最小面）：

- `TaskbarExperienceSettings.cs`：`TaskbarRestComponent.StockTicker = 17`（枚举只能追加）。
- `TaskbarRestLayoutPolicy.cs`：默认顺序加入该组件；`Visibility` 增加 `StockEnabled`；`IsVisible` 中它与媒体状态解耦。
- `ComponentLayoutPolicy.cs`：旧设置的隐式布局里按 `StockTicker.Enabled` 判定可见。
- `AppSettings.cs` / `SettingsManager.cs`：`StockTicker` 属性、归一化与重置策略（自选属用户数据，重置保留）。
- `ISettingsConfiguration` / `LegacySettingsConfiguration`：`SetStockTicker`，并在"由关到开"时把该组件写进常驻与无媒体两份布局。
- `TaskBarMediaControl.xaml(.cs)`、`.ComponentInput.cs`、`.RestTransitions.cs`、`.Vertical.cs`：视觉表面、位置、显隐、拖动重排映射、竖向长度。
- `TaskbarWindow.xaml.cs` / `FloatingBarWindow.cs`：宿主把行情源绑给控件（可选参数，旧调用点不受影响）。
- `App.xaml.cs`：`StockQuoteService` 单例 + `IHostedService` 注册，`StockTickerSettingsViewModel` 作用域注册。
- `WidgetsSettingsPage.xaml` / `WidgetsSettingsViewModel.cs`：设置分组。
- `Strings{ZhHans,ZhHant,En,Vi}.resx`：22 条文案（四种语言齐备）。
- `docs/settings-contracts.json`：登记 8 个新设置字段（`unreleased`）。
- `tests/.../AudioSettingsPageLoadingTests.cs`：构造 `WidgetsSettingsViewModel` 时补上新参数。

另外补了仓库里缺失的 `docs/third-party-audio-notices.md`（`csproj` 把它作为嵌入资源引用，缺文件时编译直接失败 CS1566；上游 main 分支当前就缺这个文件）。

## 构建

需要 .NET 10 SDK（`winget install Microsoft.DotNet.SDK.10`）：

```powershell
dotnet build src\AFMediaBar\AFMediaBar.csproj -c Debug
dotnet run --project src\AFMediaBar\AFMediaBar.csproj
```

发布单文件（与上游发布口径一致）：

```powershell
dotnet publish src\AFMediaBar\AFMediaBar.csproj -c Release -r win-x64 --self-contained true
```

## 验证状态

| 项 | 结果 |
|---|---|
| 编译 | 通过（0 error；5 个上游既有 warning） |
| 测试工程编译 | 通过 |
| `dotnet test` | 本机未跑起来：测试宿主 `OpenProcess` 被沙箱拒绝（Win32Exception 5），与代码无关 |
| 策略 + 行情链路 | 通过独立验证host：15 项断言全过，含真实拉取 `sh000001 上证指数 +1.86%`、`sh600519 -2.59%`、`sz300750 +1.49%` |
| 任务栏实际显示 | **待你在真实桌面上验收**：本机任务栏被全屏窗口遮住（自动隐藏），无法截图确认 |

实机验收建议：跑起来后在 `设置 → 小组件 → 盯盘组件` 打开开关，看媒体栏右侧是否出现两行读数；再试竖向任务栏、无音乐状态、点击切下一只。

## 在另一台已经装了 AF Media Bar 的机器上怎么用

### 路线 A：直接换程序（不需要源码和 SDK，推荐）

1. 拷 `AFMediaBar-stock-ticker.exe`（自包含单文件，约 117 MB，内置 .NET 运行时）到那台机器任意目录。
2. 先退出已安装版：托盘图标右键 → 退出，确认任务栏媒体条消失（**不要两个同时跑**，否则会各挂一条媒体条并争抢全局热键）。
3. 双击新 exe 运行。设置、自选股、布局都在 `%LOCALAPPDATA%\AFMediaBar\settings.json`，两个版本共用同一份，不需要迁移。
4. 去 `设置 → 小组件 → 盯盘组件` 把开关打开（已装过旧版的配置里没有这个组件的布局项，会以"未启用"补齐）。
5. 想回退：退出它，直接启动原来的安装版即可；安装版的注册表项与卸载信息没有被改动。

### 路线 B：源码 + 补丁（那台机器有 .NET 10 SDK 时）

```powershell
git clone https://github.com/minwei35/my-stock-bar.git af-media-bar
cd af-media-bar
git checkout stock-ticker          # 已经在分支上，无需再打补丁
dotnet build src\AFMediaBar\AFMediaBar.csproj -c Debug
```

如果手上是上游源码而不是这个分支，用补丁文件：

```powershell
git apply --3way AF-Media-Bar-stock-ticker.patch
```

（`docs/third-party-audio-notices.md` 是新增文件，补丁里带上了；上游 main 缺它，编译会报 CS1566。）

