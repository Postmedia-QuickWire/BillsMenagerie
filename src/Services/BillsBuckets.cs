using Amazon.S3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Common.Services
{
	/// <summary>
	/// I've created a common class here to handle both GCP and AWS buckets
	/// As a good oop'er we have an interface and a common base class
	/// Should be able to add an instance type as a Transient service and inject as IBillsBuckets
	/// 
	/// A good test here will be to use this in all the other crap I have for GCP/AWS implementation classes
	/// </summary>

	public class BucketException : Exception
	{
		public BucketException(string error) : base(error){ }
	}

	public interface IBillsBuckets
	{
		public Task PutFileAsync(string bucket, string path, byte[] data, string mime = null, CancellationToken cancellationToken = default);
		public Task PutFileAsync(string bucket, string path, Stream ds, string mime = null, CancellationToken cancellationToken = default);
		public Task PutFileAsync(string bucket, string path, string localFilename, string mime = null, CancellationToken cancellationToken = default);

		// couldn't call this GetFile() as it doesn't return anything
		// WriteToStreamAsync means DOWNLOAD bucket file to the writeable stream, I guess I could add a write to local file imp
		public Task WriteToStreamAsync(string bucket, string path, Stream wstream, CancellationToken cancellationToken = default);
		public Task DownloadFileAsync(string bucket, string path, string localFilename, CancellationToken cancellationToken = default);

		public Task DeleteFileAsync(string bucket, string path, CancellationToken cancellationToken = default);

		public Task<List<BucketFile>> ListFilesAsync(string bucket, string path, CancellationToken cancellationToken = default);

		public Task CreateBucketAsync(string bucket, string create_arg, CancellationToken cancellationToken = default);
		public Task DeleteBucketAsync(string bucket, CancellationToken cancellationToken = default);
		public Task<List<string>> ListBucketsAsync(string bucket, CancellationToken cancellationToken = default);

	}


	// we could add owner as well
	// we should have an way to generate a bucket URL - pass BucketFile obj to IBillsBuckets interface like string MakeUrl(BucketFile obj)
	public class BucketFile
	{
		public string Id { get; set; }		//needed? AWS doesn't have it
		public string Bucket { get; set; }
		public string Path { get; set; }
		public string Name { get; set; }	// same as Path?? should parse if not
		public string Mime { get; set; }
		public ulong? Size { get; set; }
		public string Hash { get; set; }    // needed? both have this
		public string HashType { get; set; }    // needed? both have this

		public DateTime? Created { get; set; }

		public string StorageType { get; set; }  // up to the implementation to set as they want (this is NOT StoreageClass - but I could store that too)
	}

#if bucket_tests
	public class TestBillsBuckets
	{
		private readonly ILoggerFactory _loggerFactory;
		private readonly IAmazonS3 _s3client;
		private readonly IBillsBuckets _bbClient;

		public TestBillsBuckets(IBillsBuckets bbClient, ILoggerFactory loggerFactory, IAmazonS3 s3client) 
		{
			_loggerFactory = loggerFactory;
			_s3client = s3client;
			_bbClient = bbClient;
		}

		public async Task<string> RunTestInjected(string bucket, string project_id, string bucket_list, string localFilename)
		{
			return await RunBucketTest("Injected", bucket, project_id, localFilename, bucket_list, _bbClient);
		}


		public async Task<string> RunTestGCP(string bucket, string project_id, string bucket_list, string localFilename)
		{
			var bucketClient = new BillsBucketsGCP(_loggerFactory.CreateLogger<BillsBucketsGCP>());

			return await RunBucketTest("GPC", bucket, project_id, localFilename, bucket_list, bucketClient);
		}

		public async Task<string> RunTestAWS(string bucket, string bucket_list, string localFilename)
		{
			var bucketClient = new BillsBucketsAWS(_s3client, _loggerFactory.CreateLogger<BillsBucketsAWS>());

			return await RunBucketTest("AWS", bucket, null, localFilename, bucket_list, bucketClient);
		}

		protected async Task<string> RunBucketTest(string test_name, string bucket, string create_arg, string localFilename
			, string bucket_list, IBillsBuckets bucketImp)
		{
			var msg = new StringBuilder();
			var fname = Path.GetFileName(localFilename);
			var test_path = $"folder_test/{fname}";

			msg.AppendLine($"running {test_name} bucket test");
			try
			{
				msg.AppendLine("initial delete bucket");
				await bucketImp.DeleteBucketAsync(bucket);
			}
			catch(Exception ex) 
			{
				msg.AppendLine($"initial delete failed, {ex.Message}");
			}
			try
			{
				msg.AppendLine("CreateBucketAsync");
				await bucketImp.CreateBucketAsync(bucket, create_arg);

				msg.AppendLine("ListBucketsAsync");
				foreach (var b in await bucketImp.ListBucketsAsync("streetperfect"))
				{
					msg.Append("\t");
					msg.AppendLine(b);
				}

				msg.AppendLine("PutFileAsync");
				await bucketImp.PutFileAsync(bucket, test_path, localFilename);

				msg.AppendLine("DownloadFileAsync");
				await bucketImp.DownloadFileAsync(bucket, test_path, localFilename + ".dl_test");

				msg.AppendLine("DeleteFileAsync");
				await bucketImp.DeleteFileAsync(bucket, test_path);

				msg.AppendLine("DeleteBucketAsync");
				await bucketImp.DeleteBucketAsync(bucket);

				msg.AppendLine("ListBucketsAsync");
				foreach (var b in await bucketImp.ListBucketsAsync("streetperfect"))
				{
					msg.Append("\t");
					msg.AppendLine(b);
				}

				msg.AppendLine("ListFilesAsync");
				foreach (var f in await bucketImp.ListFilesAsync(bucket_list, ""))
				{
					msg.Append("\t");
					msg.AppendLine($"{f.Name}\t{f.Size}\t{f.Created}");
				}
			} 
			catch(Exception ex) 
			{
				msg.AppendLine($"\nerror: {ex.Message}");
			}
			return msg.ToString();
		}
	}
#endif
}
