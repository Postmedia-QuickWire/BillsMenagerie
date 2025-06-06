﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Common.Classes;
using Common.Services;

namespace Common.Controllers
{

	/// <summary>
	/// yet another JWT based class, this is an enpoint for token creation and refresh
	/// it uses my IBearerTokenService that you must implement from a base class and add
	/// as a Scoped service
	/// I think we can create a new controller based on this to override any class attributes
	/// </summary>

	[ApiExplorerSettings(IgnoreApi = true)]
	public class TestApiRequest
	{
		public string TestStringIn { get; set; }
	}

	[ApiExplorerSettings(IgnoreApi = true)]
	public class TestApiResponse
	{
		public string TestStringOut { get; set; }
	}

	[ApiController]
	[ApiExplorerSettings(GroupName = "Token")]
	[Route("api/[controller]")]
	[Produces("application/json", "text/json", "application/xml", "text/xml")]
	[Consumes("application/json", "text/json", "application/xml", "text/xml")]
	public class TokenController : ControllerBase
    {
		private ILogger<TokenController> _logger;
        private readonly IBearerTokenService _tokenService;
        private readonly bool _logTokenCreate = false;

        public TokenController(IConfiguration config, IBearerTokenService tokenService, ILogger<TokenController> logger)
		{
			_logger = logger;
			_tokenService = tokenService;
            _logTokenCreate = config["AppSettings:LogTokenCreation"] == "True";
        }


		// POST: /Token/
		/// <summary>
		/// 
		/// Request a new token using your API key and secret.
		/// 
		/// </summary>
		/// <param name="tok_req"></param>
		/// <returns></returns>
		[HttpPost]
		public async Task<ActionResult<TokenResponse>> NewToken(TokenRequest tok_req)
        {
			TokenResponse resp = new TokenResponse();
			if (ModelState.IsValid)
			{
				try
				{
					resp = await _tokenService.NewToken(tok_req);
                    if (_logTokenCreate)
                    {
                        _logger.LogInformation(">> creating token for '{clientId}', ip={ip}", tok_req.ClientId, Request.HttpContext.Connection.RemoteIpAddress);
                    }
                }
                catch (ApiTokenException ex)
				{
					resp.Msg = ex.userMsg;
					Response.StatusCode = StatusCodes.Status401Unauthorized;
					_logger.LogError("Error creating token for '{clientId}', err={Message}, ip={ip}", tok_req.ClientId, ex.Message, Request.HttpContext.Connection.RemoteIpAddress);
				}
				catch (Exception ex)
				{
					if (string.IsNullOrWhiteSpace(resp.Msg))
						resp.Msg = "unable to create token";

					Response.StatusCode = StatusCodes.Status401Unauthorized;
					_logger.LogError("Error creating token for '{clientId}', err={Message}, ip={ip}", tok_req.ClientId, ex.Message, Request.HttpContext.Connection.RemoteIpAddress);
				}

			}

			return resp;
        }


         // POST: api/Token
		 /// <summary>
		 /// 
		 /// Refresh an expired token.
		 /// 
		 /// </summary>
		 /// <param name="tok_req"></param>
		 /// <returns></returns>
        [HttpPost]
		[Route("refresh")]
        public async Task<ActionResult<TokenResponse>> RefreshToken(TokenRefreshRequest tok_req)
        {
			TokenResponse resp = new TokenResponse();
			string clientId = "-";
			if (ModelState.IsValid)
			{
				try
				{
					resp = await _tokenService.RefreshToken(tok_req);
                }
                catch (ApiTokenException ex)
				{
					resp.Msg = ex.userMsg;
					Response.StatusCode = StatusCodes.Status401Unauthorized;
					_logger.LogError("Error refreshing token for '{clientId}', api_err={Message}, ip={ip}", clientId, ex.Message, Request.HttpContext.Connection.RemoteIpAddress);
				}
				catch (Exception ex)
				{
					if (string.IsNullOrWhiteSpace(resp.Msg))
						resp.Msg = "unable to refresh token";

					Response.StatusCode = StatusCodes.Status401Unauthorized;
					_logger.LogError("Error refreshing token for '{clientId}', err={Message}, ip={ip}", clientId, ex.Message, Request.HttpContext.Connection.RemoteIpAddress);
				}

			}

			return resp;
		}


		[HttpPost("test")]
		[ApiExplorerSettings(IgnoreApi =true)]
		public ActionResult<TestApiResponse> no_auth_test([FromBody] TestApiRequest req)
		{
			try
			{
				return new TestApiResponse() { TestStringOut = req.TestStringIn };
			}
			catch (Exception ex)
			{
				//EndpointException(ex, req);
				_logger.LogError("no_auth_test error, {m}, ip={ip}", ex.Message, Request.HttpContext.Connection.RemoteIpAddress);
				return StatusCode(502, new { err = ex.Message });
			}
		}

	}
}

