# PostImage 图片上传工具

> 基于 **.NET 8** 构建的生产级控制台程序，将图片上传至 [PostImages.org](https://postimages.org) 并通过 **5 层验证流水线** 保障结果可靠性：HTTP 状态码校验 → JSON 解析校验 → URL 合法性校验 → 链接可访问性主动验证。

🌐 [English Documentation → README.md](./README.md)

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![平台](https://img.shields.io/badge/平台-Windows%20|%20macOS%20|%20Linux-lightgrey)](https://dotnet.microsoft.com/)
[![许可证](https://img.shields.io/badge/许可证-MIT-green)](LICENSE)

---

## 目录

- [项目介绍](#项目介绍)
- [技术栈清单](#技术栈清单)
- [环境依赖要求](#环境依赖要求)
- [本地部署与启动](#本地部署与启动)
- [项目结构说明](#项目结构说明)
- [开发规范](#开发规范)
- [常见问题排查](#常见问题排查)

---

## 项目介绍

PostImage Uploader 是一个使用 .NET 8 构建的健壮命令行工具，用于将本地图片或远程图片 URL 直接上传至 PostImages.org。通过严格的 5 层验证流水线，它保证了上传的可靠性并在成功后返回图片直链。

### 功能特性

- ✅ 从本地文件上传图片至 PostImages.org 官方 JSON 接口
- ✅ 从远程 URL 下载并二次上传图片
- ✅ **5 维度验证**：本地预检 → HTTP 200 → JSON 解析 → URL 域名校验 → 链接 HEAD/GET 主动验证
- ✅ 精准失败定位：`FileNotFound`、`FileTooLarge`、`UnsupportedFormat`、`NetworkError`、`Timeout`、`HttpError`、`JsonParseError`、`InvalidResponseUrl`、`LinkNotAccessible`
- ✅ 上传成功后返回 `postimg.cc` 永久访问直链
- ✅ 采用 Core + CLI 架构，充分利用现代 .NET 的依赖注入和日志抽象。

---

## 技术栈清单

本项目的技术选型分为以下四大核心类别：

### 1. 前端
- **无**：本项目是一个纯后端命令行界面（CLI）工具，不包含任何图形用户界面（GUI）或 Web 前端组件。

### 2. 后端
| 组件 / 基础库 | 版本号 | 在项目中的核心作用 |
|--------------|-------|------------------|
| **.NET 8**（目标框架） | `net8.0` | 运行时与编译目标；支持 `record` 类型、隐式 using、可空引用类型等现代 C# 特性 |
| **C# 语言** | 12.0 | 主开发语言；使用顶层语句、主构造函数及表达式体成员等特性 |
| `Microsoft.Extensions.Hosting` | `8.0.0` | 应用程序宿主，用于在 CLI 中配置依赖注入、日志记录和配置 |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `8.0.0` | 为 `App.Core` 提供控制反转（IoC）和依赖注入的抽象接口 |
| `Microsoft.Extensions.Http` | `8.0.0` | 提供 `IHttpClientFactory` 用于管理和复用 HTTP 连接 |
| `Microsoft.Extensions.Logging.Abstractions` | `8.0.0` | 为核心类库的业务逻辑提供统一的日志抽象接口 |
| `Microsoft.Extensions.Logging.Console` | `8.0.0` | 控制台日志提供程序，用于 CLI 输出 |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `8.0.0` | 支持将 `IConfiguration` 强类型绑定至配置类 |

### 3. 基础设施
| 组件 | 版本号 | 在项目中的核心作用 |
|------|-------|------------------|
| **PostImages.org API** | 当前版本 | 目标图床平台；核心上传端点：`POST https://postimages.org/json` |
| **postimg.cc CDN** | — | 图片分发 CDN；生成链接格式为 `https://postimg.cc/{id}/{hash}` |
| **HTTPS / TLS** | TLS 1.2+ | 所有网络请求强制启用传输层加密加密通道 |

### 4. 工具链
| 工具 | 版本号 | 在项目中的核心作用 |
|------|-------|------------------|
| **dotnet CLI** | 8.0.x+ | 用于项目还原、编译、运行、测试和发布的官方命令行工具链 |
| **MSBuild** | 17.x+ | 通过 `.csproj` 与 `.sln` 编排并编译主项目及测试项目 |
| **Moq** | `4.20.72` | 模拟框架，用于在单元测试中生成依赖项的测试替身（Test Doubles） |
| **xUnit** | `2.5.3` | 测试框架，用于驱动和执行所有的测试用例 |
| **xunit.runner.visualstudio** | `2.5.3` | 测试运行器，使 xUnit 测试能在 .NET CLI 中执行 |
| **Microsoft.NET.Test.Sdk** | `17.8.0` | MSBuild 测试目标和基础结构支持 |
| **coverlet.collector** | `6.0.0` | 测试执行期间的代码覆盖率数据收集工具 |

---

## 环境依赖要求

| 依赖项 | 最低兼容版本 | 说明 |
|--------|------------|------|
| **.NET SDK** | **8.0.100** | 编译和运行项目所必需。[下载地址](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **操作系统** | Windows 10 / macOS 12 / Ubuntu 20.04 | 任何支持 .NET 8 的操作系统均可 |
| **互联网连接** | — | 必须能访问 `postimages.org` 和 `postimg.cc` |

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
dotnet run --project App.Cli.csproj -- testdata\test_valid.jpg
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
dotnet run --project App.Cli.csproj -- testdata/test_valid.jpg
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
dotnet run --project App.Cli.csproj -- testdata/test_valid.jpg
```

### 发布为跨平台自包含单文件

```bash
# Windows x64
dotnet publish App.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/win

# macOS Apple Silicon (M1/M2/M3)
dotnet publish App.Cli.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/mac-arm

# Linux x64
dotnet publish App.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux
```

---

## 项目结构说明

```
PostImageApp/
├── PostImageUploader.sln          # Visual Studio 解决方案文件
├── App.Cli.csproj                 # CLI 应用程序项目
├── Program.cs                     # CLI 入口（解析参数，调用核心逻辑）
├── App.Core/                      # 包含纯粹业务逻辑的核心类库
│   ├── App.Core.csproj            # 核心项目，包含抽象和各类服务
│   ├── Extensions/                # 依赖注入扩展方法
│   ├── Abstractions/              # 接口定义 (IPostImageClient, IFileSystem 等)
│   └── ...                        # 核心服务、模型、配置
├── README.md                      # 英文文档
├── ReadMe-ZhCn.md                 # 中文文档 (本文件)
├── documents/                     # 项目相关文档资料
└── PostImageUploader.Tests/       # xUnit 测试项目
    ├── PostImageUploader.Tests.csproj # 引用 App.Core 及 Moq 的测试项目
    ├── Unit/                      # 单元测试
    └── Integration/               # 集成测试
```

---

## 开发规范

1. **架构设计**：将纯业务逻辑保留在 `App.Core` 中。`App.Cli` 仅作为包装器，负责依赖注入装配、配置读取及控制台界面交互。
2. **配置管理**：必须使用强类型的 `IOptions<T>` 接口进行配置管理，禁止使用魔法字符串。
3. **抽象设计**：外部依赖（时间、文件系统、网络）必须被抽象并通过接口注入，以便于进行高覆盖率的单元测试。
4. **测试**：使用 `xUnit` 和 `Moq` 编写单元测试。在修复 bug 前尽可能先编写一个红色的失败测试（TDD 实践）。
5. **代码风格**：确保启用可空引用类型安全。为所有公共方法、类提供信息丰富的 XML 文档注释。

### 运行测试

```bash
# 运行解决方案中的所有测试
dotnet test PostImageUploader.sln

# 仅运行单元测试
dotnet test --filter "Category=Unit" PostImageUploader.sln
```

---

## 常见问题排查

### `dotnet: 命令未找到`

从[官方下载页](https://dotnet.microsoft.com/download/dotnet/8.0)安装 .NET 8 SDK，确保 `dotnet` 已添加至系统的 `PATH` 环境变量中。

### 构建警告 `CS8604`

检查是否正确处理了可能为空（nullable）的变量。仅当明确知道变量不为空时，才能使用空值原谅运算符（`!`），否则应补充判空逻辑。

### 上传返回 HTTP 403 Forbidden

触发了 PostImages.org 的速率限制。请等待一分钟后重试。

### 依赖注入错误

请确保你已经通过 `App.Core` 提供的 `IServiceCollection` 扩展方法（例如 `services.AddMyAppCore()`）正确注册了核心业务逻辑服务。
