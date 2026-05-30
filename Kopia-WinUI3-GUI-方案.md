# Kopia WinUI3 GUI 与一体化安装方案

## 目标

为开源备份工具 Kopia 构建 Windows 原生 GUI，并提供一个安装包，让用户一次安装 GUI 与 Kopia 本体。

## 调研结论

- Kopia 主仓库是 `kopia/kopia`，协议为 Apache-2.0，官方定位是跨平台备份/恢复工具，支持 CLI 和 GUI。
- 截至 2026-05-30，GitHub 最新稳定版本为 `v0.23.0`，发布日期为 2026-05-12。
- 官方 Windows GUI 安装包 `KopiaUI-Setup-X.Y.Z.exe` 已经包含 `kopia` 二进制文件；官方文档说明 KopiaUI 会按需运行 `kopia` 命令。
- 官方当前桌面 GUI 是 Electron 应用。官方构建说明写明：KopiaUI 是一个壳，启动 `kopia server --ui` 并连接到它，同时把 `kopia` 可执行文件嵌入到 App 资源里。
- WinUI3/Windows App SDK 可走 MSIX 或非打包模式。若想做传统 `.exe` 安装体验，推荐使用非打包、自包含发布，再用 WiX/Advanced Installer/Inno Setup 封装 GUI、`kopia.exe`、运行时与快捷方式。

参考链接：

- https://github.com/kopia/kopia
- https://kopia.io/docs/installation/
- https://github.com/kopia/kopia/blob/master/BUILD.md
- https://github.com/kopia/kopia/releases/tag/v0.23.0
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/

## 推荐路线

第一版不要直接重写全部 Kopia UI。建议先做一个 WinUI3 原生壳，替代 Electron 壳：

1. 安装包内带上 `KopiaWinUI.exe` 与 `kopia.exe`。
2. WinUI3 启动后寻找内置 `kopia.exe`。
3. GUI 后台启动 `kopia server --ui`，绑定到 `127.0.0.1` 的本地端口。
4. 使用 WinUI3 + WebView2 承载 Kopia 已有 Web UI。
5. 用 WinUI3 做原生托盘、窗口管理、更新入口、日志查看、安装状态、诊断页。
6. 第二阶段再逐步把常用页面原生化，例如仓库连接、快照、策略、恢复、维护任务。

这样可以先获得安装和桌面体验的提升，同时避免从第一天就追赶 Kopia 全部业务能力。

## 架构

```text
KopiaWinUI.exe
  ├─ WinUI3 Shell
  │   ├─ 主窗口 / 导航
  │   ├─ 系统托盘
  │   ├─ 设置页
  │   ├─ 日志与诊断页
  │   └─ WebView2
  │
  ├─ Kopia Process Manager
  │   ├─ 查找内置 kopia.exe
  │   ├─ 分配本地端口
  │   ├─ 启动 kopia server --ui
  │   ├─ 健康检查
  │   ├─ 退出时关闭进程
  │   └─ 崩溃重启与日志收集
  │
  ├─ Kopia API Adapter
  │   ├─ MVP: WebView2 访问本地 Web UI
  │   ├─ 后续: CLI JSON/文本适配
  │   └─ 后续: gRPC/HTTP API 客户端
  │
  └─ Bundled Runtime
      ├─ kopia.exe
      ├─ WebView2 运行时检测或离线安装包
      └─ Windows App SDK 自包含依赖
```

## 技术选型

### GUI

- C# + .NET 8/9 + WinUI3 + Windows App SDK。
- MVVM：CommunityToolkit.Mvvm。
- WebView：Microsoft.Web.WebView2。
- 日志：Serilog 或 Microsoft.Extensions.Logging。
- 后台进程：`System.Diagnostics.Process`，封装成 `IKopiaProcessService`。

### Kopia 集成

优先级建议如下：

1. MVP 使用 `kopia server --ui` + WebView2，复用官方 Web UI。
2. 常用操作通过 `kopia.exe` 子进程调用补齐，例如版本检测、仓库状态、日志诊断。
3. 中长期再研究 Kopia server API，做原生页面和更好的状态同步。

注意：Kopia 的配置默认在 `%APPDATA%\kopia\repository.config`，密码存储在 Windows Credential Manager。不要把仓库密码写入应用自己的明文配置。

## 安装包方案

### 推荐：传统 EXE/MSI 安装器

适合官网直链下载、企业内部分发、希望“一次点击安装 GUI 和本体”的场景。

建议使用 WiX Toolset v4 + Burn Bootstrapper：

- 输出文件：`KopiaWinUI-Setup-x64.exe`
- 安装目录：`%ProgramFiles%\KopiaWinUI`
- 包含：
  - `KopiaWinUI.exe`
  - `bin\kopia.exe`
  - WinUI3 自包含依赖
  - WebView2 Runtime Evergreen Bootstrapper 或离线 Runtime
  - LICENSE、NOTICE、第三方许可证
- 安装动作：
  - 创建开始菜单快捷方式
  - 可选创建桌面快捷方式
  - 可选添加 `bin` 到 PATH
  - 写入卸载项
  - 校验 `kopia.exe --version`
- 卸载动作：
  - 删除程序文件
  - 默认保留 `%APPDATA%\kopia` 与用户备份配置
  - 提供“删除用户配置”的高级选项，不默认执行

### 备选：MSIX

适合 Microsoft Store 或需要包身份的场景。

优点：

- 安装/卸载干净。
- 支持包身份、自动更新、Store 分发。
- WinUI3 默认路径更顺。

代价：

- 签名与证书分发更麻烦。
- 对 CLI 本体暴露、PATH、外部进程和企业环境的控制不如传统安装器直接。

## 文件布局建议

```text
src/
  KopiaWinUI/
    KopiaWinUI.csproj
    App.xaml
    MainWindow.xaml
    Services/
      KopiaLocator.cs
      KopiaProcessService.cs
      KopiaVersionService.cs
      LocalPortService.cs
    ViewModels/
    Views/

installer/
  wix/
    Bundle.wxs
    Product.wxs

third_party/
  kopia/
    kopia.exe
    checksums.txt
    LICENSE
```

## MVP 功能清单

- 启动 WinUI3 主窗口。
- 首次启动检测 `kopia.exe` 是否存在。
- 读取 `kopia.exe --version` 并显示。
- 自动选择本地端口并启动 `kopia server --ui`。
- WebView2 打开本地 Kopia UI。
- 退出 GUI 时优雅关闭 Kopia 子进程。
- 系统托盘：打开、隐藏、退出。
- 日志页：显示 GUI 日志、Kopia stdout/stderr、当前端口、二进制路径。
- 安装器：单个 `.exe` 安装 GUI 和 `kopia.exe`。

## 风险与处理

- 端口冲突：启动前动态分配本地端口，不写死端口。
- 进程残留：保存 PID，退出时先发正常关闭，再超时 kill。
- 权限问题：默认不以管理员权限运行 GUI；只有安装器需要管理员权限。
- 密码安全：不接管 Kopia 密码，不保存仓库密码。
- 版本漂移：CI 下载指定 Kopia release，并校验 SHA256。
- WebView2 缺失：安装器检测并安装 WebView2 Runtime。
- Windows 长路径：Kopia v0.23.0 release notes 中有 Windows `MAX_PATH` 相关修复，应保留最新上游版本策略。

## 第一阶段实施步骤

1. 建立 WinUI3 项目骨架。
2. 加入 `KopiaProcessService`，完成版本检测和进程启动。
3. 接入 WebView2，加载本地 `kopia server --ui` 地址。
4. 加入托盘与关闭行为。
5. 写 WiX 安装器，打包 GUI 与 `kopia.exe`。
6. 加入构建脚本：下载 Kopia release、校验 checksum、复制到 `third_party/kopia`。
7. 在 Windows 10/11 x64 上验证安装、启动、备份仓库连接、卸载。

## 结论

最稳妥的方案是“WinUI3 原生壳 + 内置 Kopia CLI + WebView2 复用官方 UI + WiX 一体化安装器”。它能最快替换 Electron 安装体验，并保留后续逐步原生化的空间。
