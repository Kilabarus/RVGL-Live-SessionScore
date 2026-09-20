using RvglLiveScore.Components;
using RvglLiveScore.Services;

var builder = WebApplication.CreateBuilder(args);

// Render supplies PORT. Locally use --urls or the ASP.NET Core default port.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddOptions<CoordinatorOptions>()
    .BindConfiguration("Coordinator")
    .Validate(options => Uri.TryCreate(options.SessionsUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
        "Coordinator:SessionsUrl must be an absolute HTTP(S) URL.")
    .Validate(options => options.PollIntervalSeconds is >= 2 and <= 300,
        "Coordinator:PollIntervalSeconds must be between 2 and 300.")
    .ValidateOnStart();
builder.Services.AddHttpClient("Coordinator", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddSingleton<LobbyDirectory>();
builder.Services.AddHttpClient("CoordinatorStream", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
    client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
});
builder.Services.AddTransient<CoordinatorSessionClient>();
builder.Services.AddHostedService<CoordinatorWorker>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
}

// Render terminates HTTPS at its proxy. No HTTPS redirect is needed here.
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/lobbies", (LobbyDirectory directory) => directory.Snapshot);
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
