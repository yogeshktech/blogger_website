using HyperDroid.CloudKit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using POPI_TRACKER_BACKEND.S3Services;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CareerCracker.S3Services
{
    public static class S3StorageHelper
    {
        private static IConfiguration? _config;

        // Call this in Program.cs after builder.Configuration is ready
        public static void Initialize(IConfiguration configuration)
        {
            _config = configuration;
        }

        private static CloudStorage? CreateClient()
        {
            var s3Section = _config?.GetSection("S3");
            if (s3Section == null) return null;

            var bucket = s3Section["BucketName"];
            var accessKey = s3Section["AccessKey"];
            var secretKey = s3Section["SecretKey"];

            if (string.IsNullOrWhiteSpace(bucket) ||
                string.IsNullOrWhiteSpace(accessKey) ||
                string.IsNullOrWhiteSpace(secretKey))
                return null;

            var cfg = new CloudStorageConfig
            {
                BucketName = bucket,
                AccessKey = accessKey,
                SecretKey = secretKey,
                ServiceUrl = s3Section["ServiceUrl"],
                PublicBaseUrl = s3Section["PublicBaseUrl"],
                Region = s3Section["Region"],
                ForcePathStyle = s3Section.GetValue<bool>("ForcePathStyle")
            };

            return new CloudStorage(cfg);
        }

        private static bool LooksLikeConnectionFailure(Exception ex)
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                if (cur is SocketException se &&
                    (se.SocketErrorCode == SocketError.ConnectionRefused ||
                     se.SocketErrorCode == SocketError.HostNotFound ||
                     se.SocketErrorCode == SocketError.TimedOut))
                    return true;
            }

            return ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
                   || ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase);
        }



        public static async Task<string?> UploadFileAsync(IFormFile file, string folderPrefix = "uploads")
        {
            if (file == null || file.Length == 0) return null;

            using var client = CreateClient();
            if (client == null) return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
            var key = $"{folderPrefix.TrimEnd('/')}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}{ext}";

            await using var stream = file.OpenReadStream();

            try
            {
                await client.UploadStreamAsync(
                    stream,
                    key,
                    file.FileName,
                    file.ContentType,
                    file.Length);

                // ✅ ADD BUCKET NAME HERE
                var s3 = _config?.GetSection("S3");

                string baseUrl = s3?["PublicBaseUrl"]?.TrimEnd('/') ?? "";
                string bucket = s3?["BucketName"] ?? "";
                if (string.IsNullOrWhiteSpace(baseUrl))
                    return $"{bucket}/{key}";

                // Avoid duplicate bucket in URL when PublicBaseUrl already contains "/{bucket}"
                if (!string.IsNullOrWhiteSpace(bucket) &&
                    baseUrl.EndsWith("/" + bucket, StringComparison.OrdinalIgnoreCase))
                    return $"{baseUrl}/{key}";

                return $"{baseUrl}/{bucket}/{key}";
            }
            catch (Exception ex) when (LooksLikeConnectionFailure(ex))
            {
                throw;
            }
        }


        public static async Task<bool> DeleteByPathAsync(string? pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl)) return true;

            using var client = CreateClient();
            if (client == null) return true;

            var key = ExtractKeyFromUrl(pathOrUrl);
            if (string.IsNullOrWhiteSpace(key)) return true;

            return await client.DeleteFileAsync(key);
        }

        /// <summary>
        /// Removes legacy files under wwwroot (e.g. /uploads/blogs/...) or deletes the object when <paramref name="pathOrUrl"/> is an http(s) URL from S3/MinIO.
        /// </summary>
        public static async Task DeleteStoredMediaAsync(string? pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl)) return;

            var s = pathOrUrl.Trim();
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                await DeleteByPathAsync(s);
                return;
            }

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", s.TrimStart('/', '\\'));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        private static string? ExtractKeyFromUrl(string urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath)) return null;

            var s3Section = _config?.GetSection("S3");
            var publicBase = s3Section?["PublicBaseUrl"];
            var serviceUrl = s3Section?["ServiceUrl"];
            var bucket = s3Section?["BucketName"];
            var forcePathStyle = s3Section?.GetValue<bool>("ForcePathStyle") ?? true;

            // Already a key
            if (!urlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !urlOrPath.StartsWith("/"))
                return urlOrPath;

            // Match PublicBaseUrl
            if (!string.IsNullOrWhiteSpace(publicBase) &&
                urlOrPath.StartsWith(publicBase.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                var prefix = publicBase.TrimEnd('/') + "/";
                var remainder = urlOrPath.Substring(prefix.Length);
                if (!string.IsNullOrWhiteSpace(bucket) &&
                    remainder.StartsWith(bucket + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return remainder.Substring(bucket.Length + 1);
                }
                return remainder;
            }

            // Match ServiceUrl (MinIO style)
            if (!string.IsNullOrWhiteSpace(serviceUrl) &&
                urlOrPath.StartsWith(serviceUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = serviceUrl.TrimEnd('/');
                var remainder = urlOrPath.Substring(baseUrl.Length).TrimStart('/');

                if (forcePathStyle && !string.IsNullOrWhiteSpace(bucket) &&
                    remainder.StartsWith(bucket + "/"))
                {
                    return remainder.Substring(bucket.Length + 1);
                }
                return remainder;
            }

            return null;
        }

        internal static async Task DeleteFileAsync(string oldImage)
        {
            await DeleteStoredMediaAsync(oldImage);
        }
    }
}