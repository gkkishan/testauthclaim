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

        var state = _oktaAuth.GenerateState();
        var (codeVerifier, codeChallenge) = _oktaAuth.GeneratePkce();

        HttpContext.Session.SetString("auth_app", app);
        HttpContext.Session.SetString("auth_redirect_uri", callbackUrl);
        HttpContext.Session.SetString("auth_state", state);
        HttpContext.Session.SetString("auth_code_verifier", codeVerifier);

        var authorizeUrl = _oktaAuth.BuildAuthorizeUrl(state, codeChallenge);

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

        var savedState = HttpContext.Session.GetString("auth_state");
        if (savedState != state)
        {
            _logger.LogWarning("State mismatch: expected {Expected}, got {Actual}", savedState, state);
            return BadRequest("Invalid state parameter. Possible CSRF attack.");
        }

        var appIdentifier = HttpContext.Session.GetString("auth_app");
        var redirectUri = HttpContext.Session.GetString("auth_redirect_uri");
        var codeVerifier = HttpContext.Session.GetString("auth_code_verifier");

        if (string.IsNullOrWhiteSpace(appIdentifier) || string.IsNullOrWhiteSpace(redirectUri) || string.IsNullOrWhiteSpace(codeVerifier))
        {
            _logger.LogError("Session data missing for callback");
            return BadRequest("Session expired. Please try logging in again.");
        }

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
            var separator = redirectUri!.Contains('?') ? "&" : "?";
            return Redirect($"{redirectUri}{separator}error=access_denied");
        }

        var app = _appRegistry.GetApp(appIdentifier)!;

        var oneAccessToken = new OneAccessToken
        {
            AccessToken = tokenResponse.AccessToken,
            UserId = Math.Abs(idTokenClaims.Sub.GetHashCode()),
            UserName = idTokenClaims.PreferredUsername,
            ContextName = app.AppName,
            LoginTime = DateTime.UtcNow
        };

        var aesKey = _configuration["Encryption:AesKey"] ?? throw new InvalidOperationException("AES key not configured");
        var aesIV = _configuration["Encryption:AesIV"] ?? throw new InvalidOperationException("AES IV not configured");
        var encryptedToken = AesTokenEncryptor.Encrypt(oneAccessToken, aesKey, aesIV);

        _logger.LogInformation("Token issued for user {User} to app {App}", idTokenClaims.PreferredUsername, appIdentifier);

        HttpContext.Session.Clear();

        var separator2 = redirectUri!.Contains('?') ? "&" : "?";
        var finalUrl = $"{redirectUri}{separator2}{app.TokenParameterName}={Uri.EscapeDataString(encryptedToken)}";

        return Redirect(finalUrl);
    }
}
