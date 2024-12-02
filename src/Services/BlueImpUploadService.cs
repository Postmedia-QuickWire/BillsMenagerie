using System.Net.Mime;
using WebSite.Models;

namespace Common.Services
{

	public interface IBlueImpUploadService
	{
		public Task<List<BlueImpFile>> UploadAsync(string uploadTempFolder, HttpContext httpContext);
	}

	public class BlueImpFile
	{
		public string FullName { get; set; }
		public long Size { get; set; }
	}

	public class BlueImpException : Exception
	{
		public BlueImpException(string msg) : base(msg) { }
		static public BlueImpException Raise(string msg)
		{
			throw new BlueImpException(msg);
		}

	}

	public class BlueImpUploadService : IBlueImpUploadService
	{
		private readonly ILogger<BlueImpUploadService> _logger;
		private long _MaxFileUploadSize = 1024 * 1024 * 50;

		public BlueImpUploadService(IConfiguration config, ILogger<BlueImpUploadService> logger) 
		{ 
			_logger = logger;

			var max_size_meg = config["AppSettings:MaxFileUploadSizeMeg"];
			if (!String.IsNullOrEmpty(max_size_meg)) {
				_MaxFileUploadSize = Convert.ToInt32(max_size_meg) * 1024 * 1024;
			}
		}


		public async Task<List<BlueImpFile>> UploadAsync(string uploadTempFolder, HttpContext httpContext)
		{
			var resultList = new List<BlueImpFile>();
			var httpRequest = httpContext.Request;
			//System.Diagnostics.Debug.WriteLine(Directory.Exists(tempPath));

			Directory.CreateDirectory(uploadTempFolder);

			//BILL the only difference I see in chunked uploads is a Content-Range header
			// which I ignore and just blindly append data (can it come out of sequence?)
			if (string.IsNullOrEmpty(httpRequest.Headers["Content-Range"]))
			{
				resultList = await UploadWholeFile(uploadTempFolder, httpContext);
			}
			else
			{
				// the old headers["X-File-Name"] filename thing
				// I'm guessing this was added by the OLD JQuery Upload plugin??
				// this is NOT used - even if it's present!
				// however we can fully use the content-range
				resultList = await UploadPartialFile(uploadTempFolder, httpContext);
			}
			return resultList;
		}

		private async Task<List<BlueImpFile>> UploadWholeFile(string uploadTempFolder, HttpContext httpContext)
		{
			var resultList = new List<BlueImpFile>();
			try
			{
				var request = httpContext.Request;
				foreach (IFormFile file in request.Form.Files)
				{
					string fileName = VerifyFilename(file.FileName);
					var fullPath = Path.Combine(uploadTempFolder, Path.GetFileName(fileName));

					using (var fs = new FileStream(fullPath, FileMode.OpenOrCreate))
					{
						await file.CopyToAsync(fs);
					}

					VerifyFileSize(fullPath);

					resultList.Add(MakeUploadResult(fullPath, (int)file.Length));
				}
			}
			catch (Exception e)
			{
				_logger.LogError("UploadWholeFile, error {m}", e.Message);
				throw; //rethrow
			}
			return resultList;
		}


		// I don't know how to test this
		private async Task<List<BlueImpFile>> UploadPartialFile(string uploadTempFolder, HttpContext httpContext)
		{
			var resultList = new List<BlueImpFile>();
			try
			{
				var request = httpContext.Request;
				if (request.Form.Files.Count != 1)
					throw new Exception("Attempt to upload chunked file containing more than one fragment per request");
				var file = request.Form.Files[0];
				var inputStream = file.OpenReadStream();
				string fileName = VerifyFilename(file.FileName);
				var fullName = Path.Combine(uploadTempFolder, Path.GetFileName(fileName));

				// parse the content-range header 
				// ex; "bytes 1000000-1816485/1816486"
				// where "bytes <offset-start>-<offset-end>/<total-size>"
				long offset_start = -1;
				long offset_end = -1;
				long total_bytes = -1;
				string content_range = httpContext.Request.Headers["Content-Range"];

				if (content_range.StartsWith("bytes "))
				{
					try
					{
						var s1 = content_range.Substring(6).Split('/');
						var s2 = s1[0].Split('-');
						offset_start = Convert.ToInt32(s2[0]);
						offset_end = Convert.ToInt32(s2[1]);
						total_bytes = Convert.ToInt32(s1[1]);
					}
					catch (Exception e)
					{
						total_bytes = offset_start = offset_end = -1; // make sure
						_logger.LogError(e, "UploadPartialFile content-range parse error, '{ctr}'", content_range);
					}
				}

				long bytes = 0;
				long file_len = file.Length;
				using (var fs = new FileStream(fullName, FileMode.OpenOrCreate, FileAccess.Write))
				{
					var buffer = new byte[8000];
					if (offset_start > -1 && fs.CanSeek)
						fs.Seek(offset_start, SeekOrigin.Begin);

					var l = await inputStream.ReadAsync(buffer, 0, 8000);
					while (l > 0)
					{
						fs.Write(buffer, 0, l);
						bytes += l;
						l = await inputStream.ReadAsync(buffer, 0, 8000);
					}
					await fs.FlushAsync();
					file_len = offset_start + bytes;
					fs.Close();
				}
				
				if (file_len >= total_bytes)
				{
					resultList.Add(MakeUploadResult(fullName, (int)file_len));
				}
			}
			catch (Exception e) 
			{
				_logger.LogError("UploadPartialFile, error {m}", e.Message);
				throw; //rethrow
			}

			return resultList;
		}

		private BlueImpFile MakeUploadResult(String FileFullPath, int fileSize)
		{
			var result = new BlueImpFile()
			{
				FullName = FileFullPath,
				Size = fileSize
			};

			_logger.LogInformation("new upload, {f}, {len}", FileFullPath, fileSize);

			return result;
		}


		private void VerifyFileSize(string full_filename)
		{
			FileInfo fi = new FileInfo(full_filename);
			if (_MaxFileUploadSize > 0 && fi.Length > _MaxFileUploadSize)
			{
				_logger.LogError("file size over max filename={filenaame}, size={size}, max={maxsize}", full_filename, fi.Length, _MaxFileUploadSize);
				DeleteFile(full_filename); // what the hell

				BlueImpException.Raise($"{fi.Name} size is over max size ({_MaxFileUploadSize})");
			}
		}
		private void DeleteFile(string file)
		{
			try
			{
				System.IO.File.Delete(file);
			}
			catch (Exception) { }
		}

		// removes any slashes from names
		private string VerifyFilename(string file)
		{
			file = file.Replace("/", "").Replace("\\", ""); // waste all slashes - should fix all, can leave dots

			return file;
		}


	}
}
