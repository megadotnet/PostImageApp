using System;
using System.Collections.Generic;

namespace App.Core.Models;

public class PostImageUploaderOptions
{
    public const string ConfigurationSectionName = "PostImageUploader";

    public long MaxFileSizeBytes { get; set; } = 12L * 1024 * 1024; // 12 MB

    public string[] SupportedExtensions { get; set; } =
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif" };

    public bool Verbose { get; set; } = false;
}
