namespace HyperDroid.CloudKit;

/// <summary>Minimal MIME type lookup from file extension. No external dependencies.</summary>
internal static class MimeTypes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        { ".pdf",  "application/pdf" },
        { ".doc",  "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls",  "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt",  "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".csv",  "text/csv" },
        { ".tsv",  "text/tab-separated-values" },
        { ".rtf",  "application/rtf" },
        { ".odt",  "application/vnd.oasis.opendocument.text" },
        { ".ods",  "application/vnd.oasis.opendocument.spreadsheet" },

        // Images
        { ".jpg",  "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png",  "image/png" },
        { ".gif",  "image/gif" },
        { ".bmp",  "image/bmp" },
        { ".webp", "image/webp" },
        { ".svg",  "image/svg+xml" },
        { ".ico",  "image/x-icon" },
        { ".tif",  "image/tiff" },
        { ".tiff", "image/tiff" },
        { ".avif", "image/avif" },

        // Audio
        { ".mp3",  "audio/mpeg" },
        { ".wav",  "audio/wav" },
        { ".ogg",  "audio/ogg" },
        { ".flac", "audio/flac" },
        { ".aac",  "audio/aac" },
        { ".m4a",  "audio/mp4" },
        { ".wma",  "audio/x-ms-wma" },
        { ".webm", "audio/webm" },

        // Video
        { ".mp4",  "video/mp4" },
        { ".avi",  "video/x-msvideo" },
        { ".mov",  "video/quicktime" },
        { ".mkv",  "video/x-matroska" },
        { ".wmv",  "video/x-ms-wmv" },
        { ".flv",  "video/x-flv" },

        // Archives
        { ".zip",  "application/zip" },
        { ".gz",   "application/gzip" },
        { ".tar",  "application/x-tar" },
        { ".rar",  "application/vnd.rar" },
        { ".7z",   "application/x-7z-compressed" },

        // Code / Text
        { ".txt",  "text/plain" },
        { ".html", "text/html" },
        { ".htm",  "text/html" },
        { ".css",  "text/css" },
        { ".js",   "application/javascript" },
        { ".json", "application/json" },
        { ".xml",  "application/xml" },
        { ".yaml", "application/x-yaml" },
        { ".yml",  "application/x-yaml" },
        { ".md",   "text/markdown" },

        // Data / Research
        { ".sav",  "application/x-spss-sav" },
        { ".spss", "application/x-spss-sav" },
        { ".sps",  "application/x-spss-syntax" },
        { ".por",  "application/x-spss-por" },
        { ".parquet", "application/x-parquet" },

        // Fonts
        { ".woff",  "font/woff" },
        { ".woff2", "font/woff2" },
        { ".ttf",   "font/ttf" },
        { ".otf",   "font/otf" },
    };

    public static string GetMimeType(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "application/octet-stream";

        if (!extension.StartsWith('.'))
            extension = "." + extension;

        return Map.TryGetValue(extension, out var mime) ? mime : "application/octet-stream";
    }
}
