using System.Text.RegularExpressions;
using JevSetupScore.Api.Data;
using JevSetupScore.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var useMySql = provider.Equals("MySql", StringComparison.OrdinalIgnoreCase)
    || provider.Equals("MySQL", StringComparison.OrdinalIgnoreCase);

if (!useMySql)
{
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=jev.db";
    var dataSource = Regex.Match(connectionString, @"Data Source=([^;]+)", RegexOptions.IgnoreCase);
    if (dataSource.Success)
    {
        var directory = Path.GetDirectoryName(dataSource.Groups[1].Value);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    builder.Services.AddDbContext<SqliteAppDbContext>(options => options.UseSqlite(connectionString));
    builder.Services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqliteAppDbContext>());
}
else
{
    var mysql = builder.Configuration.GetConnectionString("MySql");
    if (string.IsNullOrWhiteSpace(mysql))
        throw new InvalidOperationException("Database:Provider is MySql, but ConnectionStrings:MySql is empty.");

    var version = MySqlVersionResolver.Resolve(mysql, builder.Configuration["Database:MySqlVersion"]);
    builder.Services.AddDbContext<MysqlAppDbContext>(options => options.UseMySql(mysql, version));
    builder.Services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<MysqlAppDbContext>());
}

builder.Services.Configure<DataOptions>(builder.Configuration.GetSection("Data"));
builder.Services.PostConfigure<DataOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.CacheDirectory) || !Directory.Exists(options.CacheDirectory))
        options.CacheDirectory = ResolveCacheDirectory(builder.Environment);
});

builder.Services.AddHttpClient<MarketDataService>(client =>
{
    var seconds = builder.Configuration.GetValue("Data:HttpTimeoutSeconds", 8);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(seconds, 2, 30));
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; JevSetupScore/1.1; educational)");
});
builder.Services.AddSingleton<PublicLinks>();
builder.Services.AddSingleton<StudyCache>();
builder.Services.AddScoped<ScoreService>();
builder.Services.AddSingleton<PdfReportService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    var publicBase = builder.Configuration["PublicBaseUrl"]?.Trim().TrimEnd('/');
    if (!string.IsNullOrWhiteSpace(publicBase))
        swagger.AddServer(new Microsoft.OpenApi.Models.OpenApiServer { Url = publicBase });
});

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>();
if (origins is null || origins.Length == 0)
    origins = ["http://localhost:4200"];
builder.Services.AddCors(options => options.AddPolicy("ui", policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

var pathBase = app.Configuration["PathBase"]?.Trim();
if (!string.IsNullOrWhiteSpace(pathBase))
{
    if (!pathBase.StartsWith('/'))
        pathBase = "/" + pathBase;
    app.UsePathBase(pathBase.TrimEnd('/'));
}

app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var status = error is SetupException setup ? setup.Status : StatusCodes.Status500InternalServerError;
        var message = error is SetupException known ? known.Message : "The score service failed.";
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ui");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var market = scope.ServiceProvider.GetRequiredService<MarketDataService>();
    await market.InitializeAsync(CancellationToken.None);
}

app.Run();

static string ResolveCacheDirectory(IWebHostEnvironment env)
{
    string[] candidates =
    [
        Path.Combine(env.ContentRootPath, "data", "cache"),
        Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "data", "cache")),
        Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "data", "cache")),
        "/app/data/cache",
    ];
    foreach (var candidate in candidates)
    {
        if (Directory.Exists(candidate))
            return candidate;
    }

    return candidates[1];
}
