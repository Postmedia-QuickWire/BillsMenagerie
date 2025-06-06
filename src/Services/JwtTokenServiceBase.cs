﻿using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using Common.Classes;

namespace Common.Services
{
    /// <summary>
    /// here it is, the one JWT class for all seasons
    /// 
    /// this file contains;
    /// - the token request and response models
    /// - the JWT settings model
    /// - the ITokenUser interface
    /// - the IBearerTokenService interface and implementation BASE class
    /// 
    /// JwtAppOptions can be added to your AppSettings.json model and passed from your BearerTokenServiceBase sub class
    /// throw ApiTokenException to prevent token creation or refresh 
    /// You must implement ITokenUser on your own user model
    /// and inherit BearerTokenServiceBase in your own BearerTokenService class
    ///		then override (minimally) both:
    ///		AuthenticateTokenUser(TokenRequest tok_req);
    ///		GetTokenUserForId(string id);
    ///		
    /// and finally, add as a scoped service in Startup.cs
    /// 
    /// services.AddScoped&lt;IBearerTokenService, BearerTokenService&gt;();
    /// 
    /// You can then use this in your own token endpoints
    /// 
    /// 
    /// </summary>

    public class JwtAppOptions
    {
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string ServerSecretKey { get; set; }
        public int TokenExpiryMinutes { get; set; }
        public int MaxAgeTokenRefreshDays { get; set; }
        public string Claim_ApiAccess { get; set; }
        public string Claim_TokenCreatedDate { get; set; }
        public string Claim_TokenRefreshedDate { get; set; }
        public string Claim_RefreshTokenHash { get; set; }
        public string HashSalt { get; set; }
        public bool VerboseLog { get; set; } = false; // log token and refreshes
    }

    public class TokenResponse
    {
        /// <summary>
        /// The actual JWT, set this to the Authorization http header for all subsequent requests
        /// 
        /// Authorization: Bearer [AccessToken]
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Token type, should always be 'Bearer'
        /// </summary>
        public string TokenType { get; set; }

        /// <summary>
        /// The refresh token to save for refreshing via api/token/refresh endpoint
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// The number of minutes this token will take to expire.
        /// After that you must refresh the token.
        /// </summary>
        public int Expires { get; set; }

        /// <summary>
        /// The date (UTC) that this token will NOT refresh anymore. 
        /// 
        /// If the token refresh never expires then this will be null or simply missing.
        /// </summary>
        public DateTime? RefreshExpireDate { get; set; }

        /// <summary>
        /// error message or 'OK' if ok...
        /// </summary>
        public string Msg { get; set; }
    }

    public class TokenRequest
    {
        /// <summary>
        /// Your client id - normally the email address you use to logon the site.
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// This is the generated api key you must store safely.
        /// </summary>
        public string ClientSecret { get; set; }
    }

    public class TokenRefreshRequest
    {
        /// <summary>
        /// The JWT access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// The refresh token that was sent via the new token endpoint or the last refresh.
        /// </summary>
        public string RefreshToken { get; set; }
    }


    public class ApiTokenException : Exception
    {
        public ApiTokenException(string cause, string user_msg = null) : base(cause)
        {
            if (user_msg == null)
                userMsg = cause;
            else
                userMsg = user_msg;
        }
        public string userMsg { get; private set; }
    }

    public interface IBearerTokenService
    {
        public Task<TokenResponse> NewToken(TokenRequest tok_req);
        public Task<TokenResponse> RefreshToken(TokenRefreshRequest tok_req);

        public void ClearUserCache();

    }

    public interface ITokenUser
    {
        public string TokenUserId { get; }
        public string TokenAccountId { get; }
        public string Name { get; }
        public HashSet<string> GetAllRoles();  //list of roles separated by commas
        public bool IsDisabled { get; }
        public DateTime? RevokeTokensOlderThan { get; } // can be null to NOT expire tokens manually for user
    }


    public abstract class BearerTokenServiceBase : IBearerTokenService
    {
        protected readonly ILogger _logger;
        protected readonly JwtAppOptions _jwtSettings;

        public BearerTokenServiceBase(JwtAppOptions settings, ILogger logger)
        {
            _jwtSettings = settings;
            _logger = logger;
        }

        public virtual void ClearUserCache() { }

        // must override to authenticate the user for a new token request
        protected abstract Task<ITokenUser> AuthenticateTokenUser(TokenRequest tok_req);

        // must override to fetch the user on a refresh token request
        protected abstract Task<ITokenUser> GetTokenUserForId(string id);

        // override to do extra checking before a NEW token is created
        // throw an ApiTokenException if you don't want to allow token creation
        protected virtual Task OnNewToken(ITokenUser user, TokenResponse resp)
        {
            return Task.CompletedTask;
        }

        // override to do extra checking before the token is refreshed
        // throw an ApiTokenException if you don't want to refresh the token
        protected virtual Task OnRefreshToken(ITokenUser user, TokenRefreshRequest tok_req, TokenResponse resp)
        {
            return Task.CompletedTask;
        }

        public async Task<TokenResponse> NewToken(TokenRequest tok_req)
        {
            //_logger.LogDebug("NewToken Request: clientId=[{clientId}], clientSecret=[{clientSecret}]", tok_req.clientId, tok_req.clientSecret);

            if (tok_req == null || String.IsNullOrEmpty(tok_req.ClientId) || String.IsNullOrEmpty(tok_req.ClientSecret))
                throw new ApiTokenException("invalid null token request");

            if (tok_req.ClientId.Length > 200 || tok_req.ClientSecret.Length > 200)
                throw new ApiTokenException("invalid token request prop lengths");

            if (tok_req.ClientId.Contains('<') || tok_req.ClientSecret.Contains('<'))
                throw new ApiTokenException("invalid token request contains <");

            if (_jwtSettings.VerboseLog)
                _logger.LogInformation("NewToken Request: clientId=[{clientId}]", tok_req.ClientId);

            if (string.IsNullOrWhiteSpace(tok_req.ClientId) || string.IsNullOrWhiteSpace(tok_req.ClientSecret))
                throw new ApiTokenException("invalid token request params");

            ITokenUser user = await AuthenticateTokenUser(tok_req);
            // validate all user props

            if (user == null)
                throw new ApiTokenException($"AuthenticateTokenUser return null '{tok_req.ClientId}'", "not authenticated");

            if (user.IsDisabled)
                throw new ApiTokenException($"api user account disabled '{tok_req.ClientId}'", "account disabled");

            if (String.IsNullOrWhiteSpace(user.TokenUserId))
                throw new ApiTokenException($"api user.AddrId is null or empty '{tok_req.ClientId}'", "auth error");

            var roles = user.GetAllRoles();

            if (roles.Count == 0)
                throw new ApiTokenException($"api user.Roles is null or empty '{tok_req.ClientId}'", "authorize error");


            // make the refresh token date
            var created_dt = DateTime.UtcNow;

            // Make the bearer token
            // we add the creation date (created by clientId NOT by refresh - we add this back in during refresh)
            // sept 2022 bug, I was creating the token using the PASSED request name, this caused the refresh token to recreate using this name 
            string access_token = MakeToken(user.Name, roles.ToArray())
                                    .AddClaim(_jwtSettings.Claim_TokenCreatedDate, created_dt.Ticks.ToString())
                                    .AddClaim(ClaimTypes.NameIdentifier, user.TokenUserId)
                                    .AddClaim(ClaimTypes.UserData, user.TokenAccountId)
                                    .Build().Value;


            TokenResponse resp = new TokenResponse();

            resp.AccessToken = access_token;
            resp.RefreshToken = MakeRefreshToken(access_token, created_dt);
            resp.RefreshExpireDate = _jwtSettings.MaxAgeTokenRefreshDays > 0 ? created_dt.AddDays(_jwtSettings.MaxAgeTokenRefreshDays) : null;
            resp.Expires = _jwtSettings.TokenExpiryMinutes;
            resp.TokenType = "Bearer";
            resp.Msg = "ok";


            await OnNewToken(user, resp);

            if (_jwtSettings.VerboseLog)
            {
                _logger.LogInformation("Created new token for '{name}/{accid}:{clientId}', roles: {roles}, tok uid: {tokid}"
                , user.Name, user.TokenAccountId, user.TokenUserId, String.Join(", ", user.GetAllRoles()), tok_req.ClientId);
            }

            return resp;
        }

        //TODO we need to change this so client_id isn;t actually required when creating a new token
        public async Task<TokenResponse> RefreshToken(TokenRefreshRequest tok_req)
        {
            string clientId = "-";
            if (string.IsNullOrWhiteSpace(tok_req.AccessToken) || string.IsNullOrWhiteSpace(tok_req.RefreshToken))
                throw new ApiTokenException("invalid token refresh params", "bad params");

            // decrypt the (possibly) expired token
            ClaimsPrincipal tuser = GetPrincipalFromExpiredToken(tok_req.AccessToken);

            // get the token created date from the jwt
            string token_created_date = tuser.Claims.Where(c => c.Type == _jwtSettings.Claim_TokenCreatedDate)
                .Select(c => c.Value).SingleOrDefault();

            DateTime tokenCreatedDate = new DateTime(long.Parse(token_created_date), DateTimeKind.Utc); // MUST specify UTC kind!

            if (_jwtSettings.MaxAgeTokenRefreshDays > 0 && (DateTime.UtcNow - tokenCreatedDate).Days >= _jwtSettings.MaxAgeTokenRefreshDays)
                throw new ApiTokenException("token is too old to refresh");

            clientId = tuser.Identity.Name;
            string accountId_str = tuser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (String.IsNullOrEmpty(accountId_str))
                throw new ApiTokenException($"token missing user id for '{clientId}'", "refresh error");

            // format may be account_id:user_id
            int userId;
            if (accountId_str.Contains(':'))
            {
                var ids = accountId_str.Split(':');
                if (ids.Length != 2)
                    throw new ApiTokenException($"bad id:id string, {accountId_str}", "id refresh error");

                userId = Convert.ToInt32(ids[1]);
            }
            else
            {
                userId = Convert.ToInt32(accountId_str);
            }

            _logger.LogDebug(">> refresh user identity: {u}", clientId);

            ITokenUser user = await GetTokenUserForId(clientId); // will throw

            if (user == null)
                throw new ApiTokenException($"GetTokenUserForId returned null '{clientId}'", "user not found");

            if (user.IsDisabled)
                throw new ApiTokenException($"api user account disabled '{clientId}'", "account disabled");

            if (user.RevokeTokensOlderThan != null && tokenCreatedDate < user.RevokeTokensOlderThan)
                throw new ApiTokenException($"token revoked by [revokeTokensOlderThan] date token:{tokenCreatedDate.ToString()}, account:{user.RevokeTokensOlderThan.ToString()}"
                                            , "token revoked by date");

            HashSet<string> roles = user.GetAllRoles();
            if (roles.Count ==0)
                throw new ApiTokenException($"api user.Roles is null or empty '{clientId}'", "authorize error");


            // now for the actual refresh token auth check
            // NEW the refresh token is now an actual hash of the TOKEN itself! (along with token created date and jwt hash salt)
            if (tok_req.RefreshToken != MakeRefreshToken(tok_req.AccessToken, tokenCreatedDate))
                throw new ApiTokenException("invalid refresh token (doesn't match bearer token)", "bad token pair");

            // Make the bearer token
            // we add the creation date (created by clientId NOT by refresh - we add this back in during refresh)
            string access_token = MakeToken(clientId, roles.ToArray())
                                    .AddClaim(_jwtSettings.Claim_TokenCreatedDate, token_created_date) // re-add the token created date
                                    .AddClaim(ClaimTypes.NameIdentifier, user.TokenUserId)
                                    .AddClaim(ClaimTypes.UserData, user.TokenAccountId)
                                    .Build().Value;

            TokenResponse resp = new TokenResponse();

            resp.RefreshExpireDate = _jwtSettings.MaxAgeTokenRefreshDays > 0 ? tokenCreatedDate.AddDays(_jwtSettings.MaxAgeTokenRefreshDays) : null;
            resp.AccessToken = access_token;
            resp.RefreshToken = MakeRefreshToken(access_token, tokenCreatedDate);
            resp.Expires = _jwtSettings.TokenExpiryMinutes;
            resp.TokenType = "Bearer";
            resp.Msg = "ok";

            // allow sub class to prevent token refresh (by throwing an ApiTokenException)
            // or simply do something after the token is refreshed
            await OnRefreshToken(user, tok_req, resp); // just a quick event for sub class

            //_logger.LogInformation("Refreshed token for '{clientId}'", clientId);

            // may be too noisy...
            if (_jwtSettings.VerboseLog)
            {
                _logger.LogInformation("Refreshed token for '{name}/{accid}:{clientId}', roles: {roles}, [tok uid: {tokid}]"
                , user.Name, user.TokenAccountId, user.TokenUserId, String.Join(", ", user.GetAllRoles()), clientId);
            }


            return resp;

        }


        protected virtual JwtTokenBuilder MakeToken(string username, string[] roles, string refresh_token = null)
        {
            JwtTokenBuilder token = new JwtTokenBuilder()
                    .AddSecurityKey(JwtSecurityKey.Create(_jwtSettings.ServerSecretKey))
                    //.AddSubject(HttpContext.User.Identity.Name)
                    .AddIssuer(_jwtSettings.Issuer)
                    .AddAudience(_jwtSettings.Audience)
                    .AddApiAccess(roles)
                    //.AddClaim(ClaimTypes.Role, role)
                    .AddClaim(ClaimTypes.Name, username)
                    .AddClaim(_jwtSettings.Claim_TokenRefreshedDate, DateTime.UtcNow.Ticks.ToString())
                    //.AddClaim(_jwtSettings.Claim_RefreshTokenHash, MakeHash(refresh_token))
                    .AddExpiry(_jwtSettings.TokenExpiryMinutes);

            // I DON'T add the refresh token hash anymore, that's stupid, just hash the access_token itself
            if (!String.IsNullOrWhiteSpace(refresh_token))
                token.AddClaim(_jwtSettings.Claim_RefreshTokenHash, MakeHash(refresh_token));

            return token;
        }

        protected virtual ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true, //you might want to validate the audience and issuer depending on your use case
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidAudience = _jwtSettings.Audience,
                ValidIssuer = _jwtSettings.Issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.ServerSecretKey)),
                ValidateLifetime = false //here we are saying that we don't care about the token's expiration date
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken securityToken;
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
            var jwtSecurityToken = securityToken as JwtSecurityToken;
            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new ApiTokenException("unable to validate an expired token (via hash, that's bad)", "Invalid token");

            return principal;
        }

        protected virtual string MakeHash(string val)
        {
            return BFunct.GetHashString(val, _jwtSettings.HashSalt);
        }
        protected virtual string MakeRefreshToken(string token, DateTime tokenCreationDate)
        {
            return BFunct.GetHashString(token, $"{tokenCreationDate.ToString("o")}-{_jwtSettings.HashSalt}");
        }
    }
}
