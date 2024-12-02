using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using WebSite.Models;

namespace Common.Services
{
	public class UserFileManagementBB : IUserFileManagement
	{
		private readonly IBillsBuckets _bbClient;
		private readonly string _bucketName;
		protected readonly ILogger _logger;
		protected readonly IOptions<AppSettings> _settings;
		public UserFileManagementBB(IBillsBuckets bbClient,
			IConfiguration config, ILogger<UserFileManagementBB> logger) 
		{
			_bucketName = config["AppSettings:UserUploadBucketName"];
			_logger = logger;
			_bbClient = bbClient;
		}

		// send the file to a stream (typically Response.Body)
		// implementation class specific
		public async Task WriteStreamFromStorageAsync(int accountId, string filename, Stream wstream)
		{
			var RelFilename = $"{accountId}/{filename}";
			await _bbClient.WriteToStreamAsync(_bucketName, RelFilename, wstream);
		}

		public async Task WriteStreamToStorageAsync(Stream istream, int accountId, string filename, string mime)
		{
			var RelFilename = $"{accountId}/{filename}";
			await _bbClient.PutFileAsync(_bucketName, RelFilename, istream, mime);
		}

		public async Task Delete(int accountId, string filename)
		{
			var RelFilename = $"{accountId}/{filename}";
			await _bbClient.DeleteFileAsync(_bucketName, RelFilename);
		}
	}
}
