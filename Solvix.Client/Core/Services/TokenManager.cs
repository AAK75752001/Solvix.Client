using Microsoft.Extensions.Logging;
using Solvix.Client.Core.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace Solvix.Client.Core.Services
{
    public class TokenManager : ITokenManager
    {
        private readonly ISecureStorageService _secureStorageService;
        private readonly ILogger<TokenManager> _logger;

        private Dictionary<string, string>? _cachedClaims;
        private DateTime _cacheExpiryTime = DateTime.MinValue;

        public TokenManager(ISecureStorageService secureStorageService, ILogger<TokenManager> logger)
        {
            _secureStorageService = secureStorageService;
            _logger = logger;
        }

        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _secureStorageService.GetAsync(Constants.StorageKeys.AuthToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving token");
                return null;
            }
        }

        public async Task SaveTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Attempted to save empty token");
                return;
            }

            try
            {
                if (!IsValidJwt(token))
                {
                    _logger.LogWarning("Attempted to save invalid JWT token");
                    return;
                }

                await _secureStorageService.SaveAsync(Constants.StorageKeys.AuthToken, token);

                _cachedClaims = null;
                _cacheExpiryTime = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving token");
            }
        }

        public async Task RemoveTokenAsync()
        {
            try
            {
                await _secureStorageService.RemoveAsync(Constants.StorageKeys.AuthToken);

                _cachedClaims = null;
                _cacheExpiryTime = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing token");
            }
        }

        public async Task<bool> IsTokenValidAsync()
        {
            string? token = await GetTokenAsync();

            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                    return false;

                var jwtToken = handler.ReadJwtToken(token);
                var expiry = jwtToken.ValidTo;

                return expiry > DateTime.UtcNow.AddSeconds(30);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        public async Task<Dictionary<string, string>?> GetTokenClaimsAsync()
        {
            if (_cachedClaims != null && _cacheExpiryTime > DateTime.UtcNow)
            {
                return _cachedClaims;
            }

            string? token = await GetTokenAsync();

            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                    return null;

                var jwtToken = handler.ReadJwtToken(token);

                var claims = new Dictionary<string, string>();

                foreach (var claim in jwtToken.Claims)
                {
                    claims[claim.Type] = claim.Value;
                }

                _cachedClaims = claims;
                _cacheExpiryTime = jwtToken.ValidTo.AddMinutes(-1);

                return claims;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting claims from token");
                return null;
            }
        }

        public async Task<long> GetUserIdFromTokenAsync()
        {
            var claims = await GetTokenClaimsAsync();

            if (claims == null)
                return 0;

            string[] possibleClaimTypes = { "nameid", "sub", "id", "userid", "user_id", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" };

            foreach (var claimType in possibleClaimTypes)
            {
                if (claims.TryGetValue(claimType, out string? value) && !string.IsNullOrEmpty(value))
                {
                    if (long.TryParse(value, out long userId))
                    {
                        return userId;
                    }
                }
            }

            return 0;
        }

        public async Task<string> GetUsernameFromTokenAsync()
        {
            var claims = await GetTokenClaimsAsync();

            if (claims == null)
                return string.Empty;

            string[] possibleClaimTypes = { "unique_name", "name", "username", "email", "preferred_username", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" };

            foreach (var claimType in possibleClaimTypes)
            {
                if (claims.TryGetValue(claimType, out string? value) && !string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        public async Task<DateTime> GetTokenExpiryTimeAsync()
        {
            string? token = await GetTokenAsync();

            if (string.IsNullOrEmpty(token))
                return DateTime.MinValue;

            try
            {
                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                    return DateTime.MinValue;

                var jwtToken = handler.ReadJwtToken(token);
                return jwtToken.ValidTo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token expiry time");
                return DateTime.MinValue;
            }
        }

        private bool IsValidJwt(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                    return false;

                var jwtToken = handler.ReadJwtToken(token);

                return jwtToken.ValidTo > DateTime.UtcNow.AddSeconds(10);
            }
            catch
            {
                return false;
            }
        }
    }

   
}