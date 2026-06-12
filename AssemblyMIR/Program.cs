using AssemblyMIR.Hubs;
using AssemblyMIR.Services;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BatchLookupService>();
builder.Services.AddSingleton<CutListService>();

// SignalR = the real-time push backbone.
builder.Services.AddSignalR();

// Bind to all network interfaces on port 5000 so other machines on the LAN can reach
builder.WebHost.UseUrls("http://0.0.0.0:5002");

var app = builder.Build();

// Serve the static display page (wwwroot/index.html) at the site root.
app.UseDefaultFiles();   // makes "/" serve index.html
app.UseStaticFiles();    // serves files from wwwroot

// Map the hub to a URL the browser connects to.
app.MapHub<ScanHub>("/scanHub");

app.Run();