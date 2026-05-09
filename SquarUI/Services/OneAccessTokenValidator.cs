using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shared.Encryption;
using Shared.Models;

namespace SquarUI.Services;

public class OneAccessTokenValidator
{
    private readonly string _aesKey;
    private readonly string _aesIV;
    private readonly string _expectedContextName;
    private readonly string _oktaIssuer;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly ILogger<OneAccessTokenValidator> _logger;

    public OneAccessTokenValidator(IConfiguration configuration, ILogger<OneAccessTokenValidator> logger)
    {
        _logger = logger;
        _aesKey = configuration["Encryption:AesKey"] ?? throw new InvalidOperationException("AES key not configured");
        _aesIV = configuration["Encryption:AesIV"] ?? throw new InvalidOperationException("AES IV not configured");
        _expectedContextName = configuration["OneAccess:ExpectedContextName"] ?? throw new InvalidOperationException("ExpectedContextName not configured");

        var oktaDomain = configuration["Okta:Domain"]!;
        var authServerId = configuration["Okta:AuthorizationServerId"] ?? "default";
        _oktaIssuer = $"{oktaDomain}/oauth2/{authServerId}";

        var metadataAddress = $"{_oktaIssuer}/.well-known/openid-configuration";
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
    }

    public OneAccessToken? DecryptToken(string encryptedToken)
    {
        var token = AesTokenEncryptor.Decrypt(encryptedToken, _aesKey, _aesIV);
        if (token is null)
        {
            _logger.LogWarning("Failed to decrypt OneAccess token");
            return null;
        }

        if (!string.Equals(token.ContextName, _expectedContextName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("ContextName mismatch: expected {Expected}, got {Actual}", _expectedContextName, token.ContextName);
            return null;
        }

        _logger.LogInformation("Token decrypted for user {User}, context {Context}", token.UserName, token.ContextName);
        return token;
    }

    public async Task<ClaimsPrincipal?> ValidateOktaJwt(string accessToken)
    {
        try
        {
            var openIdConfig = await _configManager.GetConfigurationAsync(CancellationToken.None);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _oktaIssuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = openIdConfig.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var handler = new JwtSecurityTokenHandler();
            var result = await handler.ValidateTokenAsync(accessToken, validationParams);

            if (!result.IsValid)
            {
                _logger.LogWarning("Okta JWT validation failed: {Exception}", result.Exception?.Message);
                return null;
            }

            _logger.LogInformation("Okta JWT validated successfully");
            return new ClaimsPrincipal(result.ClaimsIdentity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception validating Okta JWT");
            return null;
        }
    }
}
