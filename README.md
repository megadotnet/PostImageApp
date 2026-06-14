# PostImage Uploader

> A production-quality .NET 8 console application that uploads images to [PostImages.org](https://postimages.org) with a **5-layer validation pipeline** — HTTP status verification, JSON parsing, URL legitimacy check, and live link accessibility testing.

🌐 [中文文档 → ReadMe-ZhCn.md](./ReadMe-ZhCn.md)

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20|%20macOS%20|%20Linux-lightgrey)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Project Structure](#project-structure)
- [Local Setup & Running](#local-setup--running)
- [Upload Workflow](#upload-workflow)
- [API Reference](#api-reference)
- [Validation Pipeline](#validation-pipeline)
- [Test Cases](#test-cases)
- [Development Guidelines](#development-guidelines)
- [Troubleshooting](#troubleshooting)

---

## Features

- ✅ Upload local image files to PostImages.org via the official JSON API
- ✅ Download and re-upload remote images by URL
- ✅ **5-dimensional validation**: file pre-check → HTTP 200 → JSON parse → URL domain check → live link HEAD/GET verify
- ✅ Precise failure diagnosis: `FileNotFound`, `FileTooLarge`, `UnsupportedFormat`, `NetworkError`, `Timeout`, `HttpError`, `JsonParseError`, `InvalidResponseUrl`, `LinkNotAccessible`
- ✅ Returns permanent `postimg.cc` direct links on success
- ✅ Zero external NuGet dependencies — 100% .NET BCL

---

## Tech Stack

The project's technical choices are structured into four main categories:

### 1. Frontend
- **None**: This is a command-line interface (CLI) application with no graphical user interface (GUI) or web frontend components.

### 2. Backend
| Component / Library | Version | Role in Project |
|---------------------|---------|----------------|
| **.NET 8** (Target Framework) | `net8.0` | Runtime and compilation target; minimum compatible version for C# 12 features. |
| **C# Language** | 12.0 (implicit) | Primary programming language; uses top-level statements, primary constructors, and expression-bodied members. |
| `System.Net.Http.HttpClient` | Built-in (.NET 8) | Core network client for session initialization, multipart uploads, and link accessibility checking. |
| `System.Net.CookieContainer` | Built-in (.NET 8) | Manages authentication/guest cookies automatically. |
| `System.Text.Json` | Built-in (.NET 8) | High-performance, zero-allocation JSON parsing for API responses. |
| `System.IO` | Built-in (.NET 8) | Handles local file reading, existence, format, and size validations. |

### 3. Infrastructure
| Component | Version | Role in Project |
|-----------|---------|----------------|
| **PostImages.org API** | Current (no versioning) | Target image hosting platform; endpoint: `POST https://postimages.org/json` |
| **postimg.cc CDN** | — | Image delivery CDN; generated links follow `https://postimg.cc/{id}/{hash}` |
| **HTTPS / TLS** | TLS 1.2+ (OS-managed) | Enforces secure transport layer encryption for all communications. |

### 4. Toolchain
| Tool | Version | Role in Project |
|------|---------|----------------|
| **dotnet CLI** | 8.0.x+ | Primary CLI toolchain for building, running, testing, and publishing. |
| **MSBuild** | 17.x+ (bundled) | Configures and compiles the project and test dependencies. |
| **NuGet** | 7.3.0 (bundled) | Standard package manager (0 external dependencies in the CLI application). |
| **xUnit** | 2.5.3 (in Tests project) | Testing framework used to run the 52 validation test cases. |

### Test Data
| File | Size | Purpose |
|------|------|---------|
| `testdata/test_valid.jpg` | ~977 KB | Valid JPG — positive upload test |
| `testdata/test_valid.png` | ~425 KB | Valid PNG — positive upload test |
| `testdata/test_oversized.jpg` | 20 MB | Oversized file — negative test (`FileTooLarge`) |

---

## Prerequisites

| Dependency | Minimum Version | Notes |
|------------|----------------|-------|
| **.NET SDK** | **8.0.100** | Download: [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Operating System** | Windows 10 / macOS 12 / Ubuntu 20.04 | Any OS with .NET 8 support |
| **Internet Access** | — | Required to reach `postimages.org` and `postimg.cc` |
| **Git** | Any | Optional — for cloning the repository |

> **Note**: No Docker, no database, no message queue, no environment variables required. This is a pure console app with zero infrastructure dependencies.

Verify your environment:

```bash
dotnet --version
# Expected output: 8.0.x or higher
```

---

## Project Structure

```
PostImageApp/
├── PostImageUploader.sln          # Visual Studio solution (VS 2019+ format)
├── PostImageUploader.csproj       # MSBuild project: TargetFramework=net8.0 (CLI executable)
├── Program.cs                     # CLI entry point (parses args, routes to client)
├── PostImageClient.cs             # Core service: upload client + 5-layer validation engine
├── README.md                      # This file (English)
├── ReadMe-ZhCn.md                 # 中文文档
├── documents/
│   └── httpraw.txt                # Chrome DevTools HAR export of API traffic
├── testdata/
│   ├── test_valid.jpg             # ~977 KB JPG (positive upload test)
│   ├── test_valid.png             # ~425 KB PNG (positive upload test)
│   └── test_oversized.jpg         # 20 MB oversized file (negative FileTooLarge test)
└── PostImageUploader.Tests/       # NEW: xUnit test project
    ├── PostImageUploader.Tests.csproj
    ├── Unit/
    │   └── ValidationTests.cs     # 45 pure unit tests (no network)
    └── Integration/
        └── UploadIntegrationTests.cs # 7 integration tests (real API calls)
```

### Key Source Files

| File | Responsibility |
|------|---------------|
| `Program.cs` | Command-line interface parser and entry point. Supports local file and URL uploads, outputs clean URLs on success. |
| `PostImageClient.cs` | Core `PostImageClient` and `UploadValidator` classes. Implements multipart building, upload pipeline, and the 5-layer verification. |

---

## CLI Usage

The application compiles into a command-line interface tool named `PostImageUploader` (or via `dotnet run`).

```bash
# Upload a local file (positional argument)
dotnet run --project PostImageUploader.csproj -- testdata/test_valid.jpg

# Upload a local file (explicit flag)
dotnet run --project PostImageUploader.csproj -- --file testdata/test_valid.png

# Upload from a remote image URL
dotnet run --project PostImageUploader.csproj -- --url https://www.google.com/images/branding/googlelogo/2x/googlelogo_color_272x92dp.png

# Show CLI usage help
dotnet run --project PostImageUploader.csproj -- --help
```

### Output Behavior
- **On Success**: Prints only the permanent `postimg.cc` page URL to `stdout` (exit code `0`). This makes it highly scriptable (e.g., in bash pipelines).
- **On Failure**: Prints a formatted error message prefix `[ERROR]` to `stderr` (exit code `1` or `2`).

---

## Local Setup & Running

### Windows (PowerShell / Command Prompt)

```powershell
# 1. Clone or download the project
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 2. Verify .NET SDK version (must be 8.0+)
dotnet --version

# 3. Build the entire solution (both CLI and Tests)
dotnet build PostImageUploader.sln --configuration Release

# 4. Run CLI with a test image
dotnet run --project PostImageUploader.csproj -- testdata/test_valid.jpg
```

### macOS (Terminal / zsh)

```bash
# 1. Install .NET SDK (if not installed)
brew install --cask dotnet-sdk

# 2. Clone the project
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 3. Build the solution
dotnet build PostImageUploader.sln --configuration Release

# 4. Run CLI with a test image
dotnet run --project PostImageUploader.csproj -- testdata/test_valid.jpg
```

### Linux (Ubuntu / Debian)

```bash
# 1. Install .NET SDK 8
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0

# 2. Clone the project
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 3. Build the solution
dotnet build PostImageUploader.sln --configuration Release

# 4. Run CLI with a test image
dotnet run --project PostImageUploader.csproj -- testdata/test_valid.jpg
```

---

## Running Tests

The solution contains a comprehensive suite of unit and integration tests using xUnit.

```bash
# Run all tests in the solution
dotnet test

# Run unit tests only (instant, no network calls)
dotnet test --filter "Category=Unit"

# Run integration tests only (requires active internet access)
dotnet test --filter "Category=Integration"
```

---

## Publish as Self-Contained Single Executable

```bash
# Windows x64
dotnet publish PostImageUploader.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -o ./publish/win

# macOS Apple Silicon (M1/M2/M3)
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

## Upload Workflow

```
[Local File / Remote URL]
         │
         ▼  Pre-validation (Local — no network)
  ┌──────────────────────────────────┐
  │  ① File exists?                   │ → FileNotFound
  │  ② Extension in supported list?   │ → UnsupportedFormat
  │  ③ File size ≤ 12 MB?             │ → FileTooLarge
  └──────────────────────────────────┘
         │
         ▼  Session Initialization
  GET https://postimages.org/
  └─ Stores GUESTKEY cookie in CookieContainer
         │
         ▼  Upload Request
  POST https://postimages.org/json
  Headers:
    Content-Type:      multipart/form-data; boundary=----WebKitFormBoundary{random16}
    X-Requested-With:  XMLHttpRequest
    Accept:            application/json
    Origin:            https://postimages.org
    Referer:           https://postimages.org/
    Cache-Control:     no-cache
  Form Fields:
    gallery        = ""
    optsize        = "0"    (no resize)
    expire         = "0"    (never expire)
    numfiles       = "1"
    upload_session = "{unixMs}.{random5}"
    file           = <binary image bytes; Content-Type: image/jpeg|png|...>
         │
         ▼  Response Validation (5 Layers)
  ┌──────────────────────────────────────────┐
  │  ④ HTTP status == 200?                    │ → HttpError (403/413/429/5xx hint)
  │  ⑤ Response body is valid JSON?           │ → JsonParseError / EmptyResponse
  │  ⑥ "url" starts with "postimg.cc"?        │ → InvalidResponseUrl
  │  ⑦ HEAD (fallback GET) to url → 200?      │ → LinkNotAccessible
  └──────────────────────────────────────────┘
         │
         ▼  Success
  UploadResult {
    Success        = true,
    ImageUrl       = "https://postimg.cc/{id}/{hash}",
    LinkAccessible = true,
    UploadElapsed  = ~2.75s,
    VerifyElapsed  = ~1.81s
  }
```

---

## API Reference

### `PostImageClient`

```csharp
public sealed class PostImageClient : IDisposable
```

| Method | Signature | Description |
|--------|-----------|-------------|
| `UploadFileAsync` | `Task<UploadResult>(string filePath, CancellationToken ct = default)` | Upload a local file. Performs 3-layer pre-validation before any network call. |
| `UploadFromUrlAsync` | `Task<UploadResult>(string imageUrl, CancellationToken ct = default)` | Download a remote image and upload it to PostImages.org. |

### `UploadResult`

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | `true` only if all 5 validation layers pass |
| `ImageUrl` | `string?` | Permanent `postimg.cc` page URL |
| `DirectImageUrl` | `string?` | CDN image URL (`image` field from API; same as `ImageUrl`) |
| `LinkAccessible` | `bool` | Whether the generated link returned HTTP 200 |
| `ErrorMessage` | `string?` | Structured failure message: `"失败原因: {FailureReason} — {detail}"` |
| `HttpStatusCode` | `int` | Raw HTTP status from upload API (0 if locally intercepted) |
| `FileSizeBytes` | `long` | File size in bytes |
| `UploadElapsed` | `TimeSpan` | Time spent on the upload HTTP call |
| `VerifyElapsed` | `TimeSpan` | Time spent verifying link accessibility |

### `FailureReason` Enum

```csharp
public enum FailureReason
{
    None,               // No failure
    FileNotFound,       // File path does not exist
    FileTooLarge,       // File exceeds 12 MB pre-check limit
    UnsupportedFormat,  // File extension not in supported list
    NetworkError,       // HttpRequestException or download failure
    Timeout,            // TaskCanceledException (>120s upload or >15s verify)
    HttpError,          // Non-2xx HTTP response from upload API
    JsonParseError,     // Response body is not valid JSON
    InvalidResponseUrl, // "url" field not on postimg.cc domain
    LinkNotAccessible,  // HEAD/GET to generated link returned non-2xx
    EmptyResponse,      // Response body is empty or whitespace
    UnknownError        // Unexpected exception
}
```

---

## Validation Pipeline

| Layer | Scope | Check | Failure |
|-------|-------|-------|---------|
| 1 | Local | File exists | `FileNotFound` |
| 2 | Local | Extension in `{.jpg,.jpeg,.png,.gif,.bmp,.webp,.tiff,.tif}` | `UnsupportedFormat` |
| 3 | Local | File ≤ 12 MB | `FileTooLarge` |
| 4 | Network | Upload API → HTTP 200 | `HttpError` |
| 5a | Network | Response JSON parses successfully | `JsonParseError` / `EmptyResponse` |
| 5b | Network | `url` field domain is `postimg.cc` | `InvalidResponseUrl` |
| 5c | Network | HEAD or GET to `url` → HTTP 200 | `LinkNotAccessible` |

---

## Test Cases

All 5 test cases run automatically on `dotnet run`. Results from the last verified run (2026-06-14):

| # | Input | File Size | Expected | Actual Result |
|---|-------|-----------|----------|---------------|
| 1 | `test_valid.jpg` | 977.2 KB | Success | ✅ `https://postimg.cc/NKXD5N1Z/4edbb0e4` |
| 2 | `test_valid.png` | 425.5 KB | Success | ✅ `https://postimg.cc/f3jvvJq9/084688d0` |
| 3 | `test_oversized.jpg` | 20 MB | `FileTooLarge` | ✅ Correctly rejected |
| 4 | `nonexistent.png` | N/A | `FileNotFound` | ✅ Correctly rejected |
| 5 | `fake_image.exe` | N/A | `UnsupportedFormat` | ✅ Correctly rejected |

**Final verdict**: 5 / 5 passed ✅

---

## Development Guidelines

### Adding a New Test Case

Append to the `testCases` array in `Program.cs`:

```csharp
new TestCase(
    Name:        "✅ New Test — Description",
    FilePath:    Path.Combine(testDataDir, "your_image.png"),
    ExpectPass:  true,
    Description: "What this test validates"
),
```

### Adding a New Failure Reason

1. Add the value to `FailureReason` enum in `PostImageClient.cs`
2. Return it via the `Fail(FailureReason.X, "message")` helper method
3. If it maps to an HTTP status code, add a hint in the `httpRsp.StatusCode` switch expression

### Code Style

| Rule | Detail |
|------|--------|
| Nullable reference types | Enabled — always handle `string?` before use |
| `IDisposable` | Always wrap `PostImageClient` in `using` |
| Async methods | No `async void` — all return `Task` or `Task<T>` |
| CRLF in multipart | Mandatory — RFC 2046 requires `\r\n`; never substitute `\n` |
| Encoding | Always use `Encoding.UTF8` explicitly for text parts |

### Supported Image Formats

```
Extension       MIME Type
.jpg / .jpeg →  image/jpeg
.png         →  image/png
.gif         →  image/gif
.bmp         →  image/bmp
.webp        →  image/webp
.tiff / .tif →  image/tiff
```

---

## Troubleshooting

### `dotnet: command not found`

Install the .NET 8 SDK from the [official downloads page](https://dotnet.microsoft.com/download/dotnet/8.0) and ensure `dotnet` is on your `PATH`. Then open a new terminal window.

### Upload returns HTTP 403 Forbidden

Rate limiting is in effect. Possible causes:
- Too many requests in a short period → wait 60 seconds
- Session cookie not acquired → check internet connectivity to `postimages.org`

### Upload returns HTTP 413 Request Entity Too Large

The file exceeded the server's configured limit even after the local 12 MB pre-check passed. Compress or resize the image and try again.

### `JsonParseError` — unexpected response

PostImages.org may return an HTML error page (e.g., during maintenance). The raw response body is included in `UploadResult.RawResponse`.

### `LinkNotAccessible` after successful upload

CDN propagation latency. The record was created server-side but the edge cache hasn't propagated yet. Wait 5–10 seconds and try accessing the URL in a browser.

### Test images missing at runtime

Re-build the project to trigger `CopyToOutputDirectory`:

```bash
dotnet build PostImageUploader.csproj --configuration Release
```

Then verify `bin/Release/net8.0/testdata/` contains all three test files.

### Build warning `CS8604`

Already suppressed with the `!` null-forgiving operator. If it re-appears, add an explicit null guard before the flagged call site.

---

## License

This project is licensed under the [MIT License](LICENSE).

---

*Documentation generated: 2026-06-14 | API endpoint verified via Chrome DevTools capture in `documents/httpraw.txt`*
