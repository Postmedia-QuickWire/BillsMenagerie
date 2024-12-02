using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
		public bool SendToAll { get; set; }
		public bool IncludeNullPasswords { get; set; }
	}

	public class WebMailModel : WebMailTemplateModel
	{
		public string TestEmail { get; set; }

		public Dictionary<string, string> SubstiVars { get; set; }

		public Dictionary<string, string> EmailList { get; set; }

		public IEnumerable<SelectListItem> Templates { get; set; }
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
