
using Microsoft.Extensions.Options;
using StreetPerfect.Controllers;
using System.Collections;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WebSite.Models;
using Common.Models;

namespace Common.Classes
{
	public class DownloadViewModel
	{
		public ConcurrentDictionary<string, DownloadItem> Items { get; set; }
		public WebUser User { get; set; }

		// runtime view vars
		public string id { get; set; }
		public string desc { get; set; }
		public DateTime? LastScan { get; set; }
	}
	public class DownloadItemViewModel
	{
		public DownloadViewModel ParentViewModel { get; set; }
		public string id { get; set; }

	}

	public class FileItem : ICloneable
	{
		// Filename, date and size are set at scan from real file meta
		public string Filename { get; set; }
		public string Desc { get; set; }
		public ulong? Size { get; set; }
		public DateTime? Date { get; set; }
		public DateTime? DataEffectiveDate { get; set; }
		public int Version { get; set; }
		public bool? Available { get; set; }
		public string Hash {  get; set; }
		public string HashType { get; set; }
		public object Clone()
		{
			return this.MemberwiseClone();
		}
	}

	public class DownloadItem
	{
		public string Id { get; set; } // just the key pointing to this item
		/// <summary>
		/// this is the template filename which may be a regex to find the actual filename
		/// this is required
		/// </summary>
		public string TFilename { get; set; }

		/// <summary>
		/// This is the RegEx type of the above TFilename.
		/// Currently there is date and version - used for sorting revisons in case the modification is changed
		/// 
		/// ex date "2014.08" = (\\d+)\\.(\\d+)
		/// 
		/// ex version "_v12.3.0_" = _v(\\d+)\\.(\\d+)\\.(\\d+)_
		/// </summary>
		public string ReType { get; set; }

		/// <summary>
		/// File description from appsettings
		/// </summary>
		public string Desc { get; set; }

		/// <summary>
		/// mime is required
		/// </summary>
		public string Mime { get; set; } = "application/octet-stream";

		/// <summary>
		/// RelPath not tested is optional
		/// </summary>
		public string RelPath { get; set; }

		/// <summary>
		/// allows download via API, not implemented yet
		/// </summary>
		public bool isApiDownload { get; set; } = false;

		/// <summary>
		/// Account must hold ONE of the Access Roles to download item
		/// </summary>
		public List<WebAccount.DL> Access { get; set; }


		/// <summary>
		/// Account must hold ONE or more of the Platform Roles to download an app item
		/// </summary>
		public List<WebAccount.DL> Platforms { get; set; }

		/// <summary>
		/// storage type is set at scan time by FilesDownloadBase sub class
		/// </summary>
		public string StorageType { get; set; }

		/// <summary>
		/// make all file versions avaliable
		/// </summary>
		public bool KeepAllVersions { get; set; } = false;

		/// <summary>
		/// regex to transform GET {id} into downloadItems key
		/// </summary>
		public string TGetMatch { get; set; }

		/// <summary>
		/// formatted as /find regex/replace/  handle groups in replace using $1 $2 etc.
		/// </summary>
		public string[] TTransform { get; set; }

		public List<FileItem> Files { get; set; } = new List<FileItem>();

		/// <summary>
		/// return a single file that matches id - typically will use TTransForm to create an expected filename
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public int GetFileInd(string id)
		{
			string filename = id;
			if (TTransform != null) 
			{
				filename = Regex.Replace(id, TTransform[0], TTransform[1], RegexOptions.IgnoreCase);
			}
			int ind = 0;
			foreach (FileItem item in Files)
			{
				if (item.Filename.Equals(filename, StringComparison.OrdinalIgnoreCase))
				{ 
					return ind; 
				}
				ind++;
			}
			return -1;
		}
	}

	public interface IFilesDownload
	{
		public Task<bool> WriteToStreamAsync(string dl_id, int? ind, Stream wstream);
		public DownloadItem GetItem(string id);

		public ConcurrentDictionary<string, DownloadItem> DownloadItems { get; }
		public Task<bool> ScanDownloads();
		public bool CanScanDownloads();

		public bool CanPurgeFile(string filename);
		public DateTime? LastScan { get; set; }

		public int NumRevisionsToKeep { get; }

	}
	public abstract class FilesDownloadBase
	{
		/// <summary>
		/// The idea is we scan these files at startup and update the dictionary with the filenames, size, date etc.
		/// I use a regex to help find the files regardless of the version or date tag. 
		/// This way we don't need to specifically look for a certain version or date (ie I don't need to update/rebuild the site)
		/// need to add a one line description
		/// </summary>

		protected bool hasScanned { get; set; } = false;
		protected ConcurrentDictionary<string, DownloadItem> _DownloadItems = new ConcurrentDictionary<string, DownloadItem>();

		public FilesDownloadBase(IOptions<AppSettings> settings)
		{
			// shallow copy settings.Files into DownloadItems
			NumRevisionsToKeep = settings.Value.NumRevisionsToKeep;

			if (NumRevisionsToKeep < 1)
			{
				NumRevisionsToKeep = 1; // allow removeal of everything but latest, <= 0 would waste everything....
			}

			foreach (var kv in settings.Value.Files)
			{
				_DownloadItems[kv.Key] = kv.Value;
			}

		}

		public int NumRevisionsToKeep { get; set; }
		public DateTime? LastScan { get; set; }
		public ConcurrentDictionary<string, DownloadItem> DownloadItems => _DownloadItems;


		// check if a filename is in the files list and can be deleted or not
		public bool CanPurgeFile(string filename)
		{
			foreach (var item in DownloadItems)
			{
				int cnt = 0;
				foreach (var f in item.Value.Files)
				{
					if (f.Filename.Equals(filename, StringComparison.OrdinalIgnoreCase))
					{
						if (item.Value.KeepAllVersions || cnt < NumRevisionsToKeep) // keep the latest n versions
							return false;
					}
					cnt++;
				}
			}
			return true;
		}

		// helper, kind of useless
		public DownloadItem GetItem(string id)
		{
			if (!String.IsNullOrWhiteSpace(id))
			{
				if (DownloadItems.TryGetValue(id, out DownloadItem val))
					return val;
				else
				{
					// now scan through the TGetMatch's on the id
					foreach (var kv in DownloadItems)
					{
						if (!String.IsNullOrEmpty(kv.Value.TGetMatch))
						{
							var m = Regex.Match(id, kv.Value.TGetMatch, RegexOptions.IgnoreCase);
							if (m.Success && m.Groups["1"].Value.ToLower() == kv.Key)
							{
								return kv.Value;
							}
						}
					}
				}
			}
			return null;
		}

		public virtual async Task<bool> WriteToStreamAsync(string dl_id, int? ind, Stream wstream)
		{
			/* to handle local files
			var path = Path.Join(_environment.ContentRootPath, "files", filename);
			using (var rstr = System.IO.File.OpenRead(path))
			{
				await rstr.CopyToAsync(wstream);
			}
			*/
			return await Task.FromResult(false);
		}

		/// <summary>
		/// when scanning, pass the real file name, length and date as YOUR storeage type
		/// name will be compared to TFilename possibly by regex
		/// if matched then the item will be updated
		/// </summary>
		/// <param name="name"></param>
		/// <param name="length"></param>
		/// <param name="dt"></param>
		/// <param name="storageType"></param>
		/// <param name="hash">hash tag</param>
		/// <param name="hashType">hash type</param>
		/// <returns></returns>
		protected DownloadItem InitializeItem(string name, ulong? length, DateTime? dt, string storageType, string hash, string hashType)
		{
			foreach (var item in DownloadItems)
			{
				DateTime? effdate = null;
				int version = 0;
				bool ok = false;
				if (item.Value.TFilename.StartsWith('/'))
				{
					var patstr = item.Value.TFilename.Trim('/');
					var M = Regex.Match(name, patstr, RegexOptions.IgnoreCase);
					ok = M.Success;

					// check for data files
					if (ok)
					{
						// parse out the year and month
						if (item.Value.ReType == "effdate")
						{
							int y = Convert.ToInt32(M.Groups[1].Value);
							int m = Convert.ToInt32(M.Groups[2].Value);
							effdate = new DateTime(y, m, 1);
							version = y * 100 + m;
						}
						else if (item.Value.ReType == "version") // parse version 11.22.33
						{
							int maj = Convert.ToInt32(M.Groups[1].Value);
							int min = Convert.ToInt32(M.Groups[2].Value);
							int bug = Convert.ToInt32(M.Groups[3].Value);
							version = maj * 10000 + min * 100 + bug;
						}

					}

				}
				else
				{
					ok = String.Compare(name, item.Value.TFilename, true) == 0;
				}
				if (ok)
				{
					item.Value.Id = item.Key;
					item.Value.StorageType = storageType;
					item.Value.Files.Add(new FileItem()
					{
						Filename = name,
						Desc = item.Value.Desc,
						Size = length,
						Date = dt,
						DataEffectiveDate = effdate,
						Version = version,
						Hash = hash,
						HashType = hashType,
					});
					return item.Value;
				}
			}
			return null;
		}

		// clear the downloads dict before a new scan
		public void ClearDownloads()
		{
			foreach (var item in DownloadItems)
			{
				item.Value.Files.Clear();
			}
		}

		public virtual bool CanScanDownloads()
		{
			return !hasScanned || LastScan == null || (DateTime.UtcNow - (DateTime)LastScan).TotalSeconds > 10;
		}

		// if we let the base class handle local files then this will init them
		public virtual Task<bool> ScanDownloads()
		{
			// sort any multiple files lists by newest to oldest
			// sept 2023, set it so only NEWEST item in a multi-file hit
			foreach (var item in DownloadItems)
			{
				if (item.Value?.Files?.Count > 0)
				{
					// the ReType denotes how to build the version so we use that if available
					if (!string.IsNullOrEmpty(item.Value.ReType))
					{
						item.Value.Files = item.Value.Files.OrderByDescending(f => f.Version).ToList();
					}
					else
					{
						item.Value.Files = item.Value.Files.OrderByDescending(f => f.Date).ToList();
					}
				}
			}

			hasScanned = true;
			LastScan = DateTime.UtcNow;
			return Task.FromResult(true);
		}
	}
}
