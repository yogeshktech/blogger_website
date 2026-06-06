namespace POPI_TRACKER_BACKEND.S3Services
{
    /// <summary>Metadata for a single file/object in cloud storage.</summary>
    public class CloudFileInfo
    {
        /// <summary>Full object key.</summary>
        public required string Key { get; set; }

        /// <summary>File name (last segment of the key).</summary>
        public string FileName => Key.Contains('/') ? Key[(Key.LastIndexOf('/') + 1)..] : Key;

        /// <summary>File size in bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>Last modified timestamp (UTC).</summary>
        public DateTime LastModifiedUtc { get; set; }

        /// <summary>ETag (usually MD5 hash, quoted).</summary>
        public string? ETag { get; set; }

        /// <summary>Storage class (STANDARD, REDUCED_REDUNDANCY, GLACIER, etc.).</summary>
        public string? StorageClass { get; set; }

        /// <summary>Public/pre-signed URL if generated.</summary>
        public string? Url { get; set; }

    }
}
