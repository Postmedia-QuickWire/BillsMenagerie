using Microsoft.Extensions.Options;
using StreetPerfect.Controllers;
using System.Net;
using WebSite.Models;
using Common.Services;

namespace Common.Classes
{

	/// <summary>
	/// handles downloading files from BB Storage Bucket
	/// </summary>
	public class FilesDownloadBB : FilesDownloadBase, IFilesDownload
	{
		private readonly ILogger<FilesDownloadBB> _logger;
		private readonly IBillsBuckets _bbClient;
		private readonly string _bucketName;
		public FilesDownloadBB(IBillsBuckets bbClient, IOptions<AppSettings> settings, ILogger<FilesDownloadBB> logger) : base(settings)
		{
			_logger = logger;
			_bbClient = bbClient;
			_bucketName = settings.Value.GetBucketName(settings.Value.DownloadsBucketName);
		}


		public override async Task<bool> ScanDownloads()
		{
			_logger.LogInformation("Scanning downloads bucket: {b}", _bucketName);

			//var cts = new CancellationTokenSource();

			try
			{
				var objects = await _bbClient.ListFilesAsync(_bucketName, "");
				ClearDownloads(); // incase we're re-scanning

				//Google.Apis.Storage.v1.Data.Object
				foreach (var obj in objects)
				{
					var item = InitializeItem(obj.Name, obj.Size, obj.Created, obj.StorageType, obj.Hash, obj.HashType);
					if (item == null)
					{
						_logger.LogError("No match for Storage object in {b}, file={f}", _bucketName, obj.Name);
					}
					else
					{
						_logger.LogInformation("Found download in bucket '{b}', file={f}", _bucketName, obj.Name);
					}
				}
				return await base.ScanDownloads(); // our files take precedence, but we also NEED to call this after

			}
			catch (Exception e)
			{
				_logger.LogError("ScanDownloads error, {m}", e.Message);
			}
			return false;
		}

		// send the file to a stream (typically Response.Body)
		// implementation class specific
		public override async Task<bool> WriteToStreamAsync(string dl_id, int? ind, Stream wstream)
		{
			var item = GetItem(dl_id);
			if (item != null)
			{
				//if (item.StorageType == "GCP" || item.StorageType == "AWS") // check this what other types are there??? not needed now?
				//{
					var file = item.Files[0];
					if (ind.HasValue)
					{
						file = item.Files[ind.Value]; // could throw index error
					}

					await _bbClient.WriteToStreamAsync(_bucketName, file.Filename, wstream);
				//}
				//else
				//{
				//	return await base.WriteToStreamAsync(dl_id, ind, wstream);
				//}
			}
			return false;
		}

	}
}
