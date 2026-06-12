using AssemblyMX.Hubs;
using AssemblyMX.Services;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]
var builder = WebApplication.CreateBuilder(args);

// --- Register services with the Dependency Injection (DI) container ---
builder.Services.AddSingleton<BatchLookupService>();
builder.Services.AddSingleton<CutListService>();

// SignalR = the real-time push backbone.
builder.Services.AddSignalR();

builder.WebHost.UseUrls("http://0.0.0.0:5001");

var app = builder.Build();


app.UseDefaultFiles();   // makes "/" serve index.html
app.UseStaticFiles();    // serves files from wwwroot

// Map the hub to a URL the browser connects to.
app.MapHub<ScanHub>("/scanHub");

app.Run();