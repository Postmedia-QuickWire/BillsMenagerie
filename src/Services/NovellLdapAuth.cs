using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using System.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Novell.Directory.Ldap;
using SharpCompress.Common;
using Novell.Directory.Ldap.Utilclass;
using System.Threading;

namespace Bills.Common.Services
{

	public class NovellLdapAuth : DirectoryAuthBase, IDirectoryAuth
	{
		private readonly string _domain;
		private readonly string _ldap_host;
		private readonly string _ldap_user;
		private readonly string _ldap_user_pw;
		public NovellLdapAuth(ILogger<NovellLdapAuth> logger, IConfiguration config) : base(logger)
		{
			_domain = config["ldap:domain"];
			_ldap_host = config["ldap:host"];
			_ldap_user = config["ldap:user"];
			_ldap_user_pw = config["ldap:pw"];
			//_ldap_port = config["ldap:host"];

			VerifyConfig();

		}

		private bool VerifyConfig()
		{
			if (string.IsNullOrEmpty(_domain) || string.IsNullOrEmpty(_ldap_host) || string.IsNullOrEmpty(_ldap_user) || string.IsNullOrEmpty(_ldap_user_pw))
			{
				_logger.LogError("Missing one or more AppSettings:ldap configuration params");
				return false;
			}
			return true;
		}
		public bool test()
		{
			var aduser = new DirectoryUser()
			{
				cn = "Bill Miller",
				contextType = DirectoryUser.ContextType.Domain,
				accountName = "bmiller",
				displayName = "Bill Miller",
				dn="CN=Bill Miller,CN=Users,DC=home,DC=ca"
			};
			//CheckGroupMembership()
			var user = AuthenticateUser("bmiller", "pw", DirectoryUser.ContextType.Domain);

			var B = CheckGroupMembership(user, "QuickWire Regular", DirectoryUser.ContextType.Domain);

			// not really required ArrayList users = GetADGroupUsers("QuickWire Admin");

			return false;
		}

		// authenticates AND returns a few of the users AD properties (DN is the main one for looking up membership)
		public DirectoryUser AuthenticateUser(string user, string password, DirectoryUser.ContextType ctxType, bool isAuthenticated = false)
		{
			string domain_dn = "";
			try
			{
				if (!VerifyConfig())
				{
					throw new Exception("LDAP not configured in appSettings.json");
				}

				string domain_long = _domain;
				string domain_short = domain_long;
				int ind = domain_short.IndexOf("."); // we could change a dotted domain name into OU=domain,OU=com. ...
				if (ind > 0)
					domain_short = domain_short.Substring(0, ind);

				// check if user has a domain component
				string user_domain = domain_short;
				string user_name = user; ;
				ind = user_name.IndexOfAny(new char[] { '\\', '/' });
				if (ind > -1)
				{
					user_domain = user_name.Substring(0, ind);
					user_name = user_name.Substring(ind + 1);
				}

				domain_dn = $"dc={_domain.Replace(".", ",dc=")}";

				_logger.LogInformation("AD Auth, logging in user '{user}', ldap path={ldap}", user, domain_dn);

				int ldapPort = LdapConnection.DefaultPort;
				int searchScope = LdapConnection.ScopeSub; //ScopeSub ScopeBase
				int ldapVersion = LdapConnection.LdapV3;


				String[] attrs = new String[] {
					"cn",
					"distinguishedName",
					"displayName",
					"name",
					"mail",
					"sAMAccountName",
					"member",
					"memberOf"
				};
				LdapSearchConstraints constraints = new LdapSearchConstraints();
				constraints.TimeLimit = 20000;

				string filter = $"(&(sAMAccountName={user_name})(objectCategory=person))"; //right?
				using (var Con = new LdapConnection())
				{
					// note that Connect and Bind both throw if not connected or bound
					// connect
					Con.ConnectionTimeout = 30000;
					Con.Connect(domain_long, ldapPort);
					_logger.LogInformation("ldap {c}connected", Con.Connected ? "" : "NOT ");

					var user_dn = $"{user_domain}\\{user}";
					// bind with an username and password
					// this how you can verify the password of an user
					Con.Bind(ldapVersion, user_dn, password);
					_logger.LogInformation("ldap user {c}bound, {u}", Con.Connected ? "" : "NOT ", user_dn);

					ILdapSearchResults searchResults = Con.Search(
						domain_dn,          // object to read
						searchScope,        // scope - read single object
						filter,             // search filter
						attrs,              // return only required attributes
						false,              // return attrs and values
						constraints);       // time out value

					LdapEntry Entry = null;

					while (searchResults.HasMore())
					{
						try{
							Entry = searchResults.Next();
							break; // break on first good one
						}
						catch { }
					}

					if (Entry == null)
						throw new Exception("user authenticated by ldap but couldn't find user entry in directory");

					return new DirectoryUser()
					{
						accountName = GetAttribute(Entry, "sAMAccountName"),
						cn = GetAttribute(Entry, "cn"),
						displayName = GetAttribute(Entry, "displayName"),
						email = GetAttribute(Entry, "mail"),
						contextType = ctxType,
						dn = Entry.Dn,
					};
				}

			}
			catch (Novell.Directory.Ldap.LdapReferralException e)
			{
				_logger.LogError("LDAP Exception, failed for user '{user}', ldap path={ldap}, err={err}, m={m}"
					, user, domain_dn, e.Message, e.LdapErrorMessage);

			}
			catch (LdapException e)
			{
				_logger.LogError("LDAP Exception, failed for user '{user}', ldap path={ldap}, err={err}", user, domain_dn, e.Message);
			}
			catch (Exception e)
			{
				_logger.LogError("AD Auth, failed for user '{user}', ldap path={ldap}, err={err}", user, domain_dn, e.Message);

			}
			return null;
		}

		private string GetAttribute(LdapEntry entry, string attribute)
		{
			try
			{
				if (entry != null)
				{
					return entry.GetAttribute(attribute).StringValue;
				}
			}
			catch { }
			return null;
		}

		override protected HashSet<string> GetMembersOfGroup(string groupName, DirectoryUser.ContextType ctxType = DirectoryUser.ContextType.Domain)
		{
			string domain_dn = "";
			HashSet<string> member_list = new HashSet<string>();
			try
			{
				domain_dn = $"dc={_domain.Replace(".", ",dc=")}";

				_logger.LogInformation("AD Novell Auth, get group members for group '{g}'", groupName);

				int ldapPort = LdapConnection.DefaultPort;
				//int searchScope = LdapConnection.ScopeSub; //ScopeSub ScopeBase
				int ldapVersion = LdapConnection.LdapV3;


				String[] attrs = new String[] {
					"cn",
					"distinguishedName",
					"displayName",
					"name",
					//"mail",
					"sAMAccountName",
					"member",
					"memberOf"
				};
				LdapSearchConstraints constraints = new LdapSearchConstraints();
				constraints.TimeLimit = 10000;

				using (var Con = new LdapConnection())
				{
					// connect
					Con.Connect(_ldap_host, ldapPort);
					//Con.Bind(null, null);
					Con.Bind(ldapVersion, _ldap_user, _ldap_user_pw);

					//AddAllGroupMembers(Con, "CN=Domain Users,CN=Users,DC=home,DC=ca", member_list);
					AddAllGroupMembers(Con, $"cn={groupName}", member_list);

				}
			}
			catch (Novell.Directory.Ldap.LdapReferralException e)
			{
				_logger.LogError("LDAP Exception, ldap path={ldap}, err={err}, m={m}"
					, domain_dn, e.Message, e.LdapErrorMessage);

			}
			catch (LdapException e)
			{
				_logger.LogError("LDAP Exception, ldap path={ldap}, err={err}", domain_dn, e.Message);
			}
			catch (Exception e)
			{
				_logger.LogError("AD Auth, ldap path={ldap}, err={err}", domain_dn, e.Message);

			}
			return member_list;
		}


		private bool AddAllGroupMembers(LdapConnection Con, string group_dn, HashSet<string> member_list)
		{
			LdapEntry Entry = null;
			var domain_dn = $"dc={_domain.Replace(".", ",dc=")}";
			int searchScope = LdapConnection.ScopeSub; //ScopeSub ScopeBase
			LdapSearchConstraints constraints = new LdapSearchConstraints();
			constraints.TimeLimit = 10000;

			String[] attrs = new String[] {
					"cn",
					"distinguishedName",
					"displayName",
					//"name",
					//"memberOf",
					"objectCategory",
					"member"
				};

			try
			{
				var filter = $"(&(objectCategory=*)({group_dn}))";
				
				var searchResults = Con.Search(
					domain_dn,          // object to read
					searchScope,        // scope - read single object
					filter,             // search filter
					attrs,              // return only required attributes
					false,              // return attrs and values
					constraints);       // time out value

				bool is_group = false ;
				while (searchResults.HasMore())
				{
					try
					{
						Entry = searchResults.Next();
					}
					catch
					{
						continue;
					}

					is_group = Entry.GetAttribute("objectCategory").StringValue.StartsWith("CN=Group,");

					if (is_group)
					{
						foreach (var dn in Entry.GetAttribute("member").StringValueArray)
						{
							var _dn = dn;
							var ind = dn.IndexOf(',');
							if (ind > -1)
							{
								_dn = _dn.Substring(0,ind);
							}

							if (!AddAllGroupMembers(Con, _dn, member_list))
							{
								member_list.Add(dn.ToLower());
							}
						}
					}
					return is_group; // we only expect ONE entry so return here
				}

			}
			catch (Exception e)
			{
				_logger.LogError("AddAllGroupMembers, group_dn={g}, ldap path={ldap}, err={err}", group_dn, domain_dn, e.Message);
			}
			return false; //not a group
		}


		/// <summary>
		/// This func returns all the groups the user belongs to - including inherited/recursive groups
		/// However, even though this make the most sense, I instead pull ALL the users of all specific groups I use.
		/// Then I cache those and can validate users instantly thereafter.
		/// </summary>
		/// <param name="Con"></param>
		/// <param name="user_dn"></param>
		/// <param name="group_list"></param>
		/// <returns></returns>
		private bool GetUsersGroups(LdapConnection Con, string user_dn, Dictionary<string, string> group_list)
		{
			LdapEntry Entry = null;
			var domain_dn = $"dc={_domain.Replace(".", ",dc=")}";
			int searchScope = LdapConnection.ScopeSub; //ScopeSub ScopeBase
			LdapSearchConstraints constraints = new LdapSearchConstraints();
			constraints.TimeLimit = 10000;

			String[] attrs = new String[] {
					"cn",
					"distinguishedName",
					"displayName",
					//"name",
					//"memberOf",
					//"member"
				};



			try
			{
				//var filter = $"(&(objectCategory=group)(cn={group_dn}))";

				var filter = $"(&(objectCategory=group)(member:1.2.840.113556.1.4.1941:={user_dn}))";

				var searchResults = Con.Search(
					domain_dn,          // object to read
					searchScope,        // scope - read single object
					filter,             // search filter
					attrs,              // return only required attributes
					false,              // return attrs and values
					constraints);       // time out value


				while (searchResults.HasMore())
				{
					try
					{
						Entry = searchResults.Next();
					}
					catch
					{
						continue;
					}

					group_list.Add(Entry.GetAttribute("cn").StringValue, Entry.Dn.ToLower());
				}
				return true;
			}
			catch 
			{
			}
			return false; //not a group
		}
	}
}
