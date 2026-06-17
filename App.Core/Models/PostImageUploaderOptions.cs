using System;
using System.Collections.Generic;

namespace App.Core.Models;

/// <summary>
/// Configuration options for the PostImageUploader.
/// These settings govern local validation and logging verbosity.
/// </summary>
public class PostImageUploaderOptions
{
    /// <summary>
    /// The default configuration section name used in appsettings.json.
    /// </summary>
    public const string ConfigurationSectionName = "PostImageUploader";

    /// <summary>
    /// The maximum allowed file size in bytes. Defaults to 12 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 12L * 1024 * 1024; // 12 MB

    /// <summary>
    /// The array of supported file extensions (including the leading dot).
    /// </summary>
    public string[] SupportedExtensions { get; set; } =
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif" };

    /// <summary>
    /// Determines whether verbose logging is enabled.
    /// </summary>
    public bool Verbose { get; set; } = false;
}
