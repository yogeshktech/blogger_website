namespace POPI_TRACKER_BACKEND.S3Services
{
    /// <summary>Result returned after a successful file upload.</summary>
    public class UploadResult
    {
        /// <summary>Full object key in the bucket (e.g. "uploads/2026/03/abc123_report.pdf").</summary>
        public required string Key { get; set; }

        /// <summary>Bucket name.</summary>
        public required string BucketName { get; set; }

        /// <summary>ETag returned by S3 (MD5 hash of the object, quoted).</summary>
        public string? ETag { get; set; }

        /// <summary>Version ID if bucket versioning is enabled.</summary>
        public string? VersionId { get; set; }

        /// <summary>File size in bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>Content type (MIME).</summary>
        public string? ContentType { get; set; }

        /// <summary>Original file name.</summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Public URL to the object. Only meaningful if:
        /// (a) the bucket/object is publicly readable, or
        /// (b) PublicBaseUrl was configured, or
        /// (c) a pre-signed URL was generated.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>Timestamp when the upload completed (UTC).</summary>
        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    }

}
