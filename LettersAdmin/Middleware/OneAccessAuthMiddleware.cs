namespace LettersAdmin.Middleware;

public class OneAccessAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _loginUrl;
    private readonly string _appIdentifier;

    public OneAccessAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _loginUrl = configuration["OneAccess:LoginUrl"] ?? "http://localhost:5000/auth/login";
        _appIdentifier = configuration["OneAccess:AppIdentifier"] ?? "lettersadmin";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/auth") ||
            context.Request.Path.StartsWithSegments("/Home/AccessDenied"))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect($"{_loginUrl}?app={_appIdentifier}");
            return;
        }

        await _next(context);
    }
}
