using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;

namespace Common.Services
{
	/// <summary>
	/// GCP implementation of IBillsBuckets
	/// </summary>


	public class BillsBucketsGCP : IBillsBuckets
	{
		private readonly ILogger<BillsBucketsGCP> _logger;
		private readonly StorageClient _storageClient;
		private DownloadObjectOptions _downloadOpts = null; // we could try setting this if needed or pass at init
		private UploadObjectOptions _uploadOpts = null; // we could try setting this if needed or pass at init
		private DeleteObjectOptions _deleteOpts = null;
		public BillsBucketsGCP(ILogger<BillsBucketsGCP> logger)
		{
			_storageClient = Google.Cloud.Storage.V1.StorageClient.Create();
			_logger = logger;
		}

		public async Task DeleteFileAsync(string bucket, string path, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("GCP DeleteFileAsync, {bucket}, {path}", bucket, path);
			await _storageClient.DeleteObjectAsync(bucket, path, _deleteOpts, cancellationToken);
		}

		public async Task WriteToStreamAsync(string bucket, string path, Stream wstream, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("GCP WriteToStreamAsync, {bucket}, {path}", bucket, path);
			await _storageClient.DownloadObjectAsync(bucket, path, wstream, _downloadOpts, cancellationToken);
		}

		public async Task<List<BucketFile>> ListFilesAsync(string bucket, string path, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("GCP ListFilesAsync, {bucket}, {path}", bucket, path);
			var list = new List<BucketFile>();
			var objects = _storageClient.ListObjectsAsync(bucket);
			var iterator = objects.GetAsyncEnumerator(cancellationToken);
			try
			{
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var obj = iterator.Current;

					byte[] data = Convert.FromBase64String(obj.Md5Hash);
					string md5_hex = Convert.ToHexString(data).ToLower();

					list.Add(new BucketFile()
					{
						Bucket = bucket,
						Path = path,
						Name = obj.Name,
						Created = obj.TimeCreatedDateTimeOffset.Value.UtcDateTime,
						Hash = md5_hex,
						HashType = "md5",
						Id = obj.Id,
						Mime = obj.ContentType,
						Size = obj.Size,
						StorageType = "GCP"
					});

				}
			}
			catch (Exception e)
			{
				_logger.LogError("GCP list bucket items error, {m}", e.Message);
			}
			finally
			{
				await iterator.DisposeAsync().ConfigureAwait(false);
			}

			return list;
		}

		public async Task PutFileAsync(string bucket, string path, byte[] data, string mime = null, CancellationToken cancellationToken = default)
		{
			using var ms = new MemoryStream(data);
			await PutFileAsync(bucket, path, ms, mime, cancellationToken);
		}

		public async Task PutFileAsync(string bucket, string path, Stream istream, string mime = null, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("GCP PutFileAsync, {bucket}, {path}", bucket, path);
			await _storageClient.UploadObjectAsync(bucket, path, mime, istream, _uploadOpts, cancellationToken);
		}

		public async Task PutFileAsync(string bucket, string path, string localFilename, string mime = null, CancellationToken cancellationToken = default)
		{
			using var ms = new FileStream(localFilename, FileMode.Open, FileAccess.Read);
			await PutFileAsync(bucket, path, ms, mime, cancellationToken);
		}

		public async Task DownloadFileAsync(string bucket, string path, string localFilename, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("GCP DownloadFileAsync, {bucket}, {path}, {localFilename}", bucket, path, localFilename);
			using var fs = new FileStream(localFilename, FileMode.Create, FileAccess.Write);
			await WriteToStreamAsync(bucket, path, fs, cancellationToken);
		}

		/// <summary>
		/// create_arg is your project id
		/// </summary>
		/// <param name="bucket"></param>
		/// <param name="create_arg"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task CreateBucketAsync(string bucket, string create_arg, CancellationToken cancellationToken = default)
		{
			await _storageClient.CreateBucketAsync(create_arg, bucket, null, cancellationToken);
		}

		public async Task DeleteBucketAsync(string bucket, CancellationToken cancellationToken = default)
		{
			await _storageClient.DeleteBucketAsync(bucket, null, cancellationToken);
		}

		public async Task<List<string>> ListBucketsAsync(string project_id, CancellationToken cancellationToken = default)
		{
			var ret = new List<string>();
			var iterator = _storageClient.ListBucketsAsync(project_id).GetAsyncEnumerator(cancellationToken);
			try
			{
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var obj = iterator.Current;
					ret.Add(obj.Name);
				}
			}
			catch (Exception e)
			{
				_logger.LogError("GCP list buckets error, {m}", e.Message);
				// will finally be called if I throw here? I think so
			}
			finally
			{
				await iterator.DisposeAsync().ConfigureAwait(false);
			}
			return ret;
		}
	}
}
