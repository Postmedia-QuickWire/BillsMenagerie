using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using WebSite.Models;

namespace Common.Services
{
	public interface IUserFileManagement
	{
		public Task WriteStreamFromStorageAsync(int accountId, string filename, Stream wstream);
		public Task WriteStreamToStorageAsync(Stream istream, int accountId, string filename, string mime);

		public Task Delete(int accountId, string filename);
	}

}
