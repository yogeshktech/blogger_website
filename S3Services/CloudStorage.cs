using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using POPI_TRACKER_BACKEND.S3Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HyperDroid.CloudKit
{
    /// <summary>
    /// Self-contained S3-compatible cloud storage client.
    /// Works with AWS S3, MinIO, Wasabi, DigitalOcean Spaces, Cloudflare R2, etc.
    /// </summary>
    public class CloudStorage : IDisposable
    {
        private readonly AmazonS3Client _client;
        private readonly CloudStorageConfig _config;
        private bool _disposed;

        public CloudStorage(CloudStorageConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            var credentials = new BasicAWSCredentials(config.AccessKey, config.SecretKey);

            var s3Config = new AmazonS3Config
            {
                ServiceURL = config.ServiceUrl,
                ForcePathStyle = true,                    // IMPORTANT: Must be true for MinIO
                UseHttp = config.ServiceUrl?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            };

            _client = new AmazonS3Client(credentials, s3Config);
        }

        // ────────────────────────────────────────────────────────────────
        // Upload
        // ────────────────────────────────────────────────────────────────
        public async Task<UploadResult> UploadFileAsync(
            string localFilePath,
            string? key = null,
            string? contentType = null,
            Dictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("File not found", localFilePath);

            var fileInfo = new FileInfo(localFilePath);
            key ??= fileInfo.Name;
            contentType ??= MimeTypes.GetMimeType(fileInfo.Extension);

            await using var stream = File.OpenRead(localFilePath);
            return await UploadStreamAsync(stream, key, fileInfo.Name, contentType, fileInfo.Length, metadata, cancellationToken);
        }

        public async Task<UploadResult> UploadStreamAsync(
            Stream stream,
            string key,
            string? fileName = null,
            string? contentType = null,
            long sizeBytes = 0,
            Dictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be empty", nameof(key));

            contentType ??= MimeTypes.GetMimeType(Path.GetExtension(key));

            var request = new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                AutoCloseStream = false,
                CannedACL = S3CannedACL.PublicRead
            };

            if (metadata != null)
            {
                foreach (var (k, v) in metadata)
                    request.Metadata.Add(k, v);
            }

            var response = await _client.PutObjectAsync(request, cancellationToken);

            return new UploadResult
            {
                Key = key,
                BucketName = _config.BucketName,
                ETag = response.ETag,
                VersionId = response.VersionId,
                SizeBytes = sizeBytes > 0 ? sizeBytes : (stream.CanSeek ? stream.Length : 0),
                ContentType = contentType,
                FileName = fileName ?? Path.GetFileName(key),
                Url = BuildUrl(key)
            };
        }

        public async Task<UploadResult> UploadBytesAsync(
            byte[] data,
            string key,
            string? fileName = null,
            string? contentType = null,
            Dictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream(data);
            return await UploadStreamAsync(stream, key, fileName, contentType, data.Length, metadata, cancellationToken);
        }

        // ────────────────────────────────────────────────────────────────
        // List Files
        // ────────────────────────────────────────────────────────────────
        public async Task<List<CloudFileInfo>> ListFilesAsync(
            string? prefix = null,
            bool includeUrls = false,
            int urlExpiryMinutes = 60,
            int maxKeys = 1000,
            CancellationToken cancellationToken = default)
        {
            var results = new List<CloudFileInfo>();
            string? continuationToken = null;

            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _config.BucketName,
                    Prefix = prefix,
                    MaxKeys = Math.Min(maxKeys - results.Count, 1000),
                    ContinuationToken = continuationToken
                };

                var response = await _client.ListObjectsV2Async(request, cancellationToken);

                foreach (var obj in response.S3Objects)
                {
                    if (obj.Size == 0 && obj.Key.EndsWith('/')) continue; // Skip folder markers

                    var file = new CloudFileInfo
                    {
                        Key = obj.Key,
                        SizeBytes = (long)obj.Size,
                        LastModifiedUtc = (obj.LastModified ?? DateTime.UtcNow).ToUniversalTime(),
                        ETag = obj.ETag,
                        StorageClass = obj.StorageClass?.Value
                    };

                    if (includeUrls)
                        file.Url = GeneratePreSignedUrl(obj.Key, urlExpiryMinutes);

                    results.Add(file);
                }

                continuationToken = response.IsTruncated is true ? response.NextContinuationToken : null;
            }
            while (continuationToken != null && results.Count < maxKeys);

            return results;
        }

        // ────────────────────────────────────────────────────────────────
        // Download
        // ────────────────────────────────────────────────────────────────
        public async Task DownloadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default)
        {
            var response = await _client.GetObjectAsync(_config.BucketName, key, cancellationToken);
            await using var fileStream = File.Create(localFilePath);
            await response.ResponseStream.CopyToAsync(fileStream, cancellationToken);
        }

        public async Task<Stream> DownloadStreamAsync(string key, CancellationToken cancellationToken = default)
        {
            var response = await _client.GetObjectAsync(_config.BucketName, key, cancellationToken);
            return response.ResponseStream;
        }

        public async Task<byte[]> DownloadBytesAsync(string key, CancellationToken cancellationToken = default)
        {
            var response = await _client.GetObjectAsync(_config.BucketName, key, cancellationToken);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, cancellationToken);
            return ms.ToArray();
        }

        // ────────────────────────────────────────────────────────────────
        // Delete
        // ────────────────────────────────────────────────────────────────
        public async Task<bool> DeleteFileAsync(string key, CancellationToken cancellationToken = default)
        {
            var response = await _client.DeleteObjectAsync(_config.BucketName, key, cancellationToken);
            return (int)response.HttpStatusCode is >= 200 and < 300;
        }

        public async Task<int> DeleteFilesAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            var keyList = keys.Select(k => new KeyVersion { Key = k }).ToList();
            if (keyList.Count == 0) return 0;

            var request = new DeleteObjectsRequest
            {
                BucketName = _config.BucketName,
                Objects = keyList
            };

            var response = await _client.DeleteObjectsAsync(request, cancellationToken);
            return response.DeletedObjects.Count;
        }

        // ────────────────────────────────────────────────────────────────
        // Utilities
        // ────────────────────────────────────────────────────────────────
        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.GetObjectMetadataAsync(_config.BucketName, key, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<CloudFileInfo?> GetFileInfoAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var meta = await _client.GetObjectMetadataAsync(_config.BucketName, key, cancellationToken);

                return new CloudFileInfo
                {
                    Key = key,
                    SizeBytes = meta.ContentLength,
                    LastModifiedUtc = (meta.LastModified ?? DateTime.UtcNow).ToUniversalTime(),
                    ETag = meta.ETag,
                    StorageClass = meta.StorageClass?.Value
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public string GeneratePreSignedUrl(string key, int expiryMinutes = 60)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
            };
            return _client.GetPreSignedURL(request);
        }

        public string GeneratePreSignedUploadUrl(string key, string? contentType = null, int expiryMinutes = 60)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
                ContentType = contentType
            };
            return _client.GetPreSignedURL(request);
        }

        public async Task<UploadResult> CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
        {
            var request = new CopyObjectRequest
            {
                SourceBucket = _config.BucketName,
                SourceKey = sourceKey,
                DestinationBucket = _config.BucketName,
                DestinationKey = destinationKey
            };

            var response = await _client.CopyObjectAsync(request, cancellationToken);

            return new UploadResult
            {
                Key = destinationKey,
                BucketName = _config.BucketName,
                ETag = response.ETag,
                VersionId = response.VersionId,
                Url = BuildUrl(destinationKey)
            };
        }

        // ────────────────────────────────────────────────────────────────
        // Internal helpers
        // ────────────────────────────────────────────────────────────────
        private string BuildUrl(string key)
        {
            if (!string.IsNullOrWhiteSpace(_config.PublicBaseUrl))
                return $"{_config.PublicBaseUrl.TrimEnd('/')}/{key}";

            if (!string.IsNullOrWhiteSpace(_config.ServiceUrl))
            {
                var baseUrl = _config.ServiceUrl.TrimEnd('/');
                return _config.ForcePathStyle
                    ? $"{baseUrl}/{_config.BucketName}/{key}"
                    : $"{baseUrl}/{key}";
            }

            // AWS S3 default fallback
            return $"https://{_config.BucketName}.s3.{_config.Region}.amazonaws.com/{key}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}