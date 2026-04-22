using FluentValidation;
using SqlSugar;
using UrlShortener.Api.Hosted;
using UrlShortener.Core.Cache;
using UrlShortener.Core.Dtos;
using UrlShortener.Core.Entities;
using UrlShortener.Core.Interfaces;
using UrlShortener.Core.Repositories;
using UrlShortener.Core.Services;
using UrlShortener.Core.Validators;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Data access: SqlSugar
// ---------------------------------------------------------------------------
// Scoped = one instance per HTTP request (and one per hosted-service scope).
// Same choice api-member makes — ISqlSugarClient is not thread-safe for
// concurrent queries on the same connection.
builder.Services.AddScoped<ISqlSugarClient>(_ =>
{
    var cs = builder.Configuration.GetConnectionString("Mssql")
             ?? throw new InvalidOperationException("Missing ConnectionStrings:Mssql");
    return new SqlSugarClient(new ConnectionConfig
    {
        ConnectionString = cs,
        DbType = DbType.SqlServer,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute,
    });
});

// ---------------------------------------------------------------------------
// Redis: single shared connection for the whole app
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<RedisConnection>();
builder.Services.AddSingleton<ILinkCacheManager, LinkCacheManager>();

// ---------------------------------------------------------------------------
// Domain services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ILinkRepository, LinkRepository>();
builder.Services.AddScoped<ILinkService, LinkService>();

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------
// Registered explicitly (one AddScoped per validator) — same style as api-member's
// AddValidateService() in BusinessServiceCollectionExtension.cs. Avoids pulling
// in FluentValidation.DependencyInjectionExtensions just for auto-discovery.
builder.Services.AddScoped<IValidator<CreateLinkRequest>, CreateLinkRequestValidator>();

// ---------------------------------------------------------------------------
// Web
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ---------------------------------------------------------------------------
// Background services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<ClickFlushHostedService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Schema bootstrap (POC-style; proper migrations are POC #8)
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
    db.DbMaintenance.CreateDatabase();           // no-op if the DB already exists
    db.CodeFirst.InitTables<ShortLink>();        // creates the ShortLinks table on first run
}

// ---------------------------------------------------------------------------
// Global exception middleware — tiny, POC-only.
// Proper error handling is POC #4.
// ---------------------------------------------------------------------------
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled exception");
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await ctx.Response.WriteAsJsonAsync(new { error = "internal_error" });
    }
});

app.UseDefaultFiles();  // maps / -> /index.html
app.UseStaticFiles();   // serves files from wwwroot/
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();

app.Run();
