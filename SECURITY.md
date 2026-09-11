# 隐私与安全

## 本地存储

- API Key 与应用设置保存在可执行程序旁的 `settings.dat`，由 Windows DPAPI `CurrentUser` 加密。它不是明文，但同一 Windows 用户下的程序仍可能解密；不防御已控制该账户的恶意软件。
- DeepSeek 登录会话由 WebView2 保存在程序旁的 `console-profile/`。其中可能包含 Cookie、LocalStorage、缓存及浏览器管理的其他登录数据。应用没有对整个目录统一加密，必须视为敏感资料。
- 应用没有自行保存控制台密码。网页桥接在页面内存中使用授权令牌，只向本机应用传递筛选后的用量和余额，不额外持久化授权令牌。
- `connection-status.txt` 记录连接状态、接口错误等诊断信息；代码不主动记录 API Key 或 Authorization。日志仍应留在本机，分享前检查内容。

## 网络

API Key 用于向对应提供商的官方 HTTPS 接口查询额度或余额。DeepSeek 控制台页面在 WebView2 内加载，其自身的网络请求和存储受官方网页控制。项目没有增加独立遥测服务。

## 发布前

只提交源码、静态资源、测试、构建脚本和文档。不要上传正在使用的 `outputs/` 或 `build/` 目录、浏览器资料、环境变量文件、HAR 抓包、真实账单和账户截图。构建依赖从 NuGet 恢复，不把本机 SDK 和 DLL 加入源码仓库。

`.gitignore` 无法阻止 `git add -f`，也不会移除已跟踪的文件。发布前检查 `git diff --cached` 和 `git ls-files`；若密钥曾被公开，首先在提供商侧撤销或轮换，删除文件不足以清除 Git 历史。

发现问题时，请勿在公开 issue 中附带凭据或个人会话资料。仅提供脱敏的复现步骤；敏感漏洞可在仓库开启私密安全报告后使用该渠道。
