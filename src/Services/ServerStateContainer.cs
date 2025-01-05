using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
namespace Bills.Common.Services
{

	public interface IServerStateContainer
	{
		//public Task<T> FetchAsync<T>(string key, T def_val);
		//public Task AddAsync<T>(string key, T val);
		public T Fetch<T>(string key, T def_val);
		public void Add<T>(string key, T val);
	}


	/// <summary>
	/// really just use Blazored.LocalStorage (nuget)
	/// https://github.com/Blazored/LocalStorage
	/// 
	/// This thing is NOT persistant but handles a user refresh
	/// all goes away if app restarted or has multi instances
	/// 
	/// best to use browser storage and user prefs from server side persistance
	/// </summary>

	public class ServerStateContainer : IServerStateContainer
	{
		private readonly IHttpContextAccessor _context;
		//private readonly Blazored.LocalStorage.ILocalStorageService _localStorage;

		private ConcurrentDictionary<string, ConcurrentDictionary<string, object> > _stuff { get; set; }

		protected virtual ConcurrentDictionary<string, object> FetchFromServerSide(string user_id) 
		{
			return null;
		}

		public ServerStateContainer(IHttpContextAccessor context
			//, Blazored.LocalStorage.ILocalStorageService localStorage
			)
		{
			_context = context;
			_stuff = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();
			//_localStorage = localStorage;
		}

		public T Fetch<T>(string key, T def_val)
		{
			ConcurrentDictionary<string, object> user_dict = GetUserDict();

			object val;
			if (user_dict.TryGetValue(key, out val))
				return (T)val;
			user_dict[key] = def_val;
			return def_val;
		}

		public void Add<T>(string key, T val)
		{
			GetUserDict()[key] = val;
		}

		protected ConcurrentDictionary<string, object> GetUserDict()
		{
			ConcurrentDictionary<string, object> user_dict;

			if (_context.HttpContext?.User?.Identity?.Name != null)
			{
				if (!_stuff.TryGetValue(_context.HttpContext.User.Identity.Name, out user_dict))
				{
					user_dict = FetchFromServerSide(_context.HttpContext.User.Identity.Name);
					user_dict = new ConcurrentDictionary<string, object>();
					_stuff[_context.HttpContext.User.Identity.Name] = user_dict;

				}
				return user_dict;
			}

			return new ConcurrentDictionary<string, object>();
		}
	}
}
