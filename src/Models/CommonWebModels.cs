using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Common.Models
{

	public class ContactForm
	{
		[Required]
		[MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(100)]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [MaxLength(100)]
        public string CompanyName { get; set; }

        [MaxLength(100)]
        [DataType(DataType.PhoneNumber)]
		public string Phone { get; set; }

		[Display(Name = "Contact me by Phone")]
		public Boolean ContactByPhone { get; set; }
        
		[Required]
        [MaxLength(4096)]
        public string Comment { get; set; }
	
		// this is the captcha key encoded
		[MaxLength(200)]
		public string CaptchaEnc { get; set; }

		//[Required]
		[MaxLength(4), MinLength(4)]
		[Display(Name = "Image Text")]
		public string CaptchaText { get; set; }

		public string reCaptchaKey { get; set; }

		[FromForm(Name = "g-recaptcha-response")]
		public string reCaptchaEnterpriseResponse { get; set; }

		// more captcha homegrown stuff
		public string token1 { get; set; }
		public string token2 { get; set; }
		public string token3 { get; set; }
	}


	public class TimedSubmitButtonModel
	{
        public string token1 { get; set; }
        public string token2 { get; set; }
        public string token3 { get; set; }
    }


	public class UserLogin
	{
		[Required(ErrorMessage = "The UserName field is required.")]
		[MaxLength(100)]
		public string UserName { get; set; }

		[Required(ErrorMessage = "The Password field is required.")]
		[DataType(DataType.Password)]
		public string Password { get; set; }

        public string ReturnUrl { get; set; }


		[FromForm(Name = "g-recaptcha-response")]
		public string reCaptchaEnterpriseResponse { get; set; }
	}
    public class PasswordChangeRequest
	{
		[Required]
		[DataType(DataType.EmailAddress)]
		public string EmailAddr { get; set; }

		//[Required]
		//public string Capcha { get; set; }
		//public string CapchaEnc { get; set; }

		[FromForm(Name = "g-recaptcha-response")]
		public string reCaptchaEnterpriseResponse { get; set; }
	}

	public class PasswordChange
	{
		[Required]
		public string RequestEnc { get; set; }

		[Required]
		[DataType(DataType.Password)]
		[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "The {0} does not meet requirements.")]
		public string Password { get; set; }

		[Required]
		[DataType(DataType.Password)]
		[Compare("Password", ErrorMessage = "Passwords do not match")]
		public string Password2 { get; set; }
	}

	// mail settings
	public class SMTP
	{
		public string Server { get; set; }
		public int Port { get; set; }
		public string SenderName { get; set; }
		public string SenderEmail { get; set; }
		public string Account { get; set; }
		public string Password { get; set; }
		public bool SSL { get; set; }
	}


    // store the machine keys in the DB
    public class MyKeysContext : DbContext, IDataProtectionKeyContext
	{
		// A recommended constructor overload when using EF Core 
		// with dependency injection.
		public MyKeysContext(DbContextOptions<MyKeysContext> options)
			: base(options) { }

		
		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if (!optionsBuilder.IsConfigured)
			{
				//optionsBuilder.UseSqlServer("Server=localhost;Database=GenericWebSite;Trusted_Connection=True;Pooling=True;TrustServerCertificate=True;");
				optionsBuilder.UseSqlServer("Host=localhost;Port=5432;Database=QuickWireWebSite;Include Error Detail=true;Username=postgres;");
			}
		}
		
		// This maps to the table that stores keys.
		public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
	}
}
