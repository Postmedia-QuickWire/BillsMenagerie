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
using static Common.Models.WebAccount;

namespace Common.Models
{

	/// <summary>
	/// simple user model with parent account
	/// </summary>
	public class WebUserBase 
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

		public string Name { get => Email; }

		[JsonIgnore]
		[Display(Name = "Account")]
		public WebAccount WebAccount { get; set; }


        /// <summary>
        /// this is used for edit form setting the UserRole.Support in Roles
        /// </summary>
		[NotMapped]
		[Display(Name = "Incident Support User Role")]
		public bool isSupportUser { get; set; }


		// returns webAccount api roles as well
		// note that webAccount MUST be valid/included else we (should) throw
		public HashSet<string> GetAllRoles()
		{
			var roles = GetRoles();
            if (WebAccount != null && !String.IsNullOrEmpty(WebAccount.Roles))
            {
                roles.UnionWith(WebAccount.GetRoles());
            }
            if (WebAccount.IsIntegrator)
				roles.Add("integrator");

			return roles;
		}

        // get this users roles only - doesn't include any inherited web account roles - see above
        public HashSet<string> GetRoles()
        {
            if (_roles == null)
            {
                if (Roles != null)
                    _roles = Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                else
                    _roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            return _roles;
        }

        private HashSet<string> _roles;

        public bool IsInRole(UserRole role)
		{
            return GetRoles().Contains(role.ToString());
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
                _roles = null;
			}
		}

		public override string ToString()
		{
			return $"[{FullName}/{WebAccount?.Name} ({Id}/{WebAccount?.Id})]";
		}
	}
	public class WebAccountBase
	{
		[Key]
		public int Id { get; set; }

		[MaxLength(200)]
		public string Roles { get; set; }

		[MaxLength(1000)]
		public string DownloadPerms { get; set; }

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
		public bool HasProcurementUser { get; set; }

		[NotMapped]
		public bool HasBillingUser { get; set; }


		[NotMapped]
		[MaxLength(100)]
		[Display(Name = "Integrator")]
		public string IntegratorName { get; set; }
		public HashSet<string> GetRoles()
		{
            if (_roles == null)
            {
                if (Roles != null)
                    _roles = Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                else
                    _roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
			return _roles;
		}

        [NotMapped]
        private HashSet<string> _roles;

        [NotMapped]
        private HashSet<string> _downloadPerms;

        public bool IsInRole(string role)
		{
            return GetRoles().Contains(role);
		}

		public void AddRole(string role)
		{
			if (!IsInRole(role))
			{
				if (String.IsNullOrWhiteSpace(Roles))
					Roles = "";
				else
					Roles += ",";
				Roles += role;
                _roles = null;
            }
		}

		public bool IsSupported()
		{
			if (NoSupportExpiry || SupportToDate == null)
				return true;
			return SupportToDate >= DateTime.UtcNow;
		}


        public bool CanDownload(HashSet<string> dl_privs)
        {
            if (dl_privs == null || dl_privs.Count == 0)
            {
                return true; //null access means anyone can download!
            }
            else if (IsSupported())
            {
                return GetDownloadPerms().Overlaps(dl_privs);
            }

            return false;
        }

        public bool CanDownload(string dl_priv)
        {
            return GetDownloadPerms().Contains(dl_priv);
        }

        public HashSet<string> GetDownloadPerms()
        {
            if (_downloadPerms == null)
            {
                if (String.IsNullOrEmpty(DownloadPerms))
                {
                    _downloadPerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    _downloadPerms = DownloadPerms.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }
            return _downloadPerms;
        }


        public void AddDownloadPerm(string dl_priv)
        {
            if (!CanDownload(dl_priv))
            {
                if (DownloadPerms == null)
                    DownloadPerms = "";
                DownloadPerms += $" {dl_priv}"; // note a space is used...
                _downloadPerms = null;
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
