# 盯盘组件 · 二开交接文档

> 目的：把"在 AF Media Bar 上加一个任务栏股票盯盘组件"这件事的**现状、结构、踩过的坑、后续怎么改**写在一处。新会话不用重新摸代码，也不用再踩一遍已经踩过的坑。
> 面向：后续接手优化的人 / AI 会话。
> 最后更新：1.6.4（2026-11-04）

---

## 1. 一页速览

| 项 | 值 |
|---|---|
| 这是什么东西 | 上游 [AF Media Bar](https://github.com/Fervent-Tempo/AF-Media-Bar) 的个人二开版本，在任务栏媒体栏里加了一个**股票盯盘组件** |
| 当前版本 | **1.6.4**（上游 1.6.0 + 盯盘组件 + 4 轮修复） |
| 代码位置 | `C:\Users\minwe\Desktop\ai相关\AF-Media-Bar` |
| 你的 fork | https://github.com/minwei35/AF-Media-Bar ，分支 **`stock-ticker`**（默认 `main` 保持上游原样） |
| 安装包 | https://github.com/minwei35/AF-Media-Bar/releases/tag/stock-v1.6.4 → `AFMediaBar-Setup-v1.6.4-win-x64.exe`（106.2 MB） |
| 设置入口 | 设置 → **内容与组件 → 盯盘组件**（独立页签） |
| 数据源 | 腾讯 `https://qt.gtimg.cn/?q=sh600519,sz000001`（GB18030，无需 token/账号） |
| 运行依赖 | 无（安装包自带 .NET 10 运行时；WebView2 是上游歌词功能要的，系统一般已有） |
| 相关文档 | 功能与排障：[`stock-ticker.md`](stock-ticker.md)；发布说明：[`release-v1.6.1.md`](release-v1.6.1.md) |

---

## 2. 仓库与分支现状

```
origin  https://github.com/Fervent-Tempo/AF-Media-Bar.git   （上游，只读基座）
fork    ssh://git@ssh.github.com:443/minwei35/AF-Media-Bar.git

本地分支：main（= 上游 22d51ac）、stock-ticker（全部工作在此）
远程分支：fork/main、fork/stock-ticker
tag：stock-v1.6.1 ~ stock-v1.6.4（v* 留给上游，见 §13.8）
release：只保留 stock-v1.6.4（旧三个的 release 对象已删，tag 保留）
```

相对上游的改动规模：**44 个文件，+1587 / −15**；新增 11 个文件（含文档）。

### 提交序列

| 提交 | 内容 |
|---|---|
| `e3aef39` | feat: 任务栏盯盘组件（行情源、模型、设置契约、控件、布局接入、文案） |
| `ba688e6` | docs: 已安装机器上的两种使用方式（绿色版 exe / 源码+补丁） |
| `ea7c43a` | chore: 版本号 1.6.1 |
| `dc15e5e` | docs: v1.6.1 发布说明 |
| `955e71d` | fix(1.6.2): 独立设置页 + `SettingsManager` 事件分组（改设置不生效） |
| `7432345` | fix(1.6.3): 页签接进 `SettingsNavigationCatalog` 分区（导航里看不见） |
| `3866596` | fix(1.6.4): 总开关两个方向立即生效（关掉还在） |

---

## 3. 功能现状

组件挂在任务栏媒体栏的**静置层**里，和封面、媒体文字、频谱、性能指标、蓝牙电量同级：

- 两行读数：上行证券简称（10 px，次要色），下行「最新价 + 涨跌幅」（12 px，中黑，涨跌色）
- **一次只显示一只**：按「轮换间隔」在自选列表里循环；**点一下组件立刻切下一只**（不影响自动轮换节奏）
- 悬停：显示完整读数（名称、价格、涨跌幅、行情时间、第几只/共几只）+ 组件高亮
- **不需要正在放音乐**：它只看自己的开关，不落在"无媒体保留列表"里（`TaskbarRestLayoutPolicy.IsVisible` 里有专门分支）
- 交易时段（工作日 9:15–11:35、12:55–15:05）按设置的间隔刷新；其余时间自动降到 60 秒
- 支持竖向任务栏（长度 54 DIP，竖向排列由 `TaskBarMediaControl.Vertical.cs` 处理）
- 颜色：默认 A 股习惯红涨绿跌，可切「绿涨红跌」；平盘用主题次要前景色

### 设置项

| 项 | 默认 | 范围 | 说明 |
|---|---|---|---|
| `Enabled` 显示盯盘组件 | 开 | — | 总开关，唯一权威（见 §7.3） |
| `Codes` 自选股 | `sh000001 sh600519 sz300750` | ≤12 只 | 空格/逗号分隔；纯数字按首位推断市场 |
| `RefreshSeconds` 刷新间隔 | 5 秒 | 2–60（步长 1） | 交易时段生效；收盘固定 60 秒 |
| `RotateSeconds` 轮换间隔 | 6 秒 | 2–120（步长 1），0 = 不轮换 | 组件宽度只放得下一只 |
| `ShowName` 显示简称 | 开 | — | 极窄媒体栏可关 |
| `ShowPercent` 显示涨跌幅 | 开 | — | 关掉只留价格 |
| `SwapColors` 绿涨红跌 | 关 | — | 打开后反色 |

配置存放：`%LOCALAPPDATA%\AFMediaBar\settings.json` 的 `StockTicker` 节点（与上游共用同一份文件，覆盖安装不丢）：

```json
"StockTicker": {
  "Enabled": true,
  "Codes": ["sh000001", "sh600519", "sz300750"],
  "RefreshSeconds": 5,
  "RotateSeconds": 6,
  "ShowName": true,
  "ShowPercent": true,
  "SwapColors": false
}
```

代码归一化（`StockTickerSettings.NormalizeCode`）：`600519` → `sh600519`、`000001` → `sz000001`、`300750` → `sz300750`、`600519.SS` → `sh600519`、`00700` → `hk00700`，其余按美股 `usXXX` 处理。**注意：HK/US 目前也走腾讯接口但没有对应交易时段判断**（`StockQuoteService.IsTradingNow` 只判断 A 股），所以港股美股会一直按 60 秒低频刷。

---

## 4. 架构与数据流

```
行情侧                                           呈现侧
┌───────────────────────────────┐      ┌──────────────────────────────────────┐
│ StockQuoteService (Singleton) │      │ TaskBarMediaControl (.Stock 分部)     │
│  IHostedService + Timer       │      │  BindStockSource(service)            │
│  ── HTTP GET qt.gtimg.cn ──►  │─────►│  Changed ──► 切 UI 线程 ──► 刷新两行   │
│  GB18030 解码 + '~' 解析      │ 快照 │  DispatcherTimer ──► 轮换 _stockIndex │
│  Snapshot / Changed 事件      │      │  点击 / 悬停 / 提示 / 配色             │
└───────────────────────────────┘      └──────────────┬───────────────────────┘
        ▲ 读 SettingsManager.Current.StockTicker        │ 布局与几何
        │                                              ▼
┌───────┴────────────────┐            ┌──────────────────────────────────────┐
│ AppSettings.StockTicker│            │ TaskbarRestLayoutPolicy.Arrange       │
│  Normalize()           │◄───────────│  → ApplyTaskbarSectionGeometry        │
│ SettingsManager 事件    │  设置写入   │  → PlaceRestWidget(TaskbarStockSurface)│
└────────────────────────┘            └──────────────────────────────────────┘
```

分层职责（遵循上游约定：View/控件只呈现，服务拥有外部资源，纯策略不碰 I/O）：

| 层 | 文件 | 职责 |
|---|---|---|
| 契约 | `Classes/Settings/StockTickerSettings.cs` | 字段、默认值、`Normalize()`、代码归一化 |
| 模型 | `Classes/Models/StockTickerSnapshot.cs` | `StockQuote`（只读读数）、`StockTickerSnapshot`（一批读数 + 时间 + 是否交易时段 + 错误） |
| 服务 | `Classes/Services/Stock/StockQuoteService.cs` | 轮询、解析、降频、事件；不懂 UI |
| 控件 | `Components/MediaBar/TaskBarMediaControl.Stock.cs` | 订阅快照、轮换、着色、提示、点击；不建 HTTP、不解析报文 |
| 视图 | `Components/MediaBar/TaskBarMediaControl.xaml` 里的 `TaskbarStockSurface` | 两行 `TextBlock` 的视觉 |
| 设置页 | `Views/Pages/StockTickerPage.xaml(.cs)` + `ViewModels/Pages/StockTickerSettingsViewModel.cs` | 三组参数编辑器 + 按页重置 |

---

## 5. 文件地图

### 新增

| 文件 | 行数级 | 要点 |
|---|---|---|
| `Classes/Settings/StockTickerSettings.cs` | 120 | `record`；`Default`、`Normalize()`、`NormalizeCode()`；常量 `Minimum/MaximumRefreshSeconds`、`RotateSeconds`、`MaximumCodes` |
| `Classes/Models/StockTickerSnapshot.cs` | 40 | `StockQuote(Code,Name,Price,ChangePercent,Time)`、`IsUp/IsDown`；`StockTickerSnapshot(Quotes,UpdatedAt,IsTrading,Error)` |
| `Classes/Services/Stock/StockQuoteService.cs` | 220 | `IHostedService`；`StartAsync` 起 Timer 并订阅 `SettingsManager.SettingsChanged`；`PollAsync` 拉取解析；`ScheduleNext` 决定下次间隔；`IsTradingNow()` |
| `Components/MediaBar/TaskBarMediaControl.Stock.cs` | 190 | `BindStockSource`、`StockLoaded/Unloaded`、`StockChanged`、`StockSettingsChanged`、`ApplyStockSettings`、`RefreshStockReadout`、`ResolveStockBrush`、点击/悬停处理器 |
| `ViewModels/Pages/StockTickerSettingsViewModel.cs` | 130 | 每个属性 setter → `_configuration.SetStockTicker(...)`；`WatchlistText` 解析；`WatchlistSummary` |
| `Views/Pages/StockTickerPage.xaml(.cs)` | 53/30 | `INavigableView<WidgetsSettingsViewModel>`，三组 `SettingsGroup`，`ResetButton_Click` → `SettingsResetScope.StockTicker` |
| `docs/stock-ticker.md` | — | 功能说明 + 排障（用户向） |
| `docs/release-v1.6.1.md` | — | 首版发布说明 |
| `docs/stock-ticker-handover.md` | — | 本文 |
| `docs/third-party-audio-notices.md` | — | **补上游缺失文件**，见 §13.2 |

### 改动（都是"加一个组件"绕不开的最小面）

| 文件 | 改了什么 |
|---|---|
| `Classes/Settings/TaskbarExperienceSettings.cs` | `TaskbarRestComponent.StockTicker = 17`（枚举只能追加） |
| `Classes/Services/Taskbar/TaskbarRestLayoutPolicy.cs` | `DefaultTailOrder` 加入该组件；`Visibility` 记录加 `StockEnabled`；`IsVisible()` 里它与媒体状态解耦；switch 加分支 |
| `Classes/Services/Taskbar/ComponentLayoutPolicy.cs` | `Legacy()` 的 `Visible()` 按 `StockTicker.Enabled`；`ResolveAll()` 末尾加 `Gate()`（总开关权威） |
| `Classes/Settings/AppSettings.cs` | 字段 + `StockTicker` 属性（setter 里 `Normalize()`）+ `Normalize()` + `Clone()` |
| `Classes/Settings/SettingsManager.cs` | 重置组件时保留自选；`RaiseChanged` 里 `case nameof(AppSettings.StockTicker)` → `ExtraFeaturesSettingsChanged` |
| `Classes/Settings/SettingsEvents.cs` | `SettingsResetScope.StockTicker` |
| `Classes/Abstractions/ISettingsConfiguration.cs` | `SetStockTicker(StockTickerSettings)` |
| `Classes/Services/Settings/LegacySettingsConfiguration.cs` | 实现 `SetStockTicker`：写设置 + **双向**写回 Resident/Idle 两份布局 |
| `Classes/Services/Settings/SettingsSectionResetPolicy.cs` | `Handles` 加 scope；`Apply` 里重置只恢复节奏与显示项，保留 `Codes`/`Enabled` |
| `Classes/Services/Settings/SettingsSearchPolicy.cs` | `SettingsPageKey.StockTicker` |
| `Classes/Services/Settings/SettingsPageCatalog.cs` | 页面定义（图标 `DataTrending24`、`IsGlobal: true`） |
| `Classes/Services/Settings/SettingsNavigationCatalog.cs` | **content 分区**加入该页（1.6.3 的坑，见 §10） |
| `Views/Windows/SettingsPageProvider.cs` | 页面键 → `typeof(StockTickerPage)` |
| `Views/Windows/TaskbarWindow.xaml.cs` / `FloatingBarWindow.cs` | 构造参数末尾加 `StockQuoteService? stockSource = null`（可选，旧调用点不受影响）+ `BindStockSource` |
| `App.xaml.cs` | `AddSingleton<StockQuoteService>` + `AddHostedService`；`AddScoped<StockTickerPage>`、`AddScoped<StockTickerSettingsViewModel>` |
| `Components/MediaBar/TaskBarMediaControl.xaml` | `TaskbarStockSurface`（`Panel.ZIndex=4`、`Tag="MediaAction"`、`HorizontalAlignment=Left`） |
| `TaskBarMediaControl.xaml.cs` | `PlaceRestWidget(...)`、`SetRestComponentVisible(...)`、`ResolveRestComponentWidth` 返回 `StockSurfaceWidth`(96)、`ResolveRestVisibility` 传 `StockTicker.Enabled` |
| `TaskBarMediaControl.ComponentInput.cs` / `.RestTransitions.cs` / `.Vertical.cs` | 命中映射 / 拖动重排映射 / 竖向长度与摆放 |
| `ViewModels/Pages/WidgetsSettingsViewModel.cs` | 构造加 `StockTickerSettingsViewModel stock`，暴露 `Stock` |
| `ViewModels/Pages/TaskbarRestComponentSettingItem.cs` | 名称/说明文案键（否则列表里回落成「封面」） |
| `ViewModels/Pages/DisplayModesViewModel.ComponentLayouts.cs` | 组件的「配置」跳转 → `SettingsPageKey.StockTicker` + 分组 `Widgets.Stock` |
| `Components/Settings/ComponentSymbolConverter.cs` | 图标 `DataTrending24` |
| `Resources/Strings{ZhHans,ZhHant,En,Vi}.resx` | 28 条文案（22 + 6） |
| `docs/settings-contracts.json` | 登记 8 个字段，release 标 `unreleased` |
| `tests/.../AudioSettingsPageLoadingTests.cs` | `WidgetsSettingsViewModel` 构造补参数 |

---

## 6. 任务栏接入细节（改这块之前必读）

### 6.1 组件身份

`TaskbarRestComponent` 是**参与序列化的枚举，只能追加**：`StockTicker = 17`。用户在旧设置里没有这一项时，`ComponentLayoutPolicy.ResolveAll` 会把它以 `IsVisible = false` 补进布局——这就是"全新安装默认可见、老配置需要手动开一次"的来源。

### 6.2 宽度与摆放

- `ResolveRestComponentWidth(StockTicker)` → `StockSurfaceWidth = 96` DIP（常量在 `.Stock.cs` 顶部）
- 位置由 `TaskbarRestLayoutPolicy.Arrange` 按用户顺序算出，写进 `Margin.Left`；XAML 里因此**必须 `HorizontalAlignment="Left"`**
- 横轴长度参与 `TaskbarExperiencePolicy.CalculateRestWidth`（它用的是同一个 `ResolveRestComponentWidth` 回调，所以不用另改）
- 竖向：`TaskBarMediaControl.Vertical.cs` 里 `StockVerticalLength = 54`，并在 `Place(...)` 序列里挂上

### 6.3 显隐的三道闸（关掉开关必须真的消失）

1. `TaskbarRestLayoutPolicy.IsVisible(StockTicker, visibility)` → `visibility.StockEnabled`，且**早于"无媒体"分支**判断（所以没有音乐也显示）
2. `ComponentLayoutPolicy.Legacy.Visible(StockTicker)` → `settings.StockTicker.Enabled`（构建隐式布局时）
3. `ComponentLayoutPolicy.ResolveAll` 末尾的 `Gate()`：`IsVisible = settings.StockTicker.Enabled`，**布局里存着什么都覆盖不了它**

配套：`LegacySettingsConfiguration.SetStockTicker` 在**开关任一方向变化**时都写回 Resident 与 Idle 两份布局（保持「常驻状态」列表里的勾选与实际一致）。

> 教训：只做 (1)(2) 不够——布局条目才是最终依据；只做"关→开"单向写回也不够。见 §10 的 1.6.4。

### 6.4 几何落地

`ApplyRestComponentVisibility(layout)` 里 `SetRestComponentVisible(TaskbarStockSurface, layout.Find(StockTicker) is not null)`，由 `ApplyTaskbarSectionGeometry` 驱动。只发 `RaiseDesiredSizeChanged` 时，**宽度未变会被内容指纹去重**（固定长度模式下必然如此），所以设置变更后控件自己要重跑 `ApplyTaskbarSectionGeometry` + `RefreshDesiredSize`（`.Stock.cs` 的 `StockSettingsChanged` 末尾，模仿 `BatteryChanged`）。

---

## 7. 设置页接入细节

导航是**两层**，两处都要登记，缺一处就不显示：

```
SettingsSearchPolicy.SettingsPageKey         ← 页面身份（枚举）
SettingsPageCatalog.TaskbarPages            ← 页面定义（标题键/图标/是否全局/描述键）
SettingsNavigationCatalog.Sections          ← 左侧分区归属（硬编码列表！）
SettingsPageProvider.PageTypes              ← 键 → 页面类型
App.xaml.cs                                 ← AddScoped<StockTickerPage>
```

- 分类首页的卡片列表由 `SettingsNavigationCatalog.Sections` 驱动，所以只加前两处时**侧栏和分类首页都看不到**
- 搜索索引不需要手写：`csproj` 把 `Views\Pages\*Page.xaml` 作为 `AFMediaBar.SettingsSearch.%(Filename).xml` 内嵌，按页面里的 `GroupId` 自动建索引
- 重置：`SettingsResetScope.StockTicker` → `SettingsSectionResetPolicy.Handles/Apply`，语义"只恢复节奏与显示项，保留自选与开关"

---

## 8. 事件与生效链

上游 `SettingsManager` 按属性名把变更**分门别类**发事件，宿主各订阅自己关心的那条。任务栏宿主订阅的是 `ExtraFeaturesSettingsChanged`（`TaskbarWindow.xaml.cs:345` → `ApplyExtraFeaturesSettings()` → `MediaControl.ApplyTaskbarExperienceSettings()`）；悬浮条宿主订阅通用 `SettingsChanged`。

所以新增一个设置属性时，**必须在 `SettingsManager.RaiseChanged` 的 switch 里给它归组**，否则：悬浮条正常、任务栏不动（这正是 1.6.2 的现象）。

现在的完整链路：

```
页面 setter
  → ISettingsConfiguration.SetStockTicker
    → LegacySettingsConfiguration（写布局，双向）
      → AppSettings.StockTicker setter（Normalize + PropertyChanged）
        → SettingsManager.RaiseChanged
          ├─ ExtraFeaturesSettingsChanged → TaskbarWindow.ApplyExtraFeaturesSettings → 重排 + 重报尺寸
          ├─ SettingsChanged → 控件 StockSettingsChanged（切 UI 线程：重读 + 重画 + 重算几何）
          ├─ SettingsChanged → StockQuoteService.OnSettingsChanged（Timer 立刻触发一次拉取）
          └─ SettingsChanged → FloatingBarWindow.ApplySettings（悬浮条路径）
```

日志锚点：`[Stock] 设置变更: enabled=… codes=… showName=… showPercent=… rotate=…`（控件侧打点，用来判断事件是否到达控件）。

---

## 9. 版本历史与已修问题

| 版本 | 现象 | 根因 | 修法 |
|---|---|---|---|
| 1.6.1 | 有组件、有安装包；老配置下默认不显示 | 枚举新成员在旧布局里被补成 `IsVisible=false`（设计如此） | 文档说明：需在设置里打开一次 |
| 1.6.2 | 改配置任务栏不跟着变；开关关掉组件不消失 | `AppSettings.StockTicker` 没进 `SettingsManager.RaiseChanged` 的分组 switch，任务栏宿主收不到事件 | 归入 `ExtraFeaturesSettingsChanged`；控件与服务各自订阅变更 |
| 1.6.3 | 「内容与组件」里没有盯盘组件页签 | 只加了 `SettingsPageKey` + 页面目录，漏了 `SettingsNavigationCatalog` 的硬编码分区列表 | content 分区加入；顺带补常驻列表文案键、组件「配置」跳转、图标 |
| 1.6.4 | 关掉总开关，那一格还在 | ① `SetStockTicker` 只处理"关→开"，关掉时布局仍写可见；② 布局条目可以压过开关；③ 宽度未变时尺寸请求被去重，几何不重算 | 双向写布局 + `ResolveAll` 闸门（总开关唯一权威）+ 控件主动重算几何 |

验证方式：`_stock_verify`（见 §11.3）里 8 项断言覆盖"默认可见 / 关掉两份布局都不可见（即使布局写着可见）/ 重新打开可见 / SetVisible 写入"。

---

## 10. 构建与发布

### 10.1 本机工具链

| 组件 | 版本 | 说明 |
|---|---|---|
| .NET SDK | 10.0.403 | `winget install Microsoft.DotNet.SDK.10`（本次装的；`dotnet` 在 `C:\Program Files\dotnet`，PATH 可能需要手动加） |
| Inno Setup | 6.7.3 | `winget install JRSoftware.InnoSetup`；`ISCC.exe` 在 `%LOCALAPPDATA%\Programs\Inno Setup 6\` |
| Git 凭据 | SSH key `~/.ssh/github_iptv` | 认证为 `minwei35`；走 `ssh://git@ssh.github.com:443`（22 端口不通时走 443） |
| Release token | 用户环境变量 `GH_TOKEN` | fine-grained PAT，`Contents: Read and write`，7 天有效期；读法见 §10.4 |

### 10.2 开发/构建

```powershell
cd C:\Users\minwe\Desktop\ai相关\AF-Media-Bar
$env:PATH = "C:\Program Files\dotnet;$env:PATH"

dotnet build src\AFMediaBar\AFMediaBar.csproj -c Debug        # 编译（约 15–30 秒，含 MemoryHelper 子项目）
dotnet run   --project src\AFMediaBar\AFMediaBar.csproj      # 直接跑
dotnet test  tests\AFMediaBar.Layout.Tests\AFMediaBar.Layout.Tests.csproj   # 本机跑不起来，见 §12
```

### 10.3 出安装包

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 `
  -IsccPath "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
# 产物：artifacts\AFMediaBar-Setup-v<Version>-win-x64.exe + artifacts\installer-sha256.txt
```

脚本内部：`restore → dotnet publish -r win-x64 --self-contained（单文件）→ ISCC → 写 sha256`。它还会校验发布目录里**只有一个 exe**。

安装包行为（`.iss` 未改动，所以覆盖安装成立）：`AppId={{7C1B9E4C-5B2A-4E7E-9F1D-3A6C2E8B45D1}}`、`UsePreviousAppDir=yes`、`AppMutex=AFMediaBar.InstallCoordinator`、`PrivilegesRequired=lowest`（默认当前用户，`%LOCALAPPDATA%\Programs\AFMediaBar`；若原来装的是"所有用户"，向导首页要选同一档）、`MinVersion=10.0.17763`。

### 10.4 提交、打 tag、发 release

```powershell
# 提交 + 推分支
git add -A; git commit -F msg.txt; git push fork stock-ticker

# tag 用 stock-v* 前缀（避开上游 .github/workflows/release.yml 的 tags: ['v*']，见 §13.8）
git tag -a stock-v1.6.5 -m "v1.6.5-stock: ..."; git push fork stock-v1.6.5

# 发 release + 传附件（token 从注册表读，掩码输出，不进对话）
$t = [Environment]::GetEnvironmentVariable('GH_TOKEN','User')
$h = @{ Authorization = "Bearer $t"; Accept='application/vnd.github+json'; 'X-GitHub-Api-Version'='2022-11-28'; 'User-Agent'='stock-bar-release' }
$repo='minwei35/AF-Media-Bar'
$json = @{ tag_name='stock-v1.6.5'; name='...'; body=$notes; draft=$false; prerelease=$true } | ConvertTo-Json -Depth 3
$rel = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$repo/releases" -Headers $h `
         -Body ([Text.Encoding]::UTF8.GetBytes($json)) -ContentType 'application/json; charset=utf-8'
$upload = ($rel.upload_url -replace '\{\?name,label\}','')
Invoke-RestMethod -Method Post -Uri "$upload?name=AFMediaBar-Setup-v1.6.5-win-x64.exe" -Headers $h `
         -InFile artifacts\AFMediaBar-Setup-v1.6.5-win-x64.exe -ContentType 'application/octet-stream'
```

注意：`Invoke-RestMethod` 传中文正文必须显式转 UTF-8 字节（否则 GitHub 返回 422）；`-InFile` 上传 106 MB 约 1–2 分钟。

---

## 11. 验证状态

### 11.1 已经验证的

| 项 | 方式 | 结果 |
|---|---|---|
| 编译 | `dotnet build` | 0 error（上游 5 个既有 warning） |
| 测试工程编译 | `dotnet test` 的构建阶段 | 通过 |
| 行情链路 | `_stock_verify` 真拉一次 | 上证指数 / 贵州茅台 / 宁德时代 三只读数正常，含 GB18030 解析、涨跌幅、时间 |
| 布局策略 | `_stock_verify` 断言 | 15 项（代码归一化、去重、夹取、无媒体可见、顺序包含、96 DIP 摆放） |
| 开关闸门 | `_stock_verify` 断言 | 8 项（默认可见 / 关掉后两份布局都不可见 / 重新打开可见 / SetVisible 写回） |
| 单文件绿色版启动 | 本机 smoke | 进程起得来、能解包（需 `DOTNET_BUNDLE_EXTRACT_BASE_DIR` 指向可写目录，沙箱限制，见 §13.3） |
| 安装包 | Inno 6.7.3 | 编译通过、带 sha256、附件可匿名下载（HTTP 200，111 MB） |

### 11.2 没有验证的（诚实列出）

- **`dotnet test` 全量没跑起来**：测试宿主 `OpenProcess` 被沙箱拒绝（`Win32Exception 5`），与代码无关；CI 或真实机器上能跑
- **安装包没有真实执行过一次覆盖安装**（需要 UAC/交互式向导，沙箱里做不到）；AppId/`UsePreviousAppDir`/`AppMutex` 只是按契约核对
- **任务栏实际显示**只能在用户机器上验收（本机任务栏被全屏窗口遮住，见 §13.4）
- 未加仓库内的单元测试（`tests/AFMediaBar.Layout.Tests`）：因为跑不起来，加了也无法验证；建议在能跑测试的环境里补 `StockTickerSettingsTests` 与 `ComponentLayoutPolicy` 的可见性用例

### 11.3 本地验证工具（不在仓库里）

`C:\Users\minwe\Desktop\ai相关\_stock_verify` —— 一个 `net10.0-windows` WPF 控制台工程，`ProjectReference` 指向 `AFMediaBar.csproj`，直接调用 `StockTickerSettings` / `TaskbarRestLayoutPolicy` / `ComponentLayoutPolicy` / `StockQuoteService` 打断言。改完策略层后 `dotnet run -c Debug` 一看就知道有没有破。

---

## 12. 本机 / 沙箱踩坑记录

1. **`Start-Process` 会挂死**（沙箱对 GUI 进程/管道限制）：用 `cmd /c start "" "exe"` 启动，配 `Get-CimInstance Win32_Process` 判断存活。
2. **子进程不能写 `%APPDATA%` / `%LOCALAPPDATA%`**：所以本机跑的实例读不到/写不了 `settings.json` 与日志；验证靠"绿色版 + `DOTNET_BUNDLE_EXTRACT_BASE_DIR` 指到工作区"。
3. **单文件 exe 首次运行要解包到 `%TEMP%`**：沙箱禁写 → 表现为"双击没反应"；换目录即可，真实机器无此问题。
4. **任务栏在本机不可见**（被全屏窗口遮住 / 自动隐藏），所以"媒体栏是否出现在任务栏上"必须由用户在真机验收；截图区域为 2048×1152（125% 缩放下的虚拟像素），物理分辨率 2560×1440。
5. **PowerShell 5.1 的坑**：脚本里 `Start-Process -RedirectStandardOutput` 会因环境变量 `NO_PROXY/no_proxy` 重复而崩；`.ps1` 含中文时按 ANSI 解析可能炸；`[System.IO.File]` 用进程 CWD 而不是 PS 的 location，必须传绝对路径。
6. **CRLF 匹配**：用 `ReadAllText` + 字面 `old/new` 打补丁时，文件是 CRLF 而字符串是 LF 会匹配失败，改用正则或统一换行。
7. **GitHub API 未认证常 403**（共享出口 IP 限流）；建 release/传附件必须带 token。
8. **tag 命名避开 `v*`**：上游 `release.yml` 是 `tags: ['v*']` 触发，需要维护者签名密钥，在 fork 里必红叉，所以自己的 tag 用 `stock-vX.Y.Z`。
9. **上游 main 缺 `docs/third-party-audio-notices.md`**：`csproj` 把它作为 `EmbeddedResource` 引用，缺文件直接 `CS1566` 编译失败；本分支补了一份（内容是按包许可整理的声明）。
10. **`gh` 未安装**，全部走 REST API + SSH。

---

## 13. 已知限制与优化路线

### 13.1 功能

- [ ] **一次只显示一只**：宽度固定 96 DIP。若想要"两只并排"或"名称可滚动"，需要放宽宽度或再加一个组件实例（枚举只能追加，可以加 `StockTicker2`，但更干净的做法是让宽度可配置并横向平分）
- [ ] **没有分时图**：独立版 `my-stock-bar` 里已经实现过东财 `trends2` 分时曲线，可以搬进来做悬停浮层或点击详情
- [ ] **点击语义单一**：现在只切下一只。可改为"左键切下一只 / 右键打开详情浮层 / 中键刷新"
- [ ] **港股美股没有交易时段判断**：`IsTradingNow()` 只看 A 股，港美股会一直 60 秒低频。需要按市场分别定义时段与降频策略
- [ ] **没有名称搜索**：加自选只能手输代码。腾讯有 `https://smartbox.gtimg.cn/s3/?t=all&c=1&q=茅台` 可直接搜，独立版里用过
- [ ] **配色只有红/绿两档**：可考虑跟随强调色或允许自定义
- [ ] **没有涨跌额/成交额/换手率**：行情报文里都有（`fields[37]/[38]`），加显示项即可

### 13.2 工程

- [ ] **补仓库内单元测试**：`tests/AFMediaBar.Layout.Tests` 里加 `StockTickerSettings.Normalize/NormalizeCode`、`ComponentLayoutPolicy` 可见性与 `ResolveAll` 闸门的用例（本机跑不了测试，需要在能跑的环境加）
- [ ] **`docs/settings-contracts.json` 的条目还标着 `unreleased`**：正式发布时应改成版本号
- [ ] **`GitHub Releases` 目前是 pre-release**：想让 `latest` 指向它就去掉标记（但那样会盖过上游的 latest 语义，个人建议保持 pre-release）
- [ ] **失败重试/多源备份**：现在只有腾讯一个源；独立版里还有新浪 `hq.sinajs.cn`（需 `Referer`）可作兜底
- [ ] **节流**：`HttpClient` 是静态单例，没有 ETag/缓存；多显示器多宿主共用一份快照（已经是单例，别改成 per-host）

### 13.3 排障手册（现象 → 判断 → 动作）

| 现象 | 日志/证据 | 判断 | 动作 |
|---|---|---|---|
| 整条媒体栏不出现 | `[Taskbar] Placement … budgetPx=0, targetPx=0` | 任务栏没有空位 | 合并任务栏按钮 / 关「避开任务栏图标」/ 长度改固定 280 DIP |
| 组件不出现（媒体栏在） | `[Stock]` 无日志；`settings.json` 里 `StockTicker.Enabled=false` | 开关没开 | 设置 → 盯盘组件打开 |
| 关掉开关还在 | `[Stock] 设置变更: enabled=False` 有，但组件还在 | 布局闸门失效（回归） | 检查 `ComponentLayoutPolicy.ResolveAll` 的 `Gate()` |
| 改设置没反应 | 有 `[Stock] 设置变更` | 事件到了控件但几何没落地 | 检查 `StockSettingsChanged` 末尾的 `ApplyTaskbarSectionGeometry` |
| 改设置没反应 | 没有 `[Stock] 设置变更` | 事件没到（`SettingsManager` 归组丢了） | 检查 `RaiseChanged` 里 `case nameof(AppSettings.StockTicker)` |
| 设置里没有「盯盘组件」页签 | — | 导航分区没登记 | 检查 `SettingsNavigationCatalog.Sections` 的 content 分区 |
| 常驻列表里显示成「封面」 | — | 文案键映射缺失 | 检查 `TaskbarRestComponentSettingItem.ResolveNameKey` |
| 读数是 `—` | 悬浮条提示「行情不可用 · …」 | 网络/代码格式问题 | 手工 `curl "https://qt.gtimg.cn/?q=sh600519"` 验证 |

---

## 14. 顺带产生的另一个项目

`C:\Users\minwe\Desktop\ai相关\my-stock-bar`（仓库 https://github.com/minwei35/my-stock-bar.git，分支 `stock-ticker` 曾被用来中转 AF 的代码）

- 那是最早做的**独立小工具**：C# WinForms + `csc.exe` 离线构建（不需要 SDK），`dist\StockBar.exe` 可直接跑，支持任务栏嵌入/悬浮两种模式、自绘读数、详情浮层（东财分时曲线）
- 现在主线已改为"二开 AF Media Bar"，这个独立版保留作为**算法/接口参考**（分时曲线、新浪备用源、代码归一化的另一种写法都在里面）
- 若以后本机窗口环境受限（比如 AF 的 WPF + WebView2 跑不动），这条路仍可用

---

## 15. 参考

- 上游架构边界：`docs/architecture.md`
- 上游代码风格：`docs/code-style.md`（注释中英双语、结论在前、只写影响理解的信息）
- 上游设置发布登记：`docs/settings-contracts.md` + `tools/settings-contracts.ps1 -Action Query/Verify`
- 上游安装器契约：`installer/README.md`（覆盖安装、静默安装参数、`AppMutex` 语义都在里面）
- 行情接口：`https://qt.gtimg.cn/?q=<code1,code2>`（GB18030，`~` 分隔；`[1]` 名称、`[3]` 最新价、`[4]` 昨收、`[30]` 涨跌额、`[31]` 涨跌幅、`[32]` 最高、`[33]` 最低、`[29]` 时间、`[37]` 成交额(万)、`[38]` 换手率）
