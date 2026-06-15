# App.Core

This is the core library for PostImageUploader. It contains all the business logic, models, and abstractions required to upload images to PostImages.org. It is decoupled from the console application and can be reused in other applications like Web, Desktop, etc.

## Setup

1. Install the NuGet package.
2. In your application startup, configure the services:

```csharp
using App.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

// Build configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

// Add services
var services = new ServiceCollection();
services.AddLogging(); // Or AddConsole() for CLI
services.AddMyAppCore(configuration);

var serviceProvider = services.BuildServiceProvider();

// Use the client
var client = serviceProvider.GetRequiredService<IPostImageClient>();
var result = await client.UploadFileAsync("path/to/image.jpg");
```

## Configuration

You can configure the behavior via `PostImageUploader` section in configuration:

```json
{
  "PostImageUploader": {
    "MaxFileSizeBytes": 12582912,
    "SupportedExtensions": [".jpg", ".png", ".gif"],
    "Verbose": true
  }
}
```
