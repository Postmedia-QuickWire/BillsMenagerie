using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Services
{
	/// <summary>
	/// Just a Local folder implementation of IBillsBuckets
	/// </summary>

	public class BillsBucketsLocal : IBillsBuckets
	{
		private readonly ILogger<BillsBucketsLocal> _logger;
		private readonly string _localFolder;
		// we need IAmazonS3 setup at init for injection
		public BillsBucketsLocal(IConfiguration config, ILogger<BillsBucketsLocal> logger)
		{
			_logger = logger;
			_localFolder = config["AppSettings:LocalBucketsFolder"];
		}

		public async Task PutFileAsync(string bucket, string path, byte[] data, string mime = null, CancellationToken cancellationToken = default)
		{
			using var ms = new MemoryStream(data);
			var dest = MakePath(bucket, path);
			using (var fileStream = File.Create(dest))
			{
				await ms.CopyToAsync(fileStream);
			}
		}


		public async Task PutFileAsync(string bucket, string path, Stream istream, string mime = null, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("Local PutFileAsync, {bucket}, {path}", bucket, path);

			var dest = MakePath(bucket, path);
			using (var fileStream = File.Create(dest))
			{
				await istream.CopyToAsync(fileStream, cancellationToken);
			}
		}

		public async Task PutFileAsync(string bucket, string path, string localFilename, string mime = null, CancellationToken cancellationToken = default)
		{
			using var ms = new FileStream(localFilename, FileMode.Open, FileAccess.Read);
			await PutFileAsync(bucket, path, ms, mime, cancellationToken);
		}

		public async Task WriteToStreamAsync(string bucket, string path, Stream wstream, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("Local WriteToStreamAsync, {bucket}, {path}", bucket, path);

			var dest = MakePath(bucket, path);
			using (var fileStream = File.OpenRead(dest))
			{
				await fileStream.CopyToAsync(wstream, cancellationToken);
			}

		}

		public Task DeleteFileAsync(string bucket, string path, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("Local DeleteFileAsync, {bucket}, {path}", bucket, path);
			var dest = MakePath(bucket, path);
			File.Delete(dest);
			return Task.CompletedTask;
		}

		public Task<List<BucketFile>> ListFilesAsync(string bucket, string path, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("Local ListFilesAsync, {bucket}, {path}", bucket, path);
			var list = new List<BucketFile>();

			var dest = MakePath(bucket, path, true);

			foreach (var f in Directory.GetFiles(dest))
			{

				var finfo = new FileInfo(f);
				BucketFile bucketFile = new BucketFile()
				{
					Bucket = bucket,
					Created = finfo.LastWriteTime,
					Size = (ulong)finfo.Length,

					Hash = "",
					HashType = "",
					Name = Path.GetFileName(f),
					Path = Path.GetFileName(f),
					StorageType = "LOC"
				};
				list.Add(bucketFile);
			}
			return Task.FromResult(list);
		}
		public async Task DownloadFileAsync(string bucket, string path, string localFilename, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation("Local DownloadFileAsync, {bucket}, {path}, {localFilename}", bucket, path, localFilename);
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
		public Task CreateBucketAsync(string bucket, string create_arg,  CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task DeleteBucketAsync(string bucket, CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task<List<string>> ListBucketsAsync(string not_used, CancellationToken cancellationToken = default)
		{
			var ret = new List<string>();

			return Task.FromResult(ret);
		}


		private string MakePath(string bucket, string path, bool path_is_folder = false)
		{
            if (_localFolder == null)
                throw new ArgumentException("AppSettings::LocalBucketsFolder is not defined");
			var path_parts = path.Split("/").ToList();
			path_parts.Insert(0, bucket);
			path_parts.Insert(0, _localFolder);
			var full_path = Path.Combine(path_parts.ToArray());
			if (!path_is_folder)
				path_parts.RemoveAt(path_parts.Count - 1);

			var chk_path = Path.Combine(path_parts.ToArray());
			if (!Directory.Exists(chk_path))
			{
				Directory.CreateDirectory(chk_path);
			}
			return full_path;
		}
	}
}
