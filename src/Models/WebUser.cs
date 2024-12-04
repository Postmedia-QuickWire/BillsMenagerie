using Common.Classes;
using Common.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Common.Models
{

	/// <summary>
	/// simple user model with parent account
	/// </summary>
	public class WebUser : ITokenUser
	{
		public enum UserRole { NoAccess, ROUser, User, UserAdmin, Support, SiteAdmin };

		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[ForeignKey("WebAccount")]
		[Display(Name = "Account Name")]
		public int AccountId { get; set; }

		[MaxLength(100)]
		[Required]
		[Display(Name = "Full Name")]
		public string FullName { get; set; }

		[MaxLength(50)]
		[Display(Name = "Job Title")]
		public string Title { get; set; }

		[MaxLength(100)]
		[EmailAddress]
		[Required]
		public string Email { get; set; }

		[MaxLength(50)]
		public string Phone { get; set; }

		[MaxLength(50)]
		public string Cell { get; set; }

		[MaxLength(1000)]
		public string Roles { get; set; }

		// use this to handle setting Roles
		[NotMapped]
		[Required]
		[Display(Name = "User Role")]
		public UserRole TRole { get; set; }

		[MaxLength(1000)]
		public string Comment { get; set; }

		[MaxLength(100)]
		public string UserPasswordHash { get; set; }

		[MaxLength(50)]
		public string AccountCreatedBy { get; set; }

		[MaxLength(100)]
		public string ChangePwToken { get; set; }

		public bool IsDisabled { get; set; }

		[Display(Name = "Developer Account")]
		public bool IsDeveloper { get; set; }

		[Display(Name = "Tech Contact", Description = "Will recieve incident emails from support")]
		public bool IsTechContact { get; set; }

		[Display(Name = "Billing Contact")]
		public bool IsBillingContact { get; set; } = false;

		[Display(Name = "Email News Letter")]
		public bool MailNewsLetter { get; set; }

		[Display(Name = "Email Update Notices")]
		public bool MailUpdateNotice { get; set; }


		[Display(Name = "Procurement Contact")]
		public bool IsProcurementContact { get; set; } = false;

		[Display(Name = "User Account Lock")]
		public bool IsUserLocked { get; set; }
		

		[Display(Name = "Created Date")]
		[DataType(DataType.Date)]
		[DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy}")]
		public DateTime? CreatedDate { get; set; }

		[Display(Name = "Last Logon")]
		[DataType(DataType.DateTime)]
		[DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy hh:mm}")]
		public DateTime? LastLogon { get; set; }

		[Display(Name = "Last Modified")]
		[DataType(DataType.DateTime)]
		[DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy hh:mm}")]
		public DateTime? LastModified { get; set; }

		[MaxLength(50)]
		[Display(Name = "Last Modified By")]
		public string LastModifiedBy { get; set; }

		// api token management, not really used by QuickWire
		[MaxLength(100)]
		public string ApiKeySecretHash { get; set; }

		[MaxLength(100)]
		public string ApiKey { get; set; }
		public DateTime? TokenCreated { get; set; }
		public DateTime? LastTokenRefreshed { get; set; }
		public DateTime? RevokeTokensOlderThan { get; set; }

		public string LastBatchConfig { get; set; }

		// we now expect accout_id:user_id for the token user identifier
		public string TokenUserId { get => Id.ToString(); }
		public string TokenAccountId { get => AccountId.ToString(); }

		public string Name { get => Email; }

		[JsonIgnore]
		[Display(Name = "Account")]
		public WebAccount WebAccount { get; set; }

		[NotMapped]
		[Display(Name = "Incident Support User Role")]
		public bool isSupportUser { get; set; }


		// used for runtime usage display
		[NotMapped]
		public int Usage { get; set; }


		// returns webAccount api roles as well
		// note that webAccount MUST be valid/included else we (should) throw
		public List<string> GetAllRoles()
		{
			var roles = GetRoles();
			if (WebAccount != null && !String.IsNullOrEmpty(WebAccount.Roles))
				roles.AddRange(WebAccount.Roles.Split(new char[] { ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
			if (WebAccount.IsIntegrator)
				roles.Add("integrator");

			return roles;
		}

		public List<string> GetRoles()
		{
			if (Roles != null)
				return Roles.Split(new char[] { ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
			return new List<string>();
		}

		public bool IsInRole(UserRole role)
		{
			var srole = role.ToString();
			foreach (var r in GetRoles())
			{
				if (srole.Equals(r, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}
		public void AddRole(UserRole role)
		{
			if (!IsInRole(role))
			{
				if (String.IsNullOrWhiteSpace(Roles))
					Roles = "";
				else
					Roles += ",";
				Roles += role.ToString();
			}
		}

		public override string ToString()
		{
			return $"[{FullName}/{WebAccount?.Name} ({Id}/{WebAccount?.Id})]";
		}
	}
	public class WebAccount
	{
		public enum ApiRole { NoAccess, ca, us, batch };
		public enum DL { dl_anonymous, dl_windows, dl_linux, dl_server, dl_std_batch, dl_poc_batch, dl_std_legacy, dl_poc_legacy };

		[Key]
		public int Id { get; set; }

		[MaxLength(200)]
		public string Roles { get; set; }

		[MaxLength(1000)]
		public string DownloadPerms { get; set; }

		[MaxLength(100)]
		public string DownloadKeyHash { get; set; }

		[Display(Name = "Managed by Integrator")]
		public int? IntegratorAccountId { get; set; }

		[MaxLength(100)]
		[Display(Name = "Account Name")]
		[Required]
		public string Name { get; set; }

		[MaxLength(100)]
		[Display(Name = "Company Email")]
		[EmailAddress]
		public string Email { get; set; }

		[MaxLength(200)]
		public string Address { get; set; }

		[MaxLength(200)]
		public string Address2 { get; set; }

		[MaxLength(30)]
		public string ZipPostal { get; set; }

		[MaxLength(50)]
		public string City { get; set; }

		[MaxLength(50)]
		public string StateProv { get; set; }

		[MaxLength(50)]
		public string Country { get; set; }

		[MaxLength(100)]
		public string WebSite { get; set; }

		[Display(Name = "Disabled")]
		public bool IsDisabled { get; set; }

		[Display(Name = "Integrator")]
		public bool IsIntegrator { get; set; }

		[Display(Name = "Exempt from Support Expiry")]
		public bool NoSupportExpiry { get; set; }

		[Display(Name = "Created Date")]
		[DataType(DataType.Date)]
		[DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy}")]
		public DateTime? CreatedDate { get; set; }

		[Display(Name = "Support Date")]
		[DataType(DataType.Date)]
		[DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-dd}")]
		public DateTime? SupportToDate { get; set; }

		[NotMapped]
		[Display(Name = "Windows App Access")]
		public bool dl_windows { get; set; }

		[NotMapped]
		[Display(Name = "Linux App Access")]
		public bool dl_linux { get; set; }


		[NotMapped]
		[Display(Name = "Server (implies STD DB download)")]
		public bool dl_server { get; set; }

		[NotMapped]
		[Display(Name = "STD Batch")]
		public bool dl_std_batch { get; set; }

		[NotMapped]
		[Display(Name = "POC Batch")]
		public bool dl_poc_batch { get; set; }

		[NotMapped]
		[Display(Name = "STD Legacy")]
		public bool dl_std_legacy { get; set; }

		[NotMapped]
		[Display(Name = "POC Legacy")]
		public bool dl_poc_legacy { get; set; }


		[NotMapped]
		[Display(Name = "CA API Access")]
		public bool caApiAccess { get; set; }

		[NotMapped]
		[Display(Name = "US API Access")]
		public bool usApiAccess { get; set; }

		[NotMapped]
		[Display(Name = "Batch API Access")]
		public bool batchApiAccess { get; set; }

		// used for runtime usage display
		[NotMapped]
		public int Usage { get; set; }

		[NotMapped]
		public bool HasProcurementUser { get; set; }

		[NotMapped]
		public bool HasBillingUser { get; set; }


		[NotMapped]
		[MaxLength(100)]
		[Display(Name = "Integrator")]
		public string IntegratorName { get; set; }
		public List<string> GetRoles()
		{
			if (Roles != null)
				return Roles.Split(new char[] { ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
			return new List<string>();
		}

		public bool IsInRole(ApiRole role)
		{
			var srole = role.ToString();
			foreach (var r in GetRoles())
			{
				if (srole.Equals(r, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}
		public void AddRole(ApiRole role)
		{
			if (!IsInRole(role))
			{
				if (String.IsNullOrWhiteSpace(Roles))
					Roles = "";
				else
					Roles += ",";
				Roles += role.ToString();
			}
		}

		public bool IsSupported()
		{
			if (NoSupportExpiry || SupportToDate == null)
				return true;
			return SupportToDate >= DateTime.UtcNow;
		}

		public bool CanDownload(IEnumerable<DL> dl_privs)
		{
			if (dl_privs == null)
			{
				return true; //null access means anyone can download!
			}
			else if (IsSupported())
			{
				foreach (var dl in dl_privs)
				{
					if (CanDownload(dl))
						return true;
				}
			}

			return false;
		}

		public bool CanDownload(DL dl_priv)
		{
			return dl_priv == DL.dl_anonymous || (DownloadPerms?.Contains(dl_priv.ToString(), StringComparison.Ordinal) ?? false);
		}

		public void AddDownload(DL dl_priv)
		{
			if (!CanDownload(dl_priv))
			{
				if (DownloadPerms == null)
					DownloadPerms = "";
				DownloadPerms += $" {dl_priv}";
			}
		}

		[JsonIgnore]
		public List<WebUser> WebUsers { get; set; }

	}

	public class NewWebUserEmailTemplateModel
	{
		public WebUser User { get; set; }
		public string PasswordSetLink { get; set; }

		public DateTime LinkExpiresDate { get; set; }
		public TimeSpan LinkExpiresTimeSpan { get; set; }
	}


	public class AccountViewModel
	{
		public int Id { get; set; }

		[MaxLength(200)]
		public string Roles { get; set; }

		[Display(Name = "Managed by Integrator")]
		public int? IntegratorAccountId { get; set; }

		[MaxLength(100)]
		[Display(Name = "Integrator")]
		public string IntegratorName { get; set; }

		[MaxLength(100)]
		[Display(Name = "Company Name")]
		public string Name { get; set; }

		[Display(Name = "Company Email")]
		[EmailAddress]
		public string Email { get; set; }

		[Display(Name = "Disabled")]
		public bool IsDisabled { get; set; }

		[Display(Name = "Integrator")]
		public bool IsIntegrator { get; set; }

		[Display(Name = "Exempt from Support Expiry")]
		public bool NoSupportExpiry { get; set; }

		[Display(Name = "Support Date")]
		[DataType(DataType.Date)]
		[DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-dd}")]
		public DateTime? SupportToDate { get; set; }

	}

}
