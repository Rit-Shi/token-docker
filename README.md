# Token Bar

Windows 桌面额度浮条：智谱 GLM 套餐余量和 DeepSeek 余额、今日用量。

![Token Bar](assets/tokenbar.png)

## 功能

- GLM 外环显示 5 小时剩余额度，内环显示周剩余额度；悬停查看数值。
- 额度颜色从绿色连续过渡到黄色、红色。
- DeepSeek 显示余额和今日花费，悬停查看今日累计 token。
- 今日人民币花费从 0 到 5 元由绿渐变至红，超过 5 元为深红。
- 左右边缘竖向吸附、顶部横向吸附，圆角半透明外观、吸附动画、深浅主题。
- 主窗口不占任务栏，金币 t 图标位于系统托盘；右键可显示、隐藏、刷新、设置和退出。

托盘图标是否折叠到“隐藏的图标”由 Windows 设置决定。半透明外观不是系统背景模糊。

## 构建与运行

需要 Windows x64、.NET Framework 4.8，以及 Microsoft Edge WebView2 Runtime。构建脚本使用系统 C# 编译器，首次构建从 NuGet 下载固定版本的 WebView2 SDK。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Test
```

运行 `build/TokenBar-Tray.exe`。请将程序放在当前用户可写的目录；设置和浏览器会话保存在程序旁边。已有本地用户可继续使用原来的 `outputs/TokenBar-Tray.exe`，整理仓库不会迁移或清除其登录资料。

SDK 已缓存时可加 `-Offline`。JavaScript 桥接测试只需要 Node.js，无第三方依赖：

```shell
node tests/test-console-bridge.cjs
```

图标已提交，不需要重新生成。若要修改，可编辑 `assets/tokenbar.svg`，安装 `sharp` 后运行 `node scripts/build-icon.cjs`。

## 连接

通过浮条或托盘的“连接设置”填写 GLM 套餐 API Key；国际站用户勾选 Z.ai。

DeepSeek 可通过 API Key 查询余额。要显示今日花费和 token，使用浮条菜单“连接 DeepSeek 控制台”，在官方网页中登录。关闭登录窗口会隐藏窗口并保留会话。统计取自控制台，按本机时区的今日范围累计，无需读取 pi、opencode 或 dsh 的日志。

每 5 分钟刷新，也可手动刷新。控制台内部接口可能变化；查询失败、过期或缺失的数据不应被视为零用量，请以官方控制台为准。

## 目录

| 路径 | 内容 |
| --- | --- |
| `src/` | WPF 界面、提供商解析、WebView2 控制台桥接 |
| `assets/` | SVG 图标、应用 ICO 和 PNG |
| `scripts/` | 构建和图标生成脚本 |
| `tests/` | 解析、颜色、布局及控制台桥接回归检查 |
| `licenses/` | 第三方许可 |
| `build/`、`.deps/` | 本地生成物与依赖缓存，不提交 |
| `outputs/`、`work/` | 开发期间的本地运行资料和临时文件，不提交 |

## 隐私与贡献

详见 [SECURITY.md](SECURITY.md)。不要提交 API Key、`settings.dat`、`console-profile/`、网络抓包或真实账户截图；不要直接压缩正在使用的程序目录上传。`.gitignore` 不会保护已经被跟踪的文件，提交前仍须检查暂存区。

测试使用虚构数据，不需要真实账户。真实桌面的托盘、窗口交互和登录流程需要在 Windows 用户会话中验证。

本项目代码采用 [MIT 许可](LICENSE)。智谱和 DeepSeek 图标来自 LobeHub Icons，适用其 [MIT 许可](licenses/LobeHub-LICENSE.txt)；品牌名称和标识归各自权利人所有。本项目非官方客户端。WebView2 适用 [Microsoft 许可](licenses/WebView2-LICENSE.txt)。
