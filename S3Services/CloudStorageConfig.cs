namespace POPI_TRACKER_BACKEND.S3Services
{

    /// <summary>
    /// Configuration for any S3-compatible storage provider.
    /// Works with AWS S3, DigitalOcean Spaces, Wasabi, Cloudflare R2, MinIO, BackBlaze B2, etc.
    /// </summary>
    public class CloudStorageConfig
    {
        /// <summary>Bucket name.</summary>
        public required string BucketName { get; set; }

        /// <summary>Access key (API key).</summary>
        public required string AccessKey { get; set; }

        /// <summary>Secret key.</summary>
        public required string SecretKey { get; set; }

        /// <summary>
        /// Region (e.g. "us-east-1", "nyc3", "eu-central-1").
        /// For providers that don't use regions (R2, MinIO), use any valid region string like "auto" or "us-east-1".
        /// </summary>
        public string Region { get; set; } = "us-east-1";

        /// <summary>
        /// Custom service endpoint URL. Required for non-AWS providers.
        /// Examples:
        ///   Wasabi: "https://s3.wasabisys.com"
        ///   DigitalOcean: "https://nyc3.digitaloceanspaces.com"
        ///   Cloudflare R2: "https://{account_id}.r2.cloudflarestorage.com"
        ///   MinIO: "http://localhost:9000"
        /// Leave null for AWS S3.
        /// </summary>
        public string? ServiceUrl { get; set; }

        /// <summary>
        /// Use path-style addressing (bucket in path, not subdomain).
        /// Set to true for MinIO, R2, and most self-hosted S3-compatible stores.
        /// Default: false (virtual-hosted style, standard for AWS/Wasabi/DO).
        /// </summary>
        public bool ForcePathStyle { get; set; } = false;

        /// <summary>
        /// Optional: Custom public base URL for generating download links.
        /// If set, GetPublicUrl() returns this + key. Useful for CDN domains.
        /// Example: "https://cdn.example.com"
        /// </summary>
        public string? PublicBaseUrl { get; set; }
    }

}
