using OneAccess.Models;

namespace OneAccess.Services;

public class AppRegistryService
{
    private readonly List<RegisteredApp> _apps;

    public AppRegistryService(IConfiguration configuration)
    {
        _apps = configuration.GetSection("RegisteredApps").Get<List<RegisteredApp>>() ?? [];
    }

    public RegisteredApp? GetApp(string appIdentifier)
    {
        return _apps.FirstOrDefault(a =>
            string.Equals(a.AppIdentifier, appIdentifier, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAuthorized(string appIdentifier, List<string> userGroups)
    {
        var app = GetApp(appIdentifier);
        if (app is null) return false;
        return app.AllowedGroups.Any(g =>
            userGroups.Contains(g, StringComparer.OrdinalIgnoreCase));
    }

    public bool ValidateRedirectUri(string appIdentifier, string redirectUri)
    {
        var app = GetApp(appIdentifier);
        if (app is null) return false;

        var allowedUri = new Uri(app.AppURL);
        var requestedUri = new Uri(redirectUri);

        return allowedUri.Host.Equals(requestedUri.Host, StringComparison.OrdinalIgnoreCase)
            && allowedUri.Port == requestedUri.Port
            && requestedUri.AbsolutePath.StartsWith(allowedUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }
}
