---
description: "此应用中每个新的或更改的UI（Blazor页面、对话框、组件）的绑定。这是CLAUDE.md引用的真实信息源。如果规则阻碍您，请停止并询问——不要发送违反规则的UI。基于plans/ui-overhaul.md。"
---

# UI 设计指南 — 强制

**此应用中每个新的或更改的UI**（Blazor页面、对话框、组件）的绑定。
这是`CLAUDE.md`引用的真实信息源。如果规则阻碍您，请停止并询问——不要发送违反规则的UI。基于`plans/ui-overhaul.md`。

## 1. 移动优先，始终

- **首先为360–430px手机编写，然后使用`min-width`媒体查询/MudBlazor断点属性向上增强。不要使用`max-width`覆盖进行桌面优先设计。**
- **在任何宽度320–1920px范围内都不出现水平滚动。** 如果内容宽度超过视口，这是一个错误。
- 触摸目标≥ **44px**（`var(--app-touch-target)`）。文本输入≥ 16px字体（阻止iOS对焦缩放）。
- 尊重凹口：使用`env(safe-area-inset-*)`；视口已设置`viewport-fit=cover`。
- 尊重`prefers-reduced-motion`——不要仅通过动画传达重要信息。

## 2. 设计令牌 — 无硬编码值

- 所有颜色/半径/间距来自**设计令牌**：MudBlazor主题（`Web/Components/Theme.cs`）+
  由`Web/Branding/BrandingCss.cs`发出的CSS自定义属性（`var(--app-primary)`、
  `--app-surface`、`--app-border`、`--app-text*`、`--app-radius`等）。
- **不要在组件或CSS规则中硬编码十六进制颜色、半径或品牌字符串。** 读取令牌。
  令牌来自白标`BrandingOptions`，所以经销商的调色板必须免费到达您的UI。
- 新品牌相关的值→添加令牌+品牌字段；不要内联它。

## 3. 响应式布局和数据

- **表在手机上折叠为卡片。** 每个`MudTable`设置`Breakpoint="Breakpoint.Sm"`，每个
  `MudTd`都有一个`DataLabel`。手机上没有原始宽表。（模板：`Components/Pages/Nodes.razor`。）
- 网格：`MudItem xs="12" sm="6" md="4"`——手机上全宽，向上多列。
- 表单在移动设备上为单列；大触摸目标；输入上的`inputmode`/`autocomplete`；金额/百分比的数字/小数inputmode。
- **为结构化输入使用正确的控件——不要使用原始文本框来输入数字或列表。** 使用正确的控件（`MudNumericField`、
  `MudDatePicker`、`MudSelect`、可编辑的添加/删除行列表或表）收集数字、金额、百分比、日期、枚举和任何多值数据，每个字段单独验证。单个自由文本`MudTextField`，用户必须输入逗号/空格/换行符分隔的blob，然后您解析它——这是**禁止的**：容易出错、未验证且在手机上不友好。**没有人想输入blob。** 多值输入是可编辑的类型化行列表（添加/删除），或从现有域数据加载（例如，直接从完成的回测运行检查，而不是重新输入其数字）。纯`MudTextField`仅用于真正的自由文本——名称、注释、搜索、描述。
- 在每个列表/详情上提供**加载、空和错误**状态——为移动设备调整大小。
- 移动**底部导航**（`Components/Layout/BottomNav.razor`）是主要的手机导航；分组抽屉是完整菜单。在那里添加高流量目的地；保持≤5项。

## 4. 对话框（创建/编辑）

- 所有添加/创建/编辑/新操作使用**MudBlazor对话框**（`IDialogService.ShowAsync<TDialog>`），不使用内联页面表单。对话框位于`Web/Components/Dialogs/`，公开`[Parameter]`，返回嵌套的
  `public sealed record …Result(...)`。列行操作（启动/停止/删除）保持为内联图标按钮。
- 在手机上，对话框应该是**全屏/全宽**且键盘感知的。

## 5. 内联帮助 — 每个控件

- 每个非显而易见的选项、选择、开关或操作都获得一个**`<HelpTip Text="…" />`**
  （`Components/HelpTip.razor`）——桌面上悬停，**手机上点击**。从`docs/`获取文本，使指导与行为保持同步；在同一提交中更新两者。

## 6. 白标

- 产品名称、徽标、描述、支持/公司、颜色、favicon都来自`BrandingOptions`。
  引用它们（`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`），不要使用文字"cMind"或品牌颜色。PWA清单、图标、theme-color和登录hero都是品牌化的。

## 7. PWA

- 该应用是可安装的。保持清单端点（`/manifest.webmanifest`）品牌化、图标存在
  （192/512/maskable+apple-touch）、服务工作者app-shell-only（不接触Blazor
  circuit/`_framework`/hubs），以及离线页面正常工作。新静态路由→保持清单`scope`。
- Blazor Server需要实时SignalR电路→**可安装+app-shell**，不是完全离线。不要承诺离线交互性。

## 8. 可访问性

- 输入上的标签、自定义控件上的`aria-*`、可见焦点、逻辑焦点顺序。因为主题是可白标的，请根据活动主题验证**对比度**，而不是固定调色板。

## 9. E2E — 没有UI在未测试的情况下发送（阻止）

每个面向用户的更改都在`tests/E2ETests`中发送Playwright E2E，像真实用户一样驱动，**在移动设备仿真**加桌面上：

- 新路由→将其添加到`PageSmokeTests` **和** `MobileLayoutTests`（渲染、底部导航、无错误UI）。
- 转换表/页面→将其路由添加到移动**无溢出**集合。
- 新流→现实的移动历程（创建/编辑/保存往返）**和**不愉快的路径
  （无效输入、空列表、每个角色的权限拒绝）。
- 新帮助提示→声明它在点击时打开（`HelpTipTests`模式）。
- 使用`AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync`（设备仿真）。
- `dotnet test`在"完成"之前绿色。仿真WebKit ≠ 移动Safari——真实设备门控是单独的发布步骤。

## 10. 完成定义（UI）

- [ ] 移动优先；320–1920px范围内无水平溢出；触摸目标≥44px。
- [ ] 仅限设计令牌——零硬编码颜色/半径/品牌字符串。
- [ ] 表→手机上的卡片（`DataLabel` + `Breakpoint.Sm`）；存在加载/空/错误状态。
- [ ] 结构化输入使用正确的验证控件（数字/日期/选择/可编辑行列表）——不使用原始文本框，用户键入分隔的数字/值blob。
- [ ] 通过对话框创建/编辑；在移动设备上全屏。
- [ ] 每个控件都有一个从docs采购的`HelpTip`。
- [ ] 尊重白标+PWA。
- [ ] 添加移动+桌面E2E（烟雾、无溢出、历程、不愉快路径）；`dotnet test`绿色。
- [ ] Rider `get_file_problems` + `dotnet format analyzers`在触摸文件上干净。
