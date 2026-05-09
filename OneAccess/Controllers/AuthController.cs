using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OneAccess.Services;
using Shared.Encryption;
using Shared.Models;

namespace OneAccess.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly OktaAuthService _oktaAuth;
    private readonly AppRegistryService _appRegistry;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        OktaAuthService oktaAuth,
        AppRegistryService appRegistry,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _oktaAuth = oktaAuth;
        _appRegistry = appRegistry;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? app, [FromQuery] string? redirect_uri)
    {
        if (string.IsNullOrWhiteSpace(app))
        {
            _logger.LogWarning("Login attempt without app parameter");
            return BadRequest("The 'app' query parameter is required.");
        }

        var registeredApp = _appRegistry.GetApp(app);
        if (registeredApp is null)
        {
            _logger.LogWarning("Login attempt for unregistered app: {App}", app);
            return BadRequest($"Application '{app}' is not registered.");
        }

        var callbackUrl = registeredApp.AppURL;
        if (!string.IsNullOrWhiteSpace(redirect_uri))
        {
            if (!_appRegistry.ValidateRedirectUri(app, redirect_uri))
            {
                _logger.LogWarning("Invalid redirect_uri for app {App}: {RedirectUri}", app, redirect_uri);
                return BadRequest("The provided redirect_uri is not allowed for this application.");
            }
            callbackUrl = redirect_uri;
        }

        var (codeVerifier, codeChallenge) = _oktaAuth.GeneratePkce();

        // Pack auth context into the state parameter (AES encrypted) to avoid session dependency
        var authState = new AuthState
        {
            App = app,
            RedirectUri = callbackUrl,
            CodeVerifier = codeVerifier,
            Nonce = Guid.NewGuid().ToString("N")
        };
        var stateJson = JsonSerializer.Serialize(authState);
        var aesKey = _configuration["Encryption:AesKey"]!;
        var aesIV = _configuration["Encryption:AesIV"]!;
        var encryptedState = AesTokenEncryptor.Encrypt(
            new OneAccessToken { AccessToken = stateJson, UserName = "", ContextName = "" },
            aesKey, aesIV);

        var authorizeUrl = _oktaAuth.BuildAuthorizeUrl(encryptedState, codeChallenge);

        _logger.LogInformation("Login initiated for app {App}, redirecting to Okta", app);

        return Redirect(authorizeUrl);
    }

    [HttpGet("callback/okta")]
    public async Task<IActionResult> OktaCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogError("Okta returned error: {Error}", error);
            return BadRequest($"Okta authentication error: {error}");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest("Missing code or state parameter from Okta.");
        }

        // Decrypt the state to recover auth context
        var aesKey = _configuration["Encryption:AesKey"]!;
        var aesIV = _configuration["Encryption:AesIV"]!;
        var decryptedState = AesTokenEncryptor.Decrypt(state, aesKey, aesIV);
        if (decryptedState is null)
        {
            _logger.LogWarning("Failed to decrypt state parameter");
            return BadRequest("Invalid state parameter.");
        }

        AuthState? authState;
        try
        {
            authState = JsonSerializer.Deserialize<AuthState>(decryptedState.AccessToken);
        }
        catch
        {
            _logger.LogWarning("Failed to parse state parameter");
            return BadRequest("Invalid state parameter.");
        }

        if (authState is null || string.IsNullOrWhiteSpace(authState.App) ||
            string.IsNullOrWhiteSpace(authState.RedirectUri) ||
            string.IsNullOrWhiteSpace(authState.CodeVerifier))
        {
            _logger.LogError("Incomplete auth state in callback");
            return BadRequest("Invalid auth state. Please try logging in again.");
        }

        var appIdentifier = authState.App;
        var redirectUri = authState.RedirectUri;
        var codeVerifier = authState.CodeVerifier;

        _logger.LogInformation("Okta callback received for app {App}", appIdentifier);

        var tokenResponse = await _oktaAuth.ExchangeCodeForTokens(code, codeVerifier);
        if (tokenResponse is null)
        {
            _logger.LogError("Failed to exchange code for tokens");
            return StatusCode(502, "Failed to exchange authorization code with Okta.");
        }

        var idTokenClaims = _oktaAuth.ParseIdToken(tokenResponse.IdToken);

        if (!_appRegistry.IsAuthorized(appIdentifier, idTokenClaims.Groups))
        {
            _logger.LogWarning("Access denied for user {User} to app {App}. User groups: [{Groups}]",
                idTokenClaims.PreferredUsername, appIdentifier, string.Join(", ", idTokenClaims.Groups));

            var registeredApp = _appRegistry.GetApp(appIdentifier)!;
            var diagData = new
            {
                user = idTokenClaims.PreferredUsername,
                email = idTokenClaims.Email,
                sub = idTokenClaims.Sub,
                name = idTokenClaims.Name,
                app = registeredApp.AppName,
                user_groups = idTokenClaims.Groups,
                required_groups = registeredApp.AllowedGroups,
                issuer = idTokenClaims.Issuer,
                audience = idTokenClaims.Audience,
                issued_at = idTokenClaims.IssuedAt.ToString("O"),
                expiry = idTokenClaims.Expiry.ToString("O"),
                all_claims = idTokenClaims.AllClaims
            };
            var diagJson = Uri.EscapeDataString(JsonSerializer.Serialize(diagData));
            var separator = redirectUri.Contains('?') ? "&" : "?";
            return Redirect($"{redirectUri}{separator}error=access_denied&diag={diagJson}");
        }

        var app = _appRegistry.GetApp(appIdentifier)!;

        var oneAccessToken = new OneAccessToken
        {
            AccessToken = tokenResponse.AccessToken,
            UserId = Math.Abs(idTokenClaims.Sub.GetHashCode()),
            UserName = idTokenClaims.PreferredUsername,
            Email = idTokenClaims.Email,
            FullName = idTokenClaims.Name,
            Groups = idTokenClaims.Groups,
            ContextName = app.AppName,
            LoginTime = DateTime.UtcNow
        };

        var encryptedToken = AesTokenEncryptor.Encrypt(oneAccessToken, aesKey, aesIV);

        _logger.LogInformation("Token issued for user {User} to app {App}", idTokenClaims.PreferredUsername, appIdentifier);

        var separator2 = redirectUri.Contains('?') ? "&" : "?";
        var finalUrl = $"{redirectUri}{separator2}{app.TokenParameterName}={Uri.EscapeDataString(encryptedToken)}";

        return Redirect(finalUrl);
    }
}

internal class AuthState
{
    public string App { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
}
