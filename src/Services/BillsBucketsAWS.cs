using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace Common.Services
{
    /// <summary>
    /// AWS implementation of IBillsBuckets
    /// </summary>

    // not sure I really need to re-throw amazon exceptions
    public class AwsBucketException : BucketException
    {
        public AwsBucketException(AmazonS3Exception e) : base($"S3 error: {e.Message}, code: {e.ErrorCode}") { }
    }

    public class BillsBucketsAWS : IBillsBuckets
    {
        private readonly ILogger<BillsBucketsAWS> _logger;
        private readonly IAmazonS3 _s3client;

        // we need IAmazonS3 setup at init for injection
        public BillsBucketsAWS(IAmazonS3 s3client, ILogger<BillsBucketsAWS> logger)
        {
            _logger = logger;
            _s3client = s3client;
        }

        public async Task PutFileAsync(string bucket, string path, byte[] data, string mime = null, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream(data);
            try
            {
                await PutFileAsync(bucket, path, ms, mime, cancellationToken);
            }
            catch (AmazonS3Exception e)
            {
                throw new AwsBucketException(e);
            }
        }

        public async Task PutFileAsync(string bucket, string path, Stream istream, string mime = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("AWS PutFileAsync, {bucket}, {path}", bucket, path);
            await _s3client.PutObjectAsync(new PutObjectRequest()
            {
                BucketName = bucket,
                Key = path,
                ContentType = mime,
                InputStream = istream,
            }, cancellationToken);
        }

        public async Task PutFileAsync(string bucket, string path, string localFilename, string mime = null, CancellationToken cancellationToken = default)
        {
            using var ms = new FileStream(localFilename, FileMode.Open, FileAccess.Read);
            await PutFileAsync(bucket, path, ms, mime, cancellationToken);
        }

        public async Task WriteToStreamAsync(string bucket, string path, Stream wstream, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("AWS WriteToStreamAsync, {bucket}, {path}", bucket, path);
                var resp = await _s3client.GetObjectAsync(new GetObjectRequest()
                {
                    BucketName = bucket,
                    Key = path,
                });
                await resp.ResponseStream.CopyToAsync(wstream, cancellationToken);
            }
            catch (AmazonS3Exception ex)
            {
                throw new AwsBucketException(ex);
            }
        }

        public async Task DeleteFileAsync(string bucket, string path, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("AWS DeleteFileAsync, {bucket}, {path}", bucket, path);
                await _s3client.DeleteObjectAsync(bucket, path, cancellationToken);
            }
            catch (AmazonS3Exception ex)
            {
                throw new AwsBucketException(ex);
            }
        }

        public async Task<List<BucketFile>> ListFilesAsync(string bucket, string path, CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("AWS ListFilesAsync, {bucket}, {path}", bucket, path);
            var list = new List<BucketFile>();
            try
            {
                var objs = await _s3client.ListObjectsV2Async(new ListObjectsV2Request()
                {
                    BucketName = bucket,
                    Prefix = path,
                    //OptionalObjectAttributes  = new List<string>(){"Checksum",}
                }, cancellationToken);
                foreach (var o in objs.S3Objects)
                {

                    var attribs = await _s3client.GetObjectAttributesAsync(new GetObjectAttributesRequest()
                    {
                        BucketName = bucket,
                        Key = o.Key,
                        ObjectAttributes = new List<ObjectAttributes>()
                        {
                            new ObjectAttributes("Checksum")
                        }
                    }, cancellationToken);

                    string sha_hex = null;
                    if (attribs?.Checksum?.ChecksumSHA256 != null)
                    {
                        byte[] data = Convert.FromBase64String(attribs.Checksum.ChecksumSHA256);
                        sha_hex = Convert.ToHexString(data).ToLower();
                    }

                    list.Add(new BucketFile()
                    {
                        Bucket = o.BucketName,
                        Created = o.LastModified,
                        Hash = sha_hex,
                        HashType = "sha256",
                        Size = (ulong?)o.Size,
                        Name = o.Key, // parse this?  check GCP out first 
                        Path = o.Key,
                        StorageType = "AWS"
                    });
                }
            }
            catch (AmazonS3Exception ex)
            {
                throw new AwsBucketException(ex);
            }
            catch (Exception)
            {
                throw;
            }
            return list;
        }
        public async Task DownloadFileAsync(string bucket, string path, string localFilename, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("AWS DownloadFileAsync, {bucket}, {path}, {localFilename}", bucket, path, localFilename);
            using var fs = new FileStream(localFilename, FileMode.OpenOrCreate | FileMode.Truncate, FileAccess.Write);
            await WriteToStreamAsync(bucket, path, fs, cancellationToken);
        }

        /// <summary>
        /// create_arg is the aws region name or null for your default region
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="create_arg"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task CreateBucketAsync(string bucket, string create_arg, CancellationToken cancellationToken = default)
        {
            await _s3client.PutBucketAsync(new PutBucketRequest()
            {
                BucketName = bucket,
                BucketRegionName = create_arg,
                UseClientRegion = String.IsNullOrEmpty(create_arg)
            }, cancellationToken);
        }

        public async Task DeleteBucketAsync(string bucket, CancellationToken cancellationToken = default)
        {
            await _s3client.DeleteBucketAsync(new DeleteBucketRequest()
            {
                BucketName = bucket,
                UseClientRegion = true
            }, cancellationToken);
        }

        public async Task<List<string>> ListBucketsAsync(string not_used, CancellationToken cancellationToken = default)
        {
            var ret = new List<string>();
            var bucket_list = await _s3client.ListBucketsAsync(cancellationToken);
            foreach (var b in bucket_list.Buckets)
            {
                ret.Add(b.BucketName);
            }
            return ret;
        }
    }
}
