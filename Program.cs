using PostImageUploader;

// ═══════════════════════════════════════════════════════════════════════════════
//  PostImage Uploader — CLI Entry Point
//
//  Usage:
//    postimageuploader <filepath>          Upload a local image file
//    postimageuploader --file <filepath>   Upload a local image file (explicit)
//    postimageuploader --url <imageurl>    Upload from a remote image URL
//    postimageuploader --help              Show usage information
//
//  Exit codes:
//    0 — Success (image URL printed to stdout)
//    1 — Failure (error description printed to stderr)
//    2 — Invalid arguments
// ═══════════════════════════════════════════════════════════════════════════════

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Parse arguments ────────────────────────────────────────────────────────────
// Note: in top-level statements, 'args' is the implicit parameter name.
// We use 'cliArgs' here to avoid CS0136 shadowing conflict.
var cliArgs = args;

if (cliArgs.Length == 0 || cliArgs.Contains("--help") || cliArgs.Contains("-h"))
{
    PrintUsage();
    return 0;
}

string? filePath = null;
string? imageUrl = null;

for (int i = 0; i < cliArgs.Length; i++)
{
    switch (cliArgs[i].ToLowerInvariant())
    {
        case "--file" or "-f":
            if (i + 1 >= cliArgs.Length)
            {
                WriteError("Missing value for --file");
                return 2;
            }
            filePath = cliArgs[++i];
            break;

        case "--url" or "-u":
            if (i + 1 >= cliArgs.Length)
            {
                WriteError("Missing value for --url");
                return 2;
            }
            imageUrl = cliArgs[++i];
            break;

        default:
            // Positional argument: auto-detect URL vs file path
            if (cliArgs[i].StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                cliArgs[i].StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                imageUrl = cliArgs[i];
            }
            else
            {
                filePath = cliArgs[i];
            }
            break;
    }
}

// ── Validate mutually exclusive inputs ────────────────────────────────────────
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

// ── Execute upload ─────────────────────────────────────────────────────────────
using var client = new PostImageClient(verbose: false);
using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(3));

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

// ── Output result ──────────────────────────────────────────────────────────────
if (result.Success && !string.IsNullOrWhiteSpace(result.ImageUrl))
{
    // Clean stdout: just the URL — suitable for scripting / piping
    Console.WriteLine(result.ImageUrl);
    return 0;
}
else
{
    WriteError(result.ErrorMessage ?? "Upload failed for unknown reason.");
    return 1;
}

// ── Helpers ───────────────────────────────────────────────────────────────────
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
