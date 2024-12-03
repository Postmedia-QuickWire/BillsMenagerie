using Microsoft.AspNetCore.Mvc;

namespace Common.Controllers
{
    public class SwaggerUIController : Controller
    {
        /// <summary>
        /// I use a swagger dark theme from https://github.com/ravisankarchinnam/openapi-swagger-dark-theme
        /// I inject the path to this controller /SwaggerUI/css/[anything]
        /// I ignore [anything] as I've only registered one file with swagger at this location
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IActionResult css(string id)
        {
            Response.ContentType = "text/css";

            ViewBag.Theme = "light";
            if (Request.Cookies.TryGetValue("theme", out string theme))
            {
                ViewBag.Theme = theme;
            }
            return View();
        }

    }
}
