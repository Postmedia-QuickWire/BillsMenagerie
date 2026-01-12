using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using WebSite.Models;

namespace Common.Models
{
	public class WebMailTemplateModel
	{
		public string Template { get; set; }

		[Required]
		public string Subject { get; set; }

		[Required]
		public string HtmlBody { get; set; }
		public bool SendToNormalUpdates { get; set; }
		public bool SendToNewsLetter { get; set; }
		public bool SendToDevelopers { get; set; }
		public bool SendToBilling { get; set; }
		public bool SendToProcurement { get; set; }
        public bool SendToTechs { get; set; }
        public bool ExcludeManagedAccounts { get; set; }
        public bool SendToAll { get; set; }
		public bool IncludeNullPasswords { get; set; }

        // use multiple attrib in file input for multiple file upload
        public IList<IFormFile> Attachments { get; set; }
    }

	public class WebMailModel : WebMailTemplateModel
	{
		public string TestEmail { get; set; }
        public bool DryRun { get; set; } = false;


        public Dictionary<string, string> SubstiVars { get; set; }

		public Dictionary<string, string> EmailList { get; set; }

		public IEnumerable<SelectListItem> Templates { get; set; }

        public List<MonthlyMailLog> MailLog {  get; set; } 
	}

    public class MonthlyMailLog
    {
        [Key]
        public int Id { get; set; }
        public string Subject { get; set; }
        public int EmailsSent { get; set; }
        public string UserName { get; set; }

        public string SendToFlags { get; set; }

        [DataType(DataType.Date)]
        public DateTime SendDate { get; set; } = DateTime.UtcNow;
    }

    public class WebMailSendToAddress
	{
		public string Email { get; set; }
		public string AccountName { get; set; }

	}
	public class WebMailModelComplete
	{
		public List<WebMailSendToAddress> EmailAddresses { get; set; }
		public string ErrMsg{ get; set; }
	}


}
