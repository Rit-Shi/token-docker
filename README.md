# Token Bar

Windows 桌面额度浮条：智谱 GLM、DeepSeek、Kimi、MiniMax、小米 MiMo 的套餐余量、余额和今日用量。

![Token Bar](assets/tokenbar.png)

## 功能

- 顶部主额度环：外环 5 小时、内环周额度（默认 GLM，可在设置中换主供应商）；悬停查看数值。
- 其余已连接的供应商逐行列出：图标、名称、主数值；套餐类供应商带小双环，浮条高度随行数变化。
- 额度颜色从绿色连续过渡到黄色、红色，转黄位置默认在剩余 50%，可在设置中调整。
- DeepSeek 显示余额和今日花费，悬停查看今日累计 token。
- 今日人民币花费从 0 到 5 元由绿渐变至红，超过 5 元为深红；预警、告警金额默认 2.5 和 5 元，可在设置中调整。
- 左右边缘竖向吸附、顶部横向吸附，圆角半透明外观、吸附动画、深浅主题。
- 任务栏模式：在任务栏上方贴一条细横条，把所有供应商平铺成一行；可横向拖动，右键有菜单，点击某家会在浮条上打开它的详情。两种模式互斥——横条显示时浮条自动收起，隐藏浮条后横条恢复。
- 主窗口不占任务栏，金币 t 图标位于系统托盘；右键可显示、隐藏、刷新、设置和退出。

托盘图标是否折叠到“隐藏的图标”由 Windows 设置决定。半透明外观不是系统背景模糊。

## 支持的供应商

| 供应商 | 数据 | 鉴权 |
| --- | --- | --- |
| 智谱 GLM | 5 小时 / 周套餐额度 | 套餐 API Key |
| DeepSeek | 余额（API Key）、今日花费与 token（控制台） | API Key 或网页登录 |
| Kimi | 开放平台余额，或 Kimi Code 5h / 周 / 月订阅额度 | API Key |
| MiniMax | M Plan 5 小时 / 周额度，或开放平台余额 | API Key |
| 小米 MiMo | 月度 Token Plan 额度与余额 | 网页登录（控制台接口只认会话） |

Kimi / MiniMax 的 Key 会按前缀自动判断用余额还是套餐接口（`sk-kimi-`、`sk-cp-` 走套餐，`sk-api-` 及其他走余额），也可以在设置里强制指定“余额”或“套餐”。国际站可在设置里勾选。

展示方式有两种：**逐行列表**（默认，浮条随供应商数量变高）和**轮换**（只显示一个，点击该行切换）。设置里还能勾选要显示的供应商，取消勾选只是隐藏，不会删除 Key。

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

通过浮条或托盘的“连接设置”填写各家的 Key；国际站用户勾选对应行的“国际站”。GLM 需要套餐 API Key，DeepSeek/Kimi/MiniMax 用账户 API Key。每行 Key 输入框右侧的“复制”按钮会把该 Key（包括尚未保存的改动）放进剪贴板，方便粘到别处；剪贴板内容不会被自动清除。

小米 MiMo 比较特殊：控制台的余额和 Token Plan 接口只认网页登录会话，不接受 API Key。在设置里点“连接 MiMo 控制台”，在弹出窗口登录小米账号后会自动同步；`mimo-profile/` 保存登录状态，会话过期时重新登录即可。

同一窗口的“配色阈值”可以调整额度警戒线（剩余百分比，默认 50）和今日花费的预警、告警金额（默认 2.5 / 5 元）。“恢复默认阈值”会填回默认值；保存时校验范围，额度警戒线需要 1–100，告警金额必须大于预警金额。

“展示”里的“在任务栏上方显示用量条”会另开一条细横条（任务栏模式，开启后浮条自动隐藏），按启用的供应商平铺一行：套餐类供应商的数值按剩余额度着色，能一眼看出谁快用完了。横条不会抢焦点、平时鼠标穿透，只有光标移到上面才接收点击；位置可以横向拖动并记住。托盘菜单或浮条右键菜单里的“任务栏模式”也能开关。

DeepSeek 可通过 API Key 查询余额。要显示今日花费和 token，使用浮条菜单“连接 DeepSeek 控制台”，在官方网页中登录。关闭登录窗口会隐藏窗口并保留会话。统计取自控制台，按本机时区的今日范围累计，无需读取 pi、opencode 或 dsh 的日志。

Kimi 和 MiniMax 的余额接口不返回用量，浮条上的“今日 ≈¥x.xx”是按**当日余额差**估算的：每天第一次刷新记下基线，之后按余额下降累计，充值会把基线抬高、不会显示负数；数据存在程序旁边的 `daily.json`。DeepSeek 接控制台时仍然是官方真实用量，不用估算。\r\n\r\n每 5 分钟刷新，也可手动刷新。控制台内部接口可能变化；查询失败、过期或缺失的数据不应被视为零用量，请以官方控制台为准。

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

本项目代码采用 [MIT 许可](LICENSE)。智谱、DeepSeek、Kimi 和 MiniMax 图标来自 LobeHub Icons，适用其 [MIT 许可](licenses/LobeHub-LICENSE.txt)；品牌名称和标识归各自权利人所有。本项目非官方客户端。WebView2 适用 [Microsoft 许可](licenses/WebView2-LICENSE.txt)。
