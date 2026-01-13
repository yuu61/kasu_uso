using kasu_uso.Components;
using kasu_uso.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

// Prometheusメトリクスサービスを追加
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddSingleton<KasuUsoService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// HTTPSリダイレクトと静的ファイルはパイプラインの早い段階で
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// HTTPリクエストメトリクスを収集
app.UseHttpMetrics();

app.UseAntiforgery();

app.MapMetrics();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();