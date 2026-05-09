using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using SquarUI.Services;

namespace SquarUI.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly OneAccessTokenValidator _tokenValidator;
    private readonly string _tokenParameterName;
    private readonly ILogger<AuthController> _logger;

    public AuthController(OneAccessTokenValidator tokenValidator, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _tokenValidator = tokenValidator;
        _tokenParameterName = configuration["OneAccess:TokenParameterName"] ?? "squarToken";
        _logger = logger;
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback()
    {
        var error = Request.Query["error"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogWarning("Received error from OneAccess: {Error}", error);
            return RedirectToAction("AccessDenied", "Home", new
            {
                diag = Request.Query["diag"].FirstOrDefault()
            });
        }

        var encryptedToken = Request.Query[_tokenParameterName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(encryptedToken))
        {
            _logger.LogWarning("No token or error received in callback");
            return BadRequest("Missing token parameter.");
        }

        var oneAccessToken = _tokenValidator.DecryptToken(encryptedToken);
        if (oneAccessToken is null)
        {
            _logger.LogWarning("Token decryption or context validation failed");
            return Unauthorized("Invalid or tampered token.");
        }

        var principal = await _tokenValidator.ValidateOktaJwt(oneAccessToken.AccessToken);
        if (principal is null)
        {
            _logger.LogWarning("Okta JWT validation failed for user {User}", oneAccessToken.UserName);
            return Unauthorized("Invalid Okta access token.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, oneAccessToken.UserId.ToString()),
            new(ClaimTypes.Name, oneAccessToken.UserName),
            new(ClaimTypes.Email, oneAccessToken.Email),
            new("FullName", oneAccessToken.FullName),
            new("LoginTime", oneAccessToken.LoginTime.ToString("O")),
            new("ContextName", oneAccessToken.ContextName)
        };

        foreach (var group in oneAccessToken.Groups)
            claims.Add(new Claim(ClaimTypes.Role, group));

        var identity = new ClaimsIdentity(claims, "OneAccessCookie");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("OneAccessCookie", claimsPrincipal, new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });

        _logger.LogInformation("User {User} authenticated via OneAccess for SQUAR", oneAccessToken.UserName);

        return Redirect("/");
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("OneAccessCookie");
        _logger.LogInformation("User logged out from SQUAR");
        return Redirect("/");
    }
}
