using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Classes
{

	// simple wrapper for a FileSystemWatcher
	// it only handles folders, and can take multiple filters separated by |
	// filter can also be an re in slashes /xxx/ -- only ONE regex (ie NOT "/\.a$/ | *.b")
	public class FolderEventQueue : IDisposable
	{
		private FileSystemWatcher _folderWatcher;
		private string _folder, _filter;
		public string FolderPath
		{
			get { return _folder; }
		}

		// original user passed filter
		public string Filter
		{
			get { return _filter; }
		}

		// auto-gen reg ex string from _filter
		public string FilterRE
		{
			get { return _filterREstr; }
		}
		public Action<string> OnFileAdded { get; set; }
		public Action<string> OnFileDeleted { get; set; }

		private Regex _filterRE;
		private string _filterREstr;
		public FolderEventQueue(string folderPath, string filter, bool sub_dirs = false)
		{
			_folder = folderPath;
			SetFilter(filter);
			_folderWatcher = new FileSystemWatcher();
			_folderWatcher.Path = _folder;
			_folderWatcher.Filter = "*.*";
			_folderWatcher.IncludeSubdirectories = sub_dirs;
			//_queueWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
			_folderWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
			// files moved into the folder will NOT trigger an event
			_folderWatcher.Renamed += (object source, RenamedEventArgs e) => OnQueueFileAdded(e.FullPath);       // handles file moved events (no it doesn't)
			_folderWatcher.Changed += (object source, FileSystemEventArgs e) => OnQueueFileAdded(e.FullPath);    // normal file closed events
			_folderWatcher.Deleted += (object source, FileSystemEventArgs e) => OnQueueFileDeleted(e.FullPath);
			_folderWatcher.EnableRaisingEvents = true;

		}


		// can throw
		public void SetFilter(string filter)
		{
			_filter = filter.Trim();
			if (_filter.StartsWith('/') && _filter.EndsWith('/'))
			{
				_filterREstr = _filter.Trim('/');
			}
			else {

				_filterREstr = "";
				foreach (var ext in _filter.Split('|'))
				{
					var s = ext.Trim();
					if (s.Length > 0)
					{
						s = s.Replace(".", @"\.");
						s = s.Replace("*", @".*");
						s = s.Replace('?', '.');
						if (_filterREstr.Length > 0)
							_filterREstr += "|";
						_filterREstr += s;
						_filterREstr += "$";
					}
				}
			}

			_filterRE = new Regex(_filterREstr, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		}


		protected bool FilterCheck(string fullPath)
		{
			var ext = Path.GetExtension(fullPath);
			return _filterRE.IsMatch(ext);
		}

		// convenience function - I think
		// await obj.ManualCheck(file =>{ do somthing; return true;}, cTok);
		//		
		public async Task<bool> ManualCheck(Func<string, Task<bool>> processFile, CancellationToken stoppingToken = default)
		{
			string[] fileEntries = Directory.GetFiles(_folder);
			foreach (string filename in fileEntries)
			{
				if (stoppingToken.IsCancellationRequested)
					return false;
				if (FilterCheck(filename))
				{
					if (!await processFile(filename))
						return false;
				}
			}
			return true;
		}

		private void OnQueueFileAdded(string fullPath)
		{
			if (FilterCheck(fullPath))
			{
				OnFileAdded?.Invoke(fullPath);
			}
		}
		private void OnQueueFileDeleted(string fullPath)
		{
			if (FilterCheck(fullPath)) // yes apply the filter here as well
			{
				OnFileDeleted?.Invoke(fullPath);
			}
		}
		protected virtual void Dispose(bool disposing)
		{
			if (disposing && _folderWatcher != null)
			{
				_folderWatcher.Renamed -= (object source, RenamedEventArgs e) => OnQueueFileAdded(e.FullPath); // does this even make sense?
				_folderWatcher.Changed -= (object source, FileSystemEventArgs e) => OnQueueFileAdded(e.FullPath);
				_folderWatcher.Deleted -= (object source, FileSystemEventArgs e) => OnQueueFileDeleted(e.FullPath);
				_folderWatcher.Dispose();
			}
		}
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}


	}
}
