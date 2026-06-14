# PostImage 图片上传工具

> 基于 **.NET 8** 构建的生产级控制台程序，将图片上传至 [PostImages.org](https://postimages.org) 并通过 **5 层验证流水线** 保障结果可靠性：HTTP 状态码校验 → JSON 解析校验 → URL 合法性校验 → 链接可访问性主动验证。

🌐 [English Documentation → README.md](./README.md)

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![平台](https://img.shields.io/badge/平台-Windows%20|%20macOS%20|%20Linux-lightgrey)](https://dotnet.microsoft.com/)
[![许可证](https://img.shields.io/badge/许可证-MIT-green)](LICENSE)

---

## 目录

- [功能特性](#功能特性)
- [技术栈](#技术栈)
- [环境依赖要求](#环境依赖要求)
- [项目结构](#项目结构)
- [本地部署与启动](#本地部署与启动)
- [上传流程说明](#上传流程说明)
- [API 参考](#api-参考)
- [验证流水线](#验证流水线)
- [测试用例](#测试用例)
- [开发规范](#开发规范)
- [常见问题排查](#常见问题排查)

---

## 功能特性

- ✅ 从本地文件上传图片至 PostImages.org 官方 JSON 接口
- ✅ 从远程 URL 下载并二次上传图片
- ✅ **5 维度验证**：本地预检 → HTTP 200 → JSON 解析 → URL 域名校验 → 链接 HEAD/GET 主动验证
- ✅ 精准失败定位：`FileNotFound`、`FileTooLarge`、`UnsupportedFormat`、`NetworkError`、`Timeout`、`HttpError`、`JsonParseError`、`InvalidResponseUrl`、`LinkNotAccessible`
- ✅ 上传成功后返回 `postimg.cc` 永久访问直链
- ✅ 零外部 NuGet 包依赖，100% 使用 .NET 基础类库（BCL）

---

## 技术栈

本项目的技术选型分为以下四大核心类别：

### 1. 前端
- **无**：本项目是一个纯后端命令行界面（CLI）工具，不包含任何图形用户界面（GUI）或 Web 前端组件。

### 2. 后端
| 组件 / 基础库 | 版本号 | 在项目中的核心作用 |
|--------------|-------|------------------|
| **.NET 8**（目标框架） | `net8.0` | 运行时与编译目标；支持 `record` 类型、隐式 using、可空引用类型等现代 C# 特性 |
| **C# 语言** | 12.0（随 .NET 8 隐式启用） | 主开发语言；使用顶层语句、主构造函数及表达式体成员等特性 |
| `System.Net.Http.HttpClient` | 内置（.NET 8） | 核心网络客户端，用于 Session 初始化、multipart 表单上传以及链接可访问性校验 |
| `System.Net.CookieContainer` | 内置（.NET 8） | 自动管理并存储 PostImages.org 的 GUESTKEY 会话 Cookie |
| `System.Text.Json` | 内置（.NET 8） | 提供高性能、零分配的 JSON 反序列化，解析接口返回的 url/image 字段 |
| `System.IO` | 内置（.NET 8） | 本地文件读取，以及文件大小、格式和存在性的前置校验 |

### 3. 基础设施
| 组件 | 版本号 | 在项目中的核心作用 |
|------|-------|------------------|
| **PostImages.org API** | 当前版本（无显式版本号） | 目标图床平台；核心上传端点：`POST https://postimages.org/json` |
| **postimg.cc CDN** | — | 图片分发 CDN；生成链接格式为 `https://postimg.cc/{id}/{hash}` |
| **HTTPS / TLS** | TLS 1.2+（操作系统托管） | 所有网络请求强制启用 HTTPS 传输层加密加密通道 |

### 4. 工具链
| 工具 | 版本号 | 在项目中的核心作用 |
|------|-------|------------------|
| **dotnet CLI** | 8.0.x+ | 用于项目还原、编译、运行、测试和发布的官方命令行工具链 |
| **MSBuild** | 17.x+（随 SDK 捆绑） | 通过 `.csproj` 与 `.sln` 编排并编译主项目及测试项目 |
| **NuGet** | 7.3.0（随 SDK 捆绑） | 依赖包还原工具（主程序目前为 **0 外部包依赖**） |
| **xUnit** | 2.5.3（仅在测试项目中） | 测试框架，用于驱动和执行 52 个单元与集成测试用例 |

### 测试数据
| 文件 | 大小 | 用途 |
|------|------|------|
| `testdata/test_valid.jpg` | ~977 KB | 合规 JPG——正向上传测试 |
| `testdata/test_valid.png` | ~425 KB | 合规 PNG——正向上传测试 |
| `testdata/test_oversized.jpg` | 20 MB | 超大违规文件——负向测试（触发 `FileTooLarge`） |

---

## 环境依赖要求

| 依赖项 | 最低兼容版本 | 说明 |
|--------|------------|------|
| **.NET SDK** | **8.0.100** | 下载地址：[dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **操作系统** | Windows 10 / macOS 12 / Ubuntu 20.04 | 任何支持 .NET 8 的操作系统均可 |
| **互联网连接** | — | 必须能访问 `postimages.org` 和 `postimg.cc` |
| **Git** | 任意版本 | 可选，用于克隆仓库 |

> **提示**：无需 Docker、数据库、消息队列或环境变量配置。这是一个纯控制台程序，零基础设施依赖。

验证当前环境：

```bash
dotnet --version
# 预期输出：8.0.x 或更高版本
```

---

## 项目结构

```
PostImageApp/
├── PostImageUploader.sln          # Visual Studio 解决方案文件（VS 2019+ 格式）
├── PostImageUploader.csproj       # MSBuild 项目文件：TargetFramework=net8.0（CLI 可执行程序）
├── Program.cs                     # CLI 入口（解析参数，调用客户端）
├── PostImageClient.cs             # 核心服务：上传客户端 + 5 层验证引擎
├── README.md                      # 英文文档
├── ReadMe-ZhCn.md                 # 本文件（中文）
├── documents/
│   └── httpraw.txt                # Chrome DevTools 抓包导出文件
├── testdata/
│   ├── test_valid.jpg             # ~977 KB JPG（正向上传测试）
│   ├── test_valid.png             # ~425 KB PNG（正向上传测试）
│   └── test_oversized.jpg         # 20 MB 超大文件（负向测试）
└── PostImageUploader.Tests/       # 新增：xUnit 测试项目
    ├── PostImageUploader.Tests.csproj
    ├── Unit/
    │   └── ValidationTests.cs     # 45 个纯单元测试（无网络请求）
    └── Integration/
        └── UploadIntegrationTests.cs # 7 个集成测试（真实 API 请求）
```

### 核心源文件说明

| 文件 | 职责 |
|------|------|
| `Program.cs` | 命令行界面解析与入口。支持本地文件与 URL 上传，成功时在 stdout 中仅输出纯 URL。 |
| `PostImageClient.cs` | 核心 `PostImageClient` 和 `UploadValidator` 类。实现 multipart 体构建、上传流程及 5 层验证。 |

---

## CLI 命令行使用说明

项目编译后将生成 `PostImageUploader` 命令行工具（或通过 `dotnet run` 运行）。

```bash
# 上传本地图片文件（位置参数）
dotnet run --project PostImageUploader.csproj -- testdata/test_valid.jpg

# 上传本地图片文件（显式 flag 标记）
dotnet run --project PostImageUploader.csproj -- --file testdata/test_valid.png

# 通过远程图片 URL 进行上传
dotnet run --project PostImageUploader.csproj -- --url https://www.google.com/images/branding/googlelogo/2x/googlelogo_color_272x92dp.png

# 查看 CLI 命令行帮助信息
dotnet run --project PostImageUploader.csproj -- --help
```

### 输出规范
- **成功**：在 `stdout` 中**仅**打印生成的 `postimg.cc` 永久访问直链（退出码为 `0`），极大地方便了脚本化和管道化（如 bash / pipeline）调用。
- **失败**：在 `stderr` 中打印格式化错误前缀 `[ERROR]` 及原因（退出码为 `1` 或 `2`）。

---

## 本地部署与启动

### Windows（PowerShell / 命令提示符）

```powershell
# 1. 克隆或下载项目
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 2. 验证 .NET SDK 版本（必须 8.0+）
dotnet --version

# 3. 构建整个解决方案（包含 CLI 主程序和测试项目）
dotnet build PostImageUploader.sln --configuration Release

# 4. 使用测试图运行 CLI
dotnet run --project PostImageUploader.csproj -- testdata\test_valid.jpg
```

### macOS（Terminal / zsh）

```bash
# 1. 安装 .NET SDK（如未安装）
brew install --cask dotnet-sdk

# 2. 克隆项目
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 3. 构建解决方案
dotnet build PostImageUploader.sln --configuration Release

# 4. 使用测试图运行 CLI
dotnet run --project PostImageUploader.csproj -- testdata/test_valid.jpg
```

### Linux（Ubuntu / Debian）

```bash
# 1. 安装 .NET SDK 8
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0

# 2. 克隆项目
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 3. 构建解决方案
dotnet build PostImageUploader.sln --configuration Release

# 4. 使用测试图运行 CLI
dotnet run --project PostImageUploader.csproj -- testdata/test_valid.jpg
```

---

## 运行测试

本解决方案使用 xUnit 提供了完整的单元测试与集成测试集。

```bash
# 运行解决方案中的所有测试
dotnet test

# 仅运行单元测试（极速，离线无需网络连接）
dotnet test --filter "Category=Unit"

# 仅运行集成测试（需要有效的互联网连接）
dotnet test --filter "Category=Integration"
```

---

## 发布为跨平台自包含单文件

```bash
# Windows x64
dotnet publish PostImageUploader.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -o ./publish/win

# macOS Apple Silicon（M1/M2/M3）
dotnet publish PostImageUploader.csproj -c Release -r osx-arm64 \
  --self-contained true -p:PublishSingleFile=true -o ./publish/mac-arm

# macOS Intel
dotnet publish PostImageUploader.csproj -c Release -r osx-x64 \
  --self-contained true -p:PublishSingleFile=true -o ./publish/mac-x64

# Linux x64
dotnet publish PostImageUploader.csproj -c Release -r linux-x64 \
  --self-contained true -p:PublishSingleFile=true -o ./publish/linux
```

---

## 上传流程说明

```
[本地文件 / 远程 URL]
         │
         ▼  本地预检（无网络请求）
  ┌──────────────────────────────────┐
  │  ① 文件是否存在？                  │ → FileNotFound
  │  ② 扩展名是否在支持列表？           │ → UnsupportedFormat
  │  ③ 文件大小是否 ≤ 12 MB？          │ → FileTooLarge
  └──────────────────────────────────┘
         │
         ▼  Session 初始化
  GET https://postimages.org/
  └─ 获取并存储 GUESTKEY Cookie
         │
         ▼  上传请求
  POST https://postimages.org/json
  请求头：
    Content-Type:      multipart/form-data; boundary=----WebKitFormBoundary{random16}
    X-Requested-With:  XMLHttpRequest
    Accept:            application/json
    Origin:            https://postimages.org
    Referer:           https://postimages.org/
    Cache-Control:     no-cache
  表单字段：
    gallery        = ""
    optsize        = "0"    （不缩放）
    expire         = "0"    （永不过期）
    numfiles       = "1"
    upload_session = "{unixMs}.{random5}"
    file           = <图片二进制内容；Content-Type: image/jpeg|png|...>
         │
         ▼  响应验证（5 层）
  ┌──────────────────────────────────────────┐
  │  ④ HTTP 状态码是否为 200？               │ → HttpError（403/413/429/5xx 分类提示）
  │  ⑤ 响应体是否可解析为有效 JSON？         │ → JsonParseError / EmptyResponse
  │  ⑥ "url" 字段是否以 postimg.cc 开头？   │ → InvalidResponseUrl
  │  ⑦ HEAD（fallback GET）访问 url 是否 200？│ → LinkNotAccessible
  └──────────────────────────────────────────┘
         │
         ▼  验证通过
  UploadResult {
    Success        = true,
    ImageUrl       = "https://postimg.cc/{id}/{hash}",
    LinkAccessible = true,
    UploadElapsed  = ~2.75s,
    VerifyElapsed  = ~1.81s
  }
```

---

## API 参考

### `PostImageClient` 类

```csharp
public sealed class PostImageClient : IDisposable
```

| 方法 | 签名 | 说明 |
|------|------|------|
| `UploadFileAsync` | `Task<UploadResult>(string filePath, CancellationToken ct = default)` | 上传本地文件；在发起网络请求前先执行 3 层本地预检 |
| `UploadFromUrlAsync` | `Task<UploadResult>(string imageUrl, CancellationToken ct = default)` | 下载远程图片后上传至 PostImages.org |

### `UploadResult` 属性说明

| 属性 | 类型 | 说明 |
|------|------|------|
| `Success` | `bool` | 仅当 5 层验证全部通过时为 `true` |
| `ImageUrl` | `string?` | 永久 `postimg.cc` 页面链接 |
| `DirectImageUrl` | `string?` | CDN 图片直链（API `image` 字段，与 `ImageUrl` 相同） |
| `LinkAccessible` | `bool` | 生成链接是否通过 HTTP 200 可访问验证 |
| `ErrorMessage` | `string?` | 结构化失败信息：`"失败原因: {FailureReason} — {详细描述}"` |
| `HttpStatusCode` | `int` | 上传 API 返回的原始 HTTP 状态码（本地拦截时为 0） |
| `FileSizeBytes` | `long` | 文件大小（字节） |
| `UploadElapsed` | `TimeSpan` | 上传 HTTP 请求耗时 |
| `VerifyElapsed` | `TimeSpan` | 链接可访问性验证耗时 |

### `FailureReason` 枚举（12 个值）

```csharp
public enum FailureReason
{
    None,               // 无失败
    FileNotFound,       // 文件路径不存在
    FileTooLarge,       // 文件超过 12 MB 预检上限
    UnsupportedFormat,  // 文件扩展名不在支持列表中
    NetworkError,       // HttpRequestException 或下载失败
    Timeout,            // TaskCanceledException（上传超时 >120s 或验证超时 >15s）
    HttpError,          // 上传 API 返回非 2xx HTTP 响应
    JsonParseError,     // 响应体不是有效 JSON
    InvalidResponseUrl, // "url" 字段不属于 postimg.cc 域名
    LinkNotAccessible,  // HEAD/GET 访问生成链接返回非 2xx
    EmptyResponse,      // 响应体为空或仅含空白字符
    UnknownError        // 未预期的异常
}
```

---

## 验证流水线

| 层级 | 作用域 | 校验内容 | 失败原因 |
|------|--------|----------|---------|
| 1 | 本地 | 文件是否存在 | `FileNotFound` |
| 2 | 本地 | 扩展名是否在 `{.jpg,.jpeg,.png,.gif,.bmp,.webp,.tiff,.tif}` 列表中 | `UnsupportedFormat` |
| 3 | 本地 | 文件大小 ≤ 12 MB | `FileTooLarge` |
| 4 | 网络 | 上传 API 返回 HTTP 200 | `HttpError` |
| 5a | 网络 | 响应体可被成功解析为 JSON | `JsonParseError` / `EmptyResponse` |
| 5b | 网络 | `url` 字段域名为 `postimg.cc` | `InvalidResponseUrl` |
| 5c | 网络 | HEAD 或 GET 访问返回链接 → HTTP 200 | `LinkNotAccessible` |

---

## 测试用例

所有 5 个测试用例在 `dotnet run` 时自动执行。以下为最近一次验证结果（2026-06-14）：

| # | 输入文件 | 文件大小 | 期望结果 | 实际结果 |
|---|---------|---------|---------|---------|
| 1 | `test_valid.jpg` | 977.2 KB | 上传成功 | ✅ `https://postimg.cc/NKXD5N1Z/4edbb0e4` |
| 2 | `test_valid.png` | 425.5 KB | 上传成功 | ✅ `https://postimg.cc/f3jvvJq9/084688d0` |
| 3 | `test_oversized.jpg` | 20 MB | `FileTooLarge` | ✅ 正确拒绝 |
| 4 | `nonexistent.png` | N/A | `FileNotFound` | ✅ 正确拒绝 |
| 5 | `fake_image.exe` | N/A | `UnsupportedFormat` | ✅ 正确拒绝 |

**最终判定**：5 / 5 全部通过 ✅

---

## 开发规范

### 新增测试用例

编辑 `Program.cs`，在 `testCases` 数组中追加：

```csharp
new TestCase(
    Name:        "✅ 新测试 — 描述",
    FilePath:    Path.Combine(testDataDir, "your_image.png"),
    ExpectPass:  true,
    Description: "此测试验证的内容"
),
```

### 新增失败原因

1. 在 `PostImageClient.cs` 的 `FailureReason` 枚举中添加新值
2. 通过 `Fail(FailureReason.X, "详细信息")` 辅助方法返回
3. 若与 HTTP 状态码相关，在 `switch` 表达式中添加对应提示

### 代码规范

| 规则 | 说明 |
|------|------|
| 可空引用类型 | 已启用——使用前必须处理 `string?` |
| `IDisposable` | 必须用 `using` 包裹 `PostImageClient` |
| 异步方法 | 禁止 `async void`——所有异步方法返回 `Task` 或 `Task<T>` |
| multipart 中的 CRLF | 必须使用 `\r\n`（RFC 2046 要求），禁止替换为 `\n` |
| 字符编码 | 文本部分始终显式使用 `Encoding.UTF8` |

### 支持的图片格式

```
扩展名             MIME 类型
.jpg / .jpeg  →  image/jpeg
.png          →  image/png
.gif          →  image/gif
.bmp          →  image/bmp
.webp         →  image/webp
.tiff / .tif  →  image/tiff
```

---

## 常见问题排查

### `dotnet: 命令未找到`

从[官方下载页](https://dotnet.microsoft.com/download/dotnet/8.0)安装 .NET 8 SDK，确保 `dotnet` 已添加至系统 `PATH`，然后重新打开终端窗口。

### 上传返回 HTTP 403 Forbidden（禁止访问）

触发了速率限制。可能原因：
- 短时间内请求过多 → 等待 60 秒后重试
- Session Cookie 未获取 → 检查与 `postimages.org` 的网络连通性

### 上传返回 HTTP 413 Request Entity Too Large（请求体过大）

文件大小超过了服务器配置的限制（与本地 12 MB 预检不同）。请压缩图片后重试。

### `JsonParseError`——响应内容异常

PostImages.org 在维护期间可能返回 HTML 错误页面而非 JSON。原始响应体已记录在 `UploadResult.RawResponse` 字段中，供调试参考。

### 上传成功后出现 `LinkNotAccessible`

CDN 边缘节点传播延迟。记录已在服务端创建，但 CDN 尚未完成缓存同步。等待 5-10 秒后在浏览器中手动访问该链接即可。

### 运行时找不到测试图片

重新构建以触发 `CopyToOutputDirectory` 文件复制：

```bash
dotnet build PostImageUploader.csproj --configuration Release
```

然后确认 `bin/Release/net8.0/testdata/` 目录下包含全部 3 个测试文件。

### 构建警告 `CS8604`

已通过 `!` 空值原谅运算符（null-forgiving operator）抑制。若代码变更后重新出现，在调用处前添加显式空值守卫：

```csharp
if (result.ImageUrl is null) return Fail(FailureReason.InvalidResponseUrl, "url 为 null");
```

---

## 许可证

本项目遵循 [MIT 许可证](LICENSE)授权。

---

*文档生成时间：2026-06-14 | API 端点已通过 `documents/httpraw.txt` 中的 Chrome DevTools 抓包验证*
