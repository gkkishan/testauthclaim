using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OneAccess.Services;

public class OktaAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _domain;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _authServerId;
    private readonly ILogger<OktaAuthService> _logger;

    public OktaAuthService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<OktaAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _domain = configuration["Okta:Domain"] ?? throw new InvalidOperationException("Okta:Domain not configured");
        _clientId = configuration["Okta:ClientId"] ?? throw new InvalidOperationException("Okta:ClientId not configured");
        _clientSecret = configuration["Okta:ClientSecret"] ?? throw new InvalidOperationException("Okta:ClientSecret not configured");
        _authServerId = configuration["Okta:AuthorizationServerId"] ?? "default";
    }

    public (string codeVerifier, string codeChallenge) GeneratePkce()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var codeVerifier = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (codeVerifier, codeChallenge);
    }

    public string GenerateState() => Guid.NewGuid().ToString("N");

    public string BuildAuthorizeUrl(string state, string codeChallenge)
    {
        var baseUrl = $"{_domain}/oauth2/{_authServerId}/v1/authorize";
        var redirectUri = Uri.EscapeDataString("http://localhost:5050/auth/callback/okta");

        return $"{baseUrl}?response_type=code&client_id={Uri.EscapeDataString(_clientId)}" +
               $"&redirect_uri={redirectUri}" +
               $"&scope={Uri.EscapeDataString("openid profile email groups")}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
               $"&code_challenge_method=S256";
    }

    public async Task<OktaTokenResponse?> ExchangeCodeForTokens(string code, string codeVerifier)
    {
        var client = _httpClientFactory.CreateClient();
        var tokenUrl = $"{_domain}/oauth2/{_authServerId}/v1/token";

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = "http://localhost:5050/auth/callback/okta",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["code_verifier"] = codeVerifier
        };

        var content = new FormUrlEncodedContent(parameters);

        _logger.LogInformation("Exchanging authorization code for tokens at {TokenUrl}", tokenUrl);

        var response = await client.PostAsync(tokenUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Token exchange failed: {StatusCode} {Error}", response.StatusCode, error);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OktaTokenResponse>(json);
    }

    public IdTokenClaims ParseIdToken(string idToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(idToken);

        var claims = new IdTokenClaims
        {
            Sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? string.Empty,
            Email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? string.Empty,
            PreferredUsername = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? string.Empty,
            Name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? string.Empty,
            Groups = jwt.Claims.Where(c => c.Type == "groups").Select(c => c.Value).ToList()
        };

        _logger.LogInformation("Parsed ID token for user {Username} with groups [{Groups}]",
            claims.PreferredUsername, string.Join(", ", claims.Groups));

        return claims;
    }
}

public class OktaTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}

public class IdTokenClaims
{
    public string Sub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PreferredUsername { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Groups { get; set; } = [];
}
