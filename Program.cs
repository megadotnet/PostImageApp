using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Core.Abstractions;
using App.Core.Extensions;
using App.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App.Cli;

/// <summary>
/// The thin CLI wrapper for the PostImage uploader core library.
/// Handles argument parsing, DI container setup, and global exception mapping.
/// </summary>
public class Program
{
    /// <summary>
    /// The main entry point for the CLI application.
    /// </summary>
    /// <param name="args">Command line arguments passed to the application.</param>
    /// <returns>An exit code: 0 for success, 1 for failure, 2 for invalid arguments.</returns>
    public static async Task<int> Main(string[] args)
    {
        // Ensure emojis or special characters in paths/urls don't break stdout rendering
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        string? filePath = null;
        string? imageUrl = null;
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--file" or "-f":
                    if (i + 1 >= args.Length)
                    {
                        WriteError("Missing value for --file");
                        return 2;
                    }
                    filePath = args[++i];
                    break;

                case "--url" or "-u":
                    if (i + 1 >= args.Length)
                    {
                        WriteError("Missing value for --url");
                        return 2;
                    }
                    imageUrl = args[++i];
                    break;

                case "--verbose" or "-v":
                    verbose = true;
                    break;

                default:
                    if (args[i].StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        args[i].StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        imageUrl = args[i];
                    }
                    else
                    {
                        filePath = args[i];
                    }
                    break;
            }
        }

        if (filePath is not null && imageUrl is not null)
        {
            WriteError("Cannot specify both --file and --url. Please provide only one input.");
            return 2;
        }

        if (filePath is null && imageUrl is null)
        {
            WriteError("No input provided. Use --file <path> or --url <url>.");
            PrintUsage();
            return 2;
        }

        // ── Dependency Injection & Host Building ──────────────────────────────────
        var builder = Host.CreateDefaultBuilder(args);

        // Map CLI arguments into the configuration so the Core library can read them
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>("PostImageUploader:Verbose", verbose.ToString())
            });
        });

        builder.ConfigureLogging((context, logging) =>
        {
            logging.ClearProviders();
            if (verbose)
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            }
            else
            {
                logging.SetMinimumLevel(LogLevel.Warning);
            }
        });

        builder.ConfigureServices((context, services) =>
        {
            // Defer all core logic and dependency wire-ups to the library's extension method
            services.AddMyAppCore(context.Configuration);
        });

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<IPostImageClient>();

        // Set a hard global timeout for the entire CLI operation
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        // ── Execution & Global Exception Handling ─────────────────────────────────
        UploadResult result;
        try
        {
            result = filePath is not null
                ? await client.UploadFileAsync(filePath, cts.Token)
                : await client.UploadFromUrlAsync(imageUrl!, cts.Token);
        }
        catch (OperationCanceledException)
        {
            WriteError("Operation timed out (> 3 minutes).");
            return 1;
        }
        catch (Exception ex)
        {
            WriteError($"Unexpected error: {ex.Message}");
            return 1;
        }

        if (result.Success && !string.IsNullOrWhiteSpace(result.ImageUrl))
        {
            Console.WriteLine(result.ImageUrl);
            return 0;
        }
        else
        {
            WriteError(result.ErrorMessage ?? "Upload failed for unknown reason.");
            return 1;
        }
    }

    static void WriteError(string message)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
    }

    static void PrintUsage()
    {
        Console.WriteLine("PostImage Uploader — Upload images to PostImages.org");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  postimageuploader <filepath>          Upload a local file (positional)");
        Console.WriteLine("  postimageuploader --file <filepath>   Upload a local file");
        Console.WriteLine("  postimageuploader --url <imageurl>    Upload from a remote URL");
        Console.WriteLine("  postimageuploader --verbose           Enable verbose logging");
        Console.WriteLine("  postimageuploader --help              Show this help");
        Console.WriteLine();
        Console.WriteLine("Supported formats:  .jpg .jpeg .png .gif .bmp .webp .tiff .tif");
        Console.WriteLine("Max file size:      12 MB");
        Console.WriteLine();
        Console.WriteLine("Output:");
        Console.WriteLine("  On success: prints the permanent postimg.cc URL to stdout");
        Console.WriteLine("  On failure: prints error reason to stderr, exits with code 1");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  postimageuploader photo.jpg");
        Console.WriteLine("  postimageuploader --file ./images/banner.png");
        Console.WriteLine("  postimageuploader --url https://example.com/image.png");
    }
}
