using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Common.Controllers
{
	[Route("healthz")]
	[ApiExplorerSettings(IgnoreApi = true)]
	public class CheckController : Controller
	{

		private readonly ILogger<CheckController> _logger;
		public CheckController(ILogger<CheckController> logger)
		{
			_logger = logger;
		}

		[HttpGet]
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public ActionResult<string> Index()
{
			//_logger.LogInformation("Health check"); 
			return "ok";
		}

	}
}
