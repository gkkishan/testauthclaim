using SquarUI.Middleware;
using SquarUI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<OneAccessTokenValidator>();

builder.Services.AddAuthentication("OneAccessCookie")
    .AddCookie("OneAccessCookie", options =>
    {
        options.LoginPath = "/auth/callback";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
    });

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OneAccessAuthMiddleware>();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
