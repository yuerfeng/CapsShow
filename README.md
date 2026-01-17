# CapsShow

一个基于 .NET 的轻量级 Caps Lock 状态提示工具，在按下 Caps Lock 时在屏幕中间弹出圆角半透明提示窗，并提供托盘图标方便退出。

> 适合经常在大小写之间切换、又不想被系统自带提示打断工作流的用户。

## 功能特性

- 全局 Caps Lock 监听
  - 使用 Windows 低级键盘钩子（WH\_KEYBOARD\_LL），无需激活窗口也能捕获按键
- 中间弹出的 Toast 提示
  - 固定 200×200 窗口，圆角、半透明黑色背景（Opacity = 0.7）
  - 顶部显示自定义 logo（`logo.png`），底部显示当前状态文字
  - 文案示例：`当前状态：大写` / `当前状态：小写`
  - 自动在短时间后关闭（默认 1.5 秒）
- 托盘图标 & 退出菜单
  - 支持自定义托盘图标（`main.ico`）
  - 托盘右键菜单提供“退出”选项，随时结束程序
- 纯 WinForms 实现
  - 无主窗口，程序常驻后台，占用资源极少

## 环境要求

- Windows 10 / 11
- [.NET SDK 8.0](https://dotnet.microsoft.com/) 或更高版本

## 获取与运行

```bash
git clone https://github.com/<your-name>/CapsShow.git
cd CapsShow

# 还原依赖（一般首次即可）
dotnet restore

# 调试运行
dotnet run

# 或发布为独立可执行文件
dotnet publish -c Release -r win-x64 --self-contained false
```

发布完成后，可在：

- `bin/Release/net8.0-windows/win-x64/publish/`

找到 `CapsShow.exe` 并直接运行。

## 使用说明

1. 启动 `CapsShow.exe` 后，程序会最小化到托盘，不会弹出主窗口
2. 每次按下 Caps Lock：
   - 屏幕中间会弹出一个圆角黑色小窗口
   - 若当前为大写状态：显示 `当前状态：大写`
   - 若当前为小写状态：显示 `当前状态：小写`
3. 提示窗口会在约 1.5 秒后自动消失
4. 退出程序：
   - 在任务栏托盘区域找到 CapsShow 图标
   - 右键点击图标，选择“退出”

## 自定义图标与样式

### 托盘图标 / 程序图标

- 项目根目录下的 `main.ico` 用作：
  - 托盘图标
  - 应用程序图标（通过 `CapsShow.csproj` 中的 `<ApplicationIcon>` 配置）
- 若要更换图标：
  1. 使用图标工具（或在线转换）将你的 PNG 转为标准 ICO 文件
  2. 覆盖项目根目录下的 `main.ico`
  3. 重新构建项目

### Toast 中的 Logo

- 项目根目录下的 `logo.png` 会在构建时复制到输出目录
- Toast 顶部使用该图片，缩放居中显示
- 若要更换：
  - 替换 `logo.png` 即可（保持文件名不变）

### 样式调整（代码级）

相关代码主要集中在：

- 入口与应用上下文：`Program.cs` 中的 `Program` 和 `CapsApplicationContext`
- 低级键盘钩子：`KeyboardHook`
- 提示窗口：`ToastForm`

你可以按需调整：

- 窗口尺寸（默认 `200×200`）
- 显示时间（`System.Windows.Forms.Timer.Interval`）
- 透明度（`Form.Opacity`）
- 圆角半径与配色（`ToastForm` 中的绘制逻辑）

## 安全性说明

- 键盘钩子仅用于监听 Caps Lock 键，不会记录或上传任何按键内容
- 项目内没有网络通信逻辑，不会主动访问互联网

## 贡献

欢迎通过以下方式参与：

- 提交 Issue：反馈 Bug 或提出新功能建议
- 提交 Pull Request：修复问题、改进代码或完善文档

建议在提交 PR 前先开一个 Issue 进行简单讨论，方便对齐实现思路。

## 开源协议

目前尚未在仓库中明确指定开源协议。  
在正式开源前，请根据你的需求选择合适的 License（如 MIT、Apache-2.0 等），并在仓库中添加对应的 `LICENSE` 文件。

