using Microsoft.Extensions.Options;
using WebSite.Models;

namespace Common.Services
{
	public class UserFileManagementFolder : IUserFileManagement
	{
		private readonly IWebHostEnvironment _environment;
		protected readonly ILogger _logger;
		protected readonly string _UploadsFolder;
		public UserFileManagementFolder(IWebHostEnvironment environment, 
			IConfiguration config, ILogger<UserFileManagementFolder> logger)
		{
			_environment = environment;
			_logger = logger;
			_UploadsFolder = config["AppSettings:UploadsFolder"];

		}

		// send the file to a stream (typically Response.Body)
		// implementation class specific
		public async Task WriteStreamFromStorageAsync(int accountId, string filename, Stream wstream)
		{
			var fullFilename = Path.Combine(_environment.ContentRootPath, _UploadsFolder, accountId.ToString(), filename);
			FileInfo inputFile = new FileInfo(fullFilename);
			using (FileStream originalFileStream = inputFile.OpenRead())
			{
				await originalFileStream.CopyToAsync(wstream);
			}
		}

		public async Task WriteStreamToStorageAsync(Stream istream, int accountId, string filename, string mime)
		{
			var path = Path.Combine(_environment.ContentRootPath, _UploadsFolder, accountId.ToString());
			var fullFilename = Path.Combine(path, filename);
			Directory.CreateDirectory(path);
			FileInfo inputFile = new FileInfo(fullFilename);
			using (FileStream outputFileStream = inputFile.OpenWrite())
			{
				await istream.CopyToAsync(outputFileStream);
			}
		}

		public Task Delete(int accountId, string filename)
		{
			var path = Path.Combine(_environment.ContentRootPath, _UploadsFolder, accountId.ToString());
			var fullFilename = Path.Combine(path, filename);
			File.Delete(fullFilename);
			return Task.CompletedTask;
		}
	}
}
