# MouseSim 开发文档

> 面向后续开发者（包括未来的你）：看完本文即可了解项目全貌、搭建环境、调试测试打包，并知道从哪里入手做二次开发。
> 使用层面的说明见 [README.md](README.md)。

## 1. 项目概览

MouseSim 是一个 Windows 桌面工具（.NET 8 / C# / WPF），面向办公自动化与批量重复操作：

- **录制回放**：低级钩子捕获真实的鼠标键盘操作，转成动作脚本，可编辑、可回放；
- **重复（脚本）**：手动编排动作序列，支持按次数或按时长重复；
- **连点器**：可配置的自动点击；
- **批量运行**：勾选多个脚本按顺序执行；
- **全局热键**：F6 开始 / F8 停止 / F9 录制（可改）；
- **悬浮状态卡**：录制/运行期间桌面右下角置顶显示进度，点击即停止。

**技术选型理由**（改架构前先读）：C#+WPF 是因为 Windows 原生输入注入（SendInput）和钩子（SetWindowsHookEx）靠 P/Invoke 直接调用最稳；Core/App 两层分离让核心逻辑可以无界面单测。

## 2. 目录结构与文件用途

```
mouse/
├── MouseSim.sln                  # 解决方案（Core + App + Tests 三工程）
├── publish.bat                   # 一键打包脚本（双击运行）
├── README.md                     # 用户文档
├── DEVELOPMENT.md                # 本文
│
├── src/MouseSim.Core/            # ★ 核心库：所有非界面逻辑，可独立单测
│   ├── Input/                    # —— 底层输入 ——
│   │   ├── Win32.cs              #   全部 P/Invoke 声明：SendInput、钩子、热键、消息循环等
│   │   ├── IInputSimulator.cs    #   输入模拟接口 + InputSource.OwnSendMarker（自注入标记，见 §9.4）
│   │   ├── SendInputSimulator.cs #   接口实现：点击/拖拽/滚轮/组合键/Unicode 文本输入
│   │   ├── MouseButton.cs        #   MouseButton 枚举 + KeyModifiers 修饰键（位定义与 Win32 MOD_* 对齐）
│   │   ├── KeyNames.cs           #   虚拟键码 → 显示名（"Ctrl+S" 之类界面文案用）
│   │   └── ScreenInfo.cs         #   光标位置查询 + 判断坐标是否在本程序窗口上（录制时过滤自身）
│   ├── Actions/                  # —— 动作模型（项目的灵魂）——
│   │   ├── IAction.cs            #   动作接口 + 多态 JSON 特性（$kind 判别）+ DelayAction + 类型标签表
│   │   ├── MouseActions.cs       #   移动 / 点击 / 拖拽 / 滚轮 四种动作
│   │   └── KeyboardActions.cs    #   组合键 / 按住 / 文本输入 三种动作
│   ├── Engine/                   # —— 执行引擎 ——
│   │   ├── ScriptRunner.cs       #   按序执行脚本：循环（次数/时长二选一）、每步延迟、可取消
│   │   ├── ClickerRunner.cs      #   连点循环
│   │   ├── BatchRunner.cs        #   批量：多脚本按序 + 整批循环（次数/时长）
│   │   └── PlaybackScaler.cs     #   回放速度缩放：按倍率生成缩放副本（等待/延迟/拖拽/按住）
│   ├── Recording/                # —— 录制 ——
│   │   ├── HookEvents.cs         #   钩子事件记录类型（MouseHookEvent / KeyHookEvent）
│   │   ├── GlobalInputHook.cs    #   WH_MOUSE_LL / WH_KEYBOARD_LL 低级钩子（专用消息线程）
│   │   └── MacroRecorder.cs      #   ★ 录制状态机：原始事件流 → 动作列表（双击合并/拖拽识别/滚轮累计/组合键/间隔→等待）
│   ├── Hotkeys/                  # —— 全局热键 ——
│   │   ├── HotkeyDefinition.cs   #   热键定义 + 显示格式化
│   │   └── GlobalHotkeyManager.cs#   RegisterHotKey 封装（专用消息线程，注册/注销请求线程安全投递）
│   ├── Models/
│   │   ├── Script.cs             #   脚本：名称/重复次数/重复时长(分钟)/每步延迟/动作列表
│   │   └── Clicker.cs            #   连点配置 + 位置模式枚举
│   └── Persistence/
│       ├── AppDataStore.cs       #   脚本与设置的 JSON 落盘（%APPDATA%\MouseSim），含序列化选项
│       └── AppSettings.cs        #   三个热键的配置模型
│
├── src/MouseSim.App/             # ★ WPF 界面层（只做展示与编排，不含业务规则）
│   ├── App.xaml(.cs)             #   入口：全局异常处理 + AppServices 组合根 + 日志(Log 类)
│   ├── Assets/app.ico            #   应用图标（由 tools/icon/make-icon.ps1 生成）
│   ├── Themes/Styles.xaml        #   全部界面样式：颜色、按钮、卡片(GroupBox)、列表、导航标签
│   ├── Infrastructure/           #   MVVM 基础设施
│   │   ├── ObservableObject.cs   #   INPC 基类
│   │   ├── RelayCommand.cs       #   命令（泛型/无参两种）
│   │   ├── Converters.cs         #   InverseBool / EnumToBool / 按键名显示 三个转换器
│   │   ├── EnumOption.cs         #   枚举下拉选项
│   │   ├── OptionItem.cs         #   非枚举下拉选项（分钟/小时）
│   │   ├── WpfKeys.cs            #   WPF Key 枚举 ↔ 界面显示名
│   │   ├── Ui.cs                 #   Ui.OnUi(回 UI 线程) + Ui.ShowMainWindow(停后弹窗)
│   │   └── HotkeyCaptureArbiter.cs#  热键录制协调：录新热键时通知注销旧热键（否则按键被吞，见 §9.5）
│   ├── Services/                 #   应用服务（界面与 Core 之间的胶水）
│   │   ├── AppServices.cs        #   ★ 组合根：创建一切服务并接线（热键→动作、悬浮卡路由、窗口回调）
│   │   ├── RunnerService.cs      #   唯一执行入口：同一时刻只允许一个任务；进度事件；用户停止事件
│   │   ├── RecorderService.cs    #   录制生命周期：装/卸钩子、录制器实例、实时计数
│   │   ├── FloatingStatusService.cs# 右下角悬浮卡内容编排（进度/倒计时）
│   │   └── StatusViewModel.cs    #   底部状态栏状态（线程安全写入）
│   ├── ViewModels/               #   页面逻辑（MVVM）
│   │   ├── MainViewModel.cs      #   标签页索引、F6 分发到当前页、标签索引常量
│   │   ├── RecorderViewModel.cs  #   录制页：开始/停止、结果预览、保存为脚本(可立即运行)
│   │   ├── ScriptViewModel.cs    #   重复页：脚本增删改/动作编排/重复方式(次数|时长)/批量勾选运行
│   │   ├── ClickerViewModel.cs   #   连点器页：参数与坐标拾取(5秒倒计时)
│   │   ├── SettingsViewModel.cs  #   设置页：热键录制/保存/恢复默认
│   │   └── ActionEditViewModel.cs#   动作编辑对话框：一个 VM 承载全部动作类型的字段超集
│   └── Views/                    #   XAML 视图
│       ├── MainWindow.xaml       #   主窗口：左侧导航标签 + 底部状态栏
│       ├── RecorderView / ScriptView / ClickerView / SettingsView   # 四个页面
│       ├── ActionEditWindow.xaml #   动作编辑对话框（按类型显示分区）
│       ├── InputDialog.xaml      #   通用文本输入框（重命名/新建/保存命名用）
│       ├── HotkeyCaptureBox.xaml#    热键录制控件（点按即录，Esc 取消）
│       └── FloatingStatusWindow.xaml# 悬浮卡（置顶/不抢焦点/整卡点击停止）
│
├── tests/MouseSim.Core.Tests/    # ★ xUnit 单元测试（只测 Core，45 个用例）
│   ├── FakeInputSimulator.cs     #   假输入模拟器（记录调用序列）+ DelegateAction（自定义测试动作）
│   ├── ScriptRunnerTests.cs      #   执行顺序/循环/取消/每步延迟/时长限制
│   ├── ClickerRunnerTests.cs     #   次数/固定坐标/间隔/双击/计数事件
│   ├── BatchRunnerTests.cs       #   顺序/整批循环/各自循环/取消/时长
│   ├── PlaybackScalerTests.cs    #   速度缩放：倍率换算/下限钳制/原脚本不被修改/运行器集成
│   ├── MacroRecorderTests.cs     #   录制状态机：单击/双击合并/拖拽/滚轮累计/组合键/间隔/自注入排除
│   ├── PersistenceTests.cs       #   脚本(含全部动作类型)与设置 JSON 往返
│   └── HotkeyFormatTests.cs      #   热键显示名格式化
│
└── tools/                        # 开发辅助工具（不进解决方案）
    ├── HookProbe/                #   钩子/录制链路诊断探针（排查录制问题时用，见 §5.4）
    └── icon/make-icon.ps1       #   应用图标生成脚本（改配色/比例后重跑）
```

## 3. 核心设计

### 3.1 动作模型（一切的核心）

所有可执行的操作统一为实现 `IAction` 的小类（8 种）：

```csharp
public interface IAction
{
    Task ExecuteAsync(IInputSimulator simulator, CancellationToken cancellationToken);
    string Describe();   // 界面列表展示用
}
```

- **录制器和手动编排产出同一种东西**：录制 = 事件流转动作；脚本页编辑 = 增删改动作列表。两种来源汇合后共用同一执行/存储/展示管线；
- 序列化用 System.Text.Json 多态（接口上 `[JsonDerivedType]` + `$kind` 判别字段），存为 `%APPDATA%\MouseSim\scripts\*.json`；
- **新增一种动作（如"找图点击"）是本项目的标准扩展路径**，见 §8.1。

### 3.2 线程模型（重要，改动前必须理解）

| 线程 | 职责 | 代码 |
|------|------|------|
| UI 线程 | 全部界面；StatusViewModel/各 VM 属性更新必须回此线程（`Ui.OnUi`） | WPF Dispatcher |
| 热键线程 | RegisterHotKey + GetMessage 循环；HotkeyPressed 事件在此触发 | GlobalHotkeyManager |
| 钩子线程 | 两个低级钩子的回调 + GetMessage 循环；Mouse/Keyboard 事件在此触发 | GlobalInputHook |
| 任务线程 | Task.Run 执行 ScriptRunner/ClickerRunner/BatchRunner；进度事件在此触发 | RunnerService.StartCore |

规则：**Core 的事件都在后台线程触发**，界面订阅处一律 `Ui.OnUi(...)` 包装；两个消息线程的安装/卸载请求用 PostThreadMessage 投递（保证钩子/热键函数只在 owning thread 上调用）。

### 3.3 录制管线

```
真实输入 ──► GlobalInputHook(WH_*_LL) ──► MacroRecorder(状态机) ──► List<IAction>
                                              │ 过滤规则：
                                              │ · ExtraInfo == OwnSendMarker → 跳过(自己回放的)
                                              │ · 本程序窗口上的鼠标事件 → 跳过
                                              │ · 热键按键(F6/F8/F9) → 跳过
                                              └ 识别规则：按下↔抬起小位移=单击；同位置短间隔两次=双击；
                                                大位移=拖拽(含真实时长)；连续滚轮=累计；修饰键+键=组合键；
                                                动作间隔≥60ms 记为等待(上限1分钟)
```

### 3.4 执行模型

`RunnerService` 是应用内**唯一**的任务入口（单任务模型：同一时刻只能有一个任务，停止热键/悬浮卡点击都走 `CancellationToken`）。三种任务（脚本/连点/批量）都经 `StartCore` 包装：进度事件、状态栏文案、悬浮卡、停止后弹主窗。

### 3.5 界面接线要点

- 主窗口 DataContext = MainViewModel；**每个页面 View 通过 `DataContext="{Binding Recorder}"` 等绑定各自的子 VM**（历史教训：曾因漏绑导致整页按钮无效，见 §9.1）；
- AppServices 是组合根：构造顺序 Runner → Recorder → Floating → 各 VM → Main → AttachRecorder（RecorderViewModel 与 MainViewModel 互相引用，靠 Attach 方法解循环）；
- 热键改名后按钮文案联动：StatusViewModel 持有 StartHotkeyText 等三个属性，ApplyHotkeysAsync 写入，各 VM 订阅变化刷新自己的按钮文案。

## 4. 环境搭建

1. **.NET 8 SDK**（本机已装于 `C:\Develop\.Net`，未加入 PATH）：
   - 下载安装：https://dotnet.microsoft.com/download/dotnet/8.0 （SDK 8.0.4xx，x64）
   - 验证：`C:/Develop/.Net/dotnet.exe --version` 应输出 8.0.x
2. **NuGet 源**：机器需有 nuget.org 源（`dotnet nuget list source` 检查；缺失则 `dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org`）。测试工程的 xunit 包需要联网还原；
3. IDE 任选：Visual Studio 2022（装".NET 桌面开发"工作负载）或 VS Code（装 C# Dev Kit），也可纯命令行开发；
4. 无其他依赖——WPF 与 P/Invoke 都在框架内，项目零第三方 NuGet 依赖（Core 与 App）；测试工程仅 xuint 全家桶。

**日常构建/运行命令**（注意 dotnet 要用全路径）：

```bash
cd F:/CodeProject/PersonalSpace/mouse
C:/Develop/.Net/dotnet.exe build MouseSim.sln                    # 构建
C:/Develop/.Net/dotnet.exe run --project src/MouseSim.App        # 运行（开发版）
C:/Develop/.Net/dotnet.exe test MouseSim.sln                     # 跑测试
```

## 5. 调试

### 5.1 基本方式

- **VS / VS Code**：打开 `MouseSim.sln`，F5 即可断点调试（WPF 项目直接支持）；
- **命令行 + 日志**：程序自带异常日志，位置 `%APPDATA%\MouseSim\logs\error.log`（界面未处理异常、后台任务异常都会落盘）。先看日志再看代码。

### 5.2 调试输入注入的注意事项

- **输入模拟是真实全局输入**——断点暂停时你的键盘鼠标事件、被调试程序注入的事件可能互相干扰；调试执行引擎逻辑优先用单元测试（FakeInputSimulator 不产生真实输入）；
- **单步调试钩子/热键要小心**：低级钩子回调若被断点卡住超过系统阈值（LowLevelHooksTimeout，约 300ms~1s），Windows 会静默摘除钩子，表现为"突然什么都录不到"。调试钩子问题用日志/探针（§5.4），别在钩子回调里下断点。

### 5.3 真实输入的手工验证清单

自动化测试无法覆盖真实输入，发布前按此清单人肉过一遍（也适用于排查用户反馈）：

1. 录制：记事本里单击/双击/拖选/滚轮/Ctrl+S → 停止 → 检查动作列表与预期一致；
2. 回放：保存并立即运行，记事本收到相同操作；
3. 时长重复：设 1 分钟，悬浮卡倒计时正确、到时自停；
4. 批量：勾 2 个脚本跑 2 轮，顺序与状态栏进度正确；
5. 悬浮卡点击停止、F8 停止、停止后主窗口自动前置；
6. 管理员权限：目标程序以管理员运行时，MouseSim 也需管理员（UIPI 限制，README 有说明）。

### 5.4 诊断工具 HookProbe

`tools/HookProbe` 是独立控制台工程（不进解决方案），用于排查"录制不到东西"类问题：

```bash
C:/Develop/.Net/dotnet.exe run --project tools/HookProbe
```

它安装真实钩子 → 用 SendInput 注入"模拟第三方输入"（中键点击 + F13）和"本程序自注入" → 输出录制结果。预期输出 3 个动作（点击/等待/按键）且自注入被排除；若不符，问题在钩子或录制状态机，配合 `MacroRecorderTests` 定位。

## 6. 测试

- **范围**：只测 MouseSim.Core（引擎、录制状态机、序列化、格式化），45 个用例，全部无真实输入、毫秒级跑完；
- **跑法**：`C:/Develop/.Net/dotnet.exe test MouseSim.sln`；
- **关键设施**（写新测试前先看这两个）：
  - `FakeInputSimulator`：实现 IInputSimulator，把每次调用记成字符串（`"move:10,20"`、`"press:Control:83"`），断言直接比对字符串序列；
  - `DelegateAction`：测试专用的自定义动作，可在第 N 次执行时取消令牌、抛异常等；
- **写新用例的套路**：
  1. 引擎类：构造 Script/ClickerConfig + FakeInputSimulator → RunAsync → 断言 Ops 序列或耗时；
  2. 录制状态机：按时间顺序喂 `OnMouse(new MouseHookEvent(...))` / `OnKey(...)` → `Stop()` 断言动作列表（注意时间戳参数是"开机毫秒数"，测试里自己编造递增值即可）；
  3. 序列化：构造对象 → JsonSerializer 序列化/反序列化 → 逐属性断言（用 `AppDataStore.JsonOpts`）；
- 界面层目前无自动化测试（WPF UI 测试成本高、收益低），靠 §5.3 手工清单把关。

## 7. 打包

### 7.1 一键打包（推荐）

双击项目根目录 **`publish.bat`**（或命令行 `cmd /c publish.bat`），产出两个单文件 exe 到 `publish/`：

| 产物 | 大小 | .NET 依赖 | 用途 |
|------|------|-----------|------|
| `MouseSim.exe` | ~70MB | **无**（自包含运行时） | 发给任何人、任何 Win10 x64/Win11 |
| `MouseSim-lite.exe` | <1MB | 需目标机已装 .NET 8 桌面运行时 | 内部机器/确定有运行时时 |

### 7.2 手动命令（脚本里就是这两条）

```bash
# 完整版（无 .NET 环境也能跑）
C:/Develop/.Net/dotnet.exe publish src/MouseSim.App -c Release -r win-x64 ^
  --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# 精简版（目标机需 .NET 8）
C:/Develop/.Net/dotnet.exe publish src/MouseSim.App -c Release -r win-x64 ^
  --self-contained false -p:PublishSingleFile=true -o publish/lite_tmp
move publish\lite_tmp\MouseSim.exe publish\MouseSim-lite.exe && rd /s /q publish\lite_tmp
```

注意：精简版必须先发到临时目录再改名——直接 `-p:AssemblyName=...` 会把属性传染到 ProjectReference 的 Core 工程，NuGet 报 "Ambiguous project name"。

### 7.3 图标

改图标设计：编辑 `tools/icon/make-icon.ps1`（纯 GDI+ 绘制，坐标即设计），运行后把 `app.ico` 拷到 `src/MouseSim.App/Assets/` 覆盖，重新打包即可。图标通过 csproj 的 `ApplicationIcon`（exe 文件图标）和 `Resource`（窗口标题栏）双路生效。

### 7.4 发布 Release 流程

1. 确认构建与测试全绿：`build` 0 错误 + `test` 全过；
2. **先关闭正在运行的 MouseSim**（文件锁会让打包失败，且中断的打包会留下损坏的半成品 exe——发现体积异常就删掉重跑）；
3. 双击 `publish.bat`，确认产出两个 exe 且完整版 ~68MB；
4. 打 tag 并推送：`git tag vX.Y.Z && git push origin vX.Y.Z`；
5. GitHub 网页 → Releases → Draft a new release → 选择已推送的 tag → 标题与说明（结构：新增 / 修复 / 下载表 / 文档链接，参考 v1.0.0 与 v1.1.0 的发布说明）→ **拖入两个 exe 附件** → Publish。

**版本号约定**（semver）：`主.次.修订`——不兼容改动才动主号，新功能加次号，纯修 bug 加修订号。测试版发 `vX.Y.Z-beta.N` 并在发布时**勾选 Pre-release**（不会成为 Latest，正式版发布后自动让位）。

### 7.5 Git 提交规范

- **message 格式**：`类型(范围): 中文描述`，类型取 `feat`（新功能）/ `fix`（修复）/ `docs` / `test` / `chore`（构建、工具、杂务）/ `refactor`；范围用 `core` / `app` 或省略。要点写在 message 正文（为什么/包含什么）；
- **粒度**：一次提交 = 一个完整可编译的改动单元（模块内聚），不要"顺手"把无关改动混进同一提交；
- **常用命令**：`git status`（看状态）→ `git add <文件>`（精确暂存，别无脑 `add .`）→ `git commit -m "fix(app): xxx"` → `git push`；`git log --oneline` 看历史；
- 编译产物（bin/obj/publish/）与 `.idea/` 已被 .gitignore 挡住，不会误传；
- 修完 bug 的提交已推送但内容有误时，个人仓库可 `git commit --amend` 后 `git push --force-with-lease` 修正（保持历史干净）。

## 8. 二次开发指南

### 8.1 新增一种动作类型（最常见扩展，以"等待窗口出现"为例）

1. `Core/Actions/` 新建类实现 `IAction`（属性带默认值 + Describe 中文描述）；
2. `IAction.cs` 接口上追加 `[JsonDerivedType(typeof(XxxAction), "xxx")]`（旧脚本兼容靠这个）；
3. `ActionTypeLabels.Of` 的 switch 加一行类型标签；
4. 界面四处接入：
   - `ScriptViewModel.ActionFactory.CreateDefault` 加默认值分支；
   - `ActionEditViewModel`：TypeOptions 加项、LoadFrom/BuildAction 加分支、必要时加可见性属性；
   - `ActionEditWindow.xaml` 加一个分区（Visibility 绑定）；
   - `ScriptView.xaml` 的"添加"菜单加一项 MenuItem；
5. 测试：`PersistenceTests` 的往返用例加该类型；有独立逻辑再加引擎用例。

### 8.2 常见改动入口

| 想做什么 | 去哪改 |
|----------|--------|
| 改界面配色/控件样式 | `Themes/Styles.xaml`（颜色都是资源字典顶部的 Brush） |
| 改默认热键 | `Persistence/AppSettings.cs` 三个 HotkeyDefinition + `SettingsViewModel` 恢复默认处 |
| 改录制规则（如间隔阈值） | `MacroRecorder` 顶部常量（MinGapMs=60、MaxGapMs=60000 等） |
| 改速度档位（标尺刻度） | `ScriptViewModel.SpeedValues`（UI 档位数组，与 SpeedNames 一一对应）；缩放规则在 `PlaybackScaler` |
| 改悬浮卡行为 | `Views/FloatingStatusWindow.xaml`（外观）+ `FloatingStatusService`（内容/路由） |
| 加新页面 | Views 加 UserControl → MainWindow 加 TabItem（记得 `DataContext="{Binding Xxx}"`！）→ MainViewModel 加属性与索引常量 |
| 改数据存储位置 | `AppDataStore.AppDataDir` |

### 8.3 提交前检查

```bash
C:/Develop/.Net/dotnet.exe build MouseSim.sln   # 0 警告 0 错误
C:/Develop/.Net/dotnet.exe test MouseSim.sln    # 全绿
```

构建报 "文件被 MouseSim.exe 锁定" → 先关掉正在运行的程序实例（开发中常忘）。

## 9. 踩坑记录（本项目真实踩过的坑，改相关代码前必读）

1. **页面 DataContext 必须显式接到子 VM**：主窗口 DataContext 是 MainViewModel，页面若不绑各自的子 VM，所有按钮静默失效（绑定错误只在 Output 窗口可见，不抛异常）。
2. **GroupBox 模板不能用 StackPanel**：StackPanel 给子级无限高度，内部 ListBox 永不出滚动条、会把内容整体挤出窗口。必须用 DockPanel（Styles.xaml 里有注释）。
3. **SetCursorPos 不经过低级钩子**：只有 SendInput（及真实硬件）输入进 WH_MOUSE_LL。诊断钩子时若用 SetCursorPos 模拟移动，会误判"钩子坏了"——用 HookProbe 的 SendInput 方式。
4. **自注入识别必须用 dwExtraInfo 魔数**（`InputSource.OwnSendMarker`），不能用 INJECTED 标志判断：很多鼠标驱动/改键软件的真实输入也带 INJECTED 标志，按标志过滤会把用户的一切操作录不进来（本项目最严重的一次线上问题）。
5. **RegisterHotKey 会全局吞键**：热键生效期间，设置页录制新热键时按下的键会被旧热键吃掉。解法见 `HotkeyCaptureArbiter`：进入录制先注销全部热键，退出再恢复。
6. **热键注册要逐个独立尝试**：一个失败不能中断其余注册，否则出现"一个冲突、全部失灵"（AppServices.ApplyHotkeysAsync 的 TryRegister 模式）。
7. **WPF 默认虚线焦点框**：按 Alt/Tab 会给焦点控件画虚线框。有两层来源：①FocusVisualStyle——所有交互控件样式里置 `{x:Null}`；②**主题模板内部画的焦点框**（Slider、CheckBox、RadioButton 都有）——属性压不掉，必须 `Focusable=False` 禁止键盘焦点根除（鼠标操作不受影响）。
8. **中文字符串/注释的编码坑**：C# 字符串里的中文引号必须用直引号包裹（弯引号""会截断字符串）；PowerShell 脚本含中文必须存 UTF-8 **带 BOM**，否则 PS5 按 GBK 解析、注释尾字节吞换行导致下一行代码被并入注释。
9. **WPF `{Binding}` 不能绑定静态字段**：ItemsSource 等绑定目标必须是**实例属性**，静态字段绑定会静默失败（控件为空但不报错）。数据想静态共享时用"静态数据 + 实例属性暴露"（参考 ActionEditViewModel.TypeOptions 的注释）；纯静态场景用 `{x:Static}` 扩展语法（参考 WpfKeys.SelectableKeys）。
10. **单文件打包改程序集名**：见 §7.2 的临时目录方案。
11. **权限边界（UIPI）**：模拟输入无法操作以管理员运行的程序，MouseSim 需同级提权；这是系统机制，代码层无解，用户文档要写明。

## 10. 路线图候选

按实现成本排序：窗口绑定定位（坐标随窗口移动，抵消分辨率变化）、图像识别找图（新增一种 IAction + 截图匹配）、托盘常驻/开机自启、脚本导入导出为单文件、相对坐标录制。新功能优先考虑"做成一种 Action"——管线现成（§8.1）。
