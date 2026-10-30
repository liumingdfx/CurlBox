# CurlBox

一个面向 Windows 的轻量原生 cURL / HTTP 请求工具。粘贴 cURL 即可发送请求，也可以像 Postman 一样创建请求、保存到目录，并在请求库中再次打开或直接发送。

- 原生 WinForms，.NET Framework 4.8，不是 Electron
- 默认驻留托盘，内存占用小
- 所有请求都通过本机 `curl.exe` 发出，不经过第三方中转服务

当前版本：`1.0.0`

- 仓库：<https://github.com/liumingdfx/CurlBox>
- 下载：<https://github.com/liumingdfx/CurlBox/releases/latest>

## 功能

- 粘贴 cURL：支持 bash / cmd / PowerShell 复制格式，支持任意 HTTP 方法
- 请求构建器：GET / POST / PUT / PATCH / DELETE / HEAD / OPTIONS
- URL、查询参数、请求头、请求体（无 / JSON / 表单 / 文本）
- 请求库：命名保存、目录管理、打开、直接发送
- 目录级自动 Token：
  - 只对路径以 `/backend/` 开头的请求生效
  - 在请求库目录上单独开启 / 关闭
  - 自动补 Authorization、缺少 Token 时自动登录、响应 401 时重新登录并重试一次
- 响应查看：JSON 格式化、中文 `\uXXXX` 转义解码、GBK 解码、图片预览
- 左右分栏可拖动，浅色主题，托盘图标和全局快捷键 `Ctrl+Alt+C`
- 可选开机启动（写入当前用户 HKCU，默认关闭）

## 下载与安装

### 安装版

1. 从 Releases 下载 `CurlBox-Setup-1.0.0.exe`。
2. 运行安装器，选择安装位置。
3. 安装后，设置、历史和请求库保存在：

   ```text
   <安装目录>\data
   ```

安装器会创建快捷方式，并注册当前用户卸载项。覆盖安装会保留 `data`。卸载时默认保留数据，也可以选择一并删除。

默认安装目录是 `%LOCALAPPDATA%\Programs\CurlBox`。请选择有写入权限的目录；如果要继续使用当前 D 盘的数据，可以把安装目录选择为 `D:\CurlBox`。

### 免安装版

下载 `CurlBox-1.0.0.exe` 后直接运行。程序会把数据放在 EXE 同级的 `data` 目录。

### 运行环境

- Windows 10 / 11
- .NET Framework 4.8（Windows 10/11 通常已自带）
- 系统 `curl.exe`（Windows 10 1803 及以上自带）

## 自动 Token 使用方式

1. 打开「设置 → 自动 Token 登录设置…」，填写：
   - 登录接口 URL
   - 用户名
   - 密码
   - 固定验证码 / code
2. 进入「请求库」，在每个目录行右侧打开「自动开」。
3. 打开或发送该目录下的请求。未保存请求和直接粘贴的 cURL 按「未分组」目录的开关处理。

行为和约束：

- 登录接口需要返回：

  ```json
  { "data": { "token": "..." } }
  ```

- 程序会自动加 `Bearer ` 前缀（如果返回值已经带 `Bearer `，不会重复添加）。
- 只对 `/backend/` 路径生效；其他请求即使目录开关开启也不会执行自动登录。
- 返回 HTTP 401，或响应 JSON 的 `code` 为 `"401"` 时，会重新登录并重试一次，避免无限循环。
- 系统只保留一份登录配置和一个 Token；目录开关只控制哪些目录启用这套逻辑。

## 数据与隐私

程序没有遥测、统计上报或自动更新，也不会把数据上传到开发者服务器。请求直接发往你填写的目标地址；自动 Token 的登录请求发往你配置的登录接口。

数据目录默认在安装位置下，可能包含：

| 文件 | 内容 |
| --- | --- |
| `settings.json` | 界面配置、默认请求、自动 Token 的登录地址、账号、密码和 Token |
| `history.json` | 最近发送过的 cURL 命令 |
| `requests.json` | 请求库、目录以及每个目录的自动 Token 开关 |
| `tmp/` | 请求执行期间的临时文件，正常结束后自动清理 |

请注意：

- 密码、Token、请求头、Cookie 等内容都是**本机明文存储**，没有额外加密。
- `settings.json`、`history.json`、`requests.json` 可能包含敏感凭据，不要提交到 Git、发给别人或放进截图。
- 仓库的 `.gitignore` 已排除 `data/`、设置、历史和请求库文件。
- 如果电脑是多用户共享或安装目录可被其他用户读取，建议把程序安装在仅自己可访问的目录。

## 风险提示（为什么会被 SmartScreen / 杀毒软件拦截）

本项目**没有代码签名证书**，这是安全提示最主要的原因。除此之外，程序本身的一些正常行为也可能触发启发式扫描。

| 提示或现象 | 可能原因 |
| --- | --- |
| SmartScreen 提示“Windows 已保护你的电脑” | EXE 未签名、发布者未知、下载量/声誉不足，下载文件还带有 Mark-of-the-Web |
| 杀毒软件报通用木马/可疑程序 | 会启动系统 `curl.exe`、生成临时配置和请求体文件、在安装目录写配置，安装器还会复制 EXE、写注册表和快捷方式，这些行为与恶意程序的部分行为相似 |
| 安装时提示风险 | 安装器包含并释放 `CurlBox.exe`，会创建 `data`、卸载器和 HKCU 卸载项；这是安装器的正常行为，但可能被误判 |
| 自动登录相关告警 | 自动 Token 会向配置的登录接口发送账号密码，保存 Token，并可能自动重试请求；这是功能需要，也意味着必须使用可信的登录地址 |
| 托盘、热键、开机启动告警 | 程序使用托盘、全局热键；如果手动开启开机启动，还会写 HKCU `Run` 注册表项 |

项目不包含远程代码加载、静默更新或隐藏下载功能；所有源码都可在本仓库审计。

## SmartScreen / 误报处理

1. 优先从本仓库 Releases 下载，并核对 SHA256：

   ```powershell
   Get-FileHash .\CurlBox-Setup-1.0.0.exe -Algorithm SHA256
   ```

   与 Release 附件 `SHA256SUMS.txt` 对比。

2. 如果文件来自网络且被锁定，可在文件属性中点击“解除锁定”，或执行：

   ```powershell
   Unblock-File .\CurlBox-Setup-1.0.0.exe
   ```

3. SmartScreen 可在确认来源和哈希后点击“更多信息 → 仍要运行”。这只适用于你信任的构建。

4. 杀毒软件隔离时，先确认检测名称和文件路径；只有确认是自编译或 Release 中的文件后，才选择还原/允许。**不建议关闭 Windows Defender 或 SmartScreen。**

5. 如果确认是 Defender 误报，可以向 Microsoft 提交误报样本。

### 彻底减少警告：代码签名

公开分发最可靠的方式是使用代码签名证书：

- 购买 OV / EV 代码签名证书，或使用 Microsoft Azure Trusted Signing
- 用同一证书签名 `CurlBox.exe` 和安装器 `CurlBoxSetup.exe`，并加时间戳
- 后续版本保持同一发布者签名，逐步建立 SmartScreen 声誉

自签名证书只能改善本机提示（需要把证书加入本机“受信任的根证书”和“受信任的发布者”），其他用户下载仍可能看到警告；不建议为了绕过提示而随意安装来源不明的根证书。

## 安全使用建议

- 自动 Token 的登录接口尽量使用 HTTPS，并确认域名属于你的项目。
- 自动登录账号使用最小权限，不要使用权限过高的管理员账号。
- 定期更换 Token / 密码；不再使用的 Token 可在自动 Token 设置里清除。
- 不要勾选“忽略 TLS 证书”，除非你明确知道目标服务是本地测试环境。
- `--verbose` 日志可能包含请求头，截图或分享日志前先检查。
- 不要分享 `data` 目录；如果必须提供日志，先删除 Authorization、Cookie、Token 和密码字段。
- 使用 `--insecure`、自动登录、开机启动等功能前，确认它们符合你所在环境的安全要求。

## 从源码构建

构建只需要 Windows 自带的 .NET Framework 4.x 编译器，不需要 Visual Studio、Node.js 或额外 SDK。

```bat
build.cmd
installer\build-installer.cmd
```

当前脚本的输出位置：

- `build.cmd`：生成 `CurlBox.exe` 到源码目录的上一级
- `installer\build-installer.cmd`：先生成主程序，再生成 `CurlBoxSetup.exe` 到与主程序相同的目录（源码目录的上一级）

安装器会把主程序、配置和第三方声明嵌入到安装包；安装时释放到所选目录，不会附加源码。

## 项目结构

```text
.
├─ Program.cs                 入口、单实例、便携数据目录
├─ MainForm.cs                主界面、托盘、请求流程、目录级自动 Token 集成
├─ RequestBuilder.cs          cURL 构建 / 导入 / 键值编辑器
├─ RequestStore.cs            请求库数据与持久化
├─ RequestLibraryView.cs      顶部请求库和目录开关
├─ AutoTokenService.cs        Token 登录、401 判断与重试辅助
├─ AutoTokenDialog.cs         自动 Token 登录配置
├─ CurlEngine.cs              cURL 解析、执行和响应解析
├─ Theme.cs                   浅色主题和自绘控件
├─ installer/                 WinForms 安装器、卸载器和打包脚本
└─ tests/EngineSelfTest.cs    引擎自测
```

## 已知限制

- 仅支持 Windows。
- 需要系统已安装 `curl.exe` 和 .NET Framework 4.8。
- 自动 Token 的登录返回值固定读取 `data.token`；项目接口字段不同则需要修改源码。
- 文件上传 / multipart 请求体导入时只保留基础信息，建议直接用 cURL 模式发送。
- 没有自动更新；升级需要重新运行安装器。
- 没有代码签名，因此 SmartScreen / 杀毒软件提示属于已知风险，而不是安装器或程序的隐藏行为。

## 许可证

当前仓库未添加 `LICENSE` 文件，默认保留所有权利。如果需要以 MIT、Apache-2.0 等许可证分发，请先添加对应的许可证文件。
