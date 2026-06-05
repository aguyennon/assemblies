using CutListDisplay.Hubs;
using CutListDisplay.Services;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]
var builder = WebApplication.CreateBuilder(args);

// register services with the dependency injection (DI) container
builder.Services.AddSingleton<BatchLookupService>();
builder.Services.AddSingleton<CutListService>();

// signalR = the real-time push backbone
builder.Services.AddSignalR();

// bind to all network interfaces on port 5000 so other machines on the LAN can reach
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();

// serve the static display page (wwwroot/index.html) at the site root
app.UseDefaultFiles();   // makes "/" serve index.html (the main page)
app.UseStaticFiles();    // serves files from wwwroot

// map the hub to a URL the browser connects to
app.MapHub<ScanHub>("/scanHub");

app.Run();