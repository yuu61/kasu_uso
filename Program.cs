using kasu_uso.Components;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});

builder.Services.AddServerSideBlazor(options => {
    options.DetailedErrors = true;
    options.DisconnectedCircuitRetentionPeriod =
        TimeSpan.FromMinutes(5);
})
.AddHubOptions(hubOptions => {
    hubOptions.KeepAliveInterval = TimeSpan.FromSeconds(30);
    hubOptions.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.Secure = CookieSecurePolicy.Always;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseCookiePolicy();

app.UseForwardedHeaders();

app.UseAntiforgery();

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
