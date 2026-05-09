using OneAccess.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AppRegistryService>();
builder.Services.AddSingleton<OktaAuthService>();

var app = builder.Build();
app.UseRouting();
app.MapControllers();

app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head><title>OneAccess Gateway</title>
    <style>
        body { font-family: sans-serif; max-width: 600px; margin: 80px auto; color: #333; }
        h1 { color: #1a56db; }
        a { color: #1a56db; text-decoration: none; }
        a:hover { text-decoration: underline; }
        .apps { margin-top: 24px; }
        .app { padding: 12px; border: 1px solid #ddd; border-radius: 6px; margin-bottom: 8px; }
    </style>
    </head>
    <body>
        <h1>OneAccess Gateway</h1>
        <p>Use a downstream application to initiate login.</p>
        <div class="apps">
            <div class="app"><a href="http://localhost:5001">SQUAR UI</a> &mdash; Port 5001</div>
            <div class="app"><a href="http://localhost:5002">Letters Admin</a> &mdash; Port 5002</div>
        </div>
    </body>
    </html>
    """, "text/html"));

app.Run();
