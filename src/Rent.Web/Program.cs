using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Data.Seed;
using Rent.Web.Infrastructure.Localization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));
}

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        var culture = PageModelLocalizationExtensions.ResolveCulture(context.HttpContext);
        var redirectUri = "/" + culture + "/login?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
        context.Response.Redirect(redirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        var culture = PageModelLocalizationExtensions.ResolveCulture(context.HttpContext);
        context.Response.Redirect("/" + culture + "/login");
        return Task.CompletedTask;
    };
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleConfigured = !string.IsNullOrWhiteSpace(googleClientId)
                       && !string.IsNullOrWhiteSpace(googleClientSecret);

if (googleConfigured)
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId!;
            options.ClientSecret = googleClientSecret!;
        });
}

// The Identity.External cookie defaults to a 5-minute ExpireTimeSpan, which is too short
// for a user reading the role-picker on /external-login-confirm. Bump to 30 min so the
// signup completes even if the user takes a few minutes to decide.
builder.Services.ConfigureExternalCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped<Rent.Web.Features.Search.SearchHandler>();
builder.Services.AddScoped<Rent.Web.Features.Maps.MapMarkersHandler>();
builder.Services.AddScoped<Rent.Web.Features.Favorites.IFavoriteService, Rent.Web.Features.Favorites.FavoriteService>();
builder.Services.AddScoped<Rent.Web.Features.Admin.Services.IPopularSearchTracker, Rent.Web.Features.Admin.Services.PopularSearchTracker>();

builder.Services.Configure<Rent.Web.Features.Maps.MapsOptions>(
    builder.Configuration.GetSection(Rent.Web.Features.Maps.MapsOptions.SectionName));

builder.Services.Configure<Rent.Web.Features.Email.EmailOptions>(
    builder.Configuration.GetSection(Rent.Web.Features.Email.EmailOptions.SectionName));

var emailApiKey = builder.Configuration[$"{Rent.Web.Features.Email.EmailOptions.SectionName}:ApiKey"];
var emailConfigured = !string.IsNullOrWhiteSpace(emailApiKey);

if (emailConfigured)
{
    builder.Services.AddHttpClient<Rent.Web.Features.Email.ResendEmailSender>(client =>
    {
        var baseUrl = builder.Configuration[$"{Rent.Web.Features.Email.EmailOptions.SectionName}:BaseUrl"];
        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(baseUrl) ? "https://api.resend.com" : baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddScoped<Rent.Web.Features.Email.IEmailSender>(sp =>
        sp.GetRequiredService<Rent.Web.Features.Email.ResendEmailSender>());
}
else
{
    builder.Services.AddSingleton<Rent.Web.Features.Email.IEmailSender, Rent.Web.Features.Email.NoOpEmailSender>();
}

builder.Services.Configure<Rent.Web.Features.AiChat.AiOptions>(
    builder.Configuration.GetSection(Rent.Web.Features.AiChat.AiOptions.SectionName));

var aiApiKey = builder.Configuration[$"{Rent.Web.Features.AiChat.AiOptions.SectionName}:OpenRouterApiKey"];
var aiConfigured = !string.IsNullOrWhiteSpace(aiApiKey);

if (aiConfigured)
{
    builder.Services.AddHttpClient<Rent.Web.Features.AiChat.Services.OpenRouterClient>(client =>
    {
        var baseUrl = builder.Configuration[$"{Rent.Web.Features.AiChat.AiOptions.SectionName}:BaseUrl"];
        var timeoutSeconds = builder.Configuration.GetValue<int?>(
            $"{Rent.Web.Features.AiChat.AiOptions.SectionName}:RequestTimeoutSeconds") ?? 60;
        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(baseUrl)
            ? "https://openrouter.ai/api/v1/"
            : baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    });
    builder.Services.AddScoped<Rent.Web.Features.AiChat.Services.IOpenRouterClient>(sp =>
        sp.GetRequiredService<Rent.Web.Features.AiChat.Services.OpenRouterClient>());
}
else
{
    builder.Services.AddSingleton<Rent.Web.Features.AiChat.Services.IOpenRouterClient,
        Rent.Web.Features.AiChat.Services.NoOpOpenRouterClient>();
}

builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.SearchPropertiesTool>();
builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.CreateAlertTool>();
builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.GetCityInfoTool>();
builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.GetPropertyDetailsTool>();
builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.IAiTool>(sp =>
    sp.GetRequiredService<Rent.Web.Features.AiChat.Tools.SearchPropertiesTool>());
builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.IAiTool>(sp =>
    sp.GetRequiredService<Rent.Web.Features.AiChat.Tools.CreateAlertTool>());
builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.IAiTool>(sp =>
    sp.GetRequiredService<Rent.Web.Features.AiChat.Tools.GetCityInfoTool>());
builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.IAiTool>(sp =>
    sp.GetRequiredService<Rent.Web.Features.AiChat.Tools.GetPropertyDetailsTool>());
builder.Services.AddScoped<Rent.Web.Features.AiChat.Tools.ToolRegistry>();
builder.Services.AddSingleton<Rent.Web.Features.AiChat.Services.IRateLimiter,
    Rent.Web.Features.AiChat.Services.InMemoryRateLimiter>();
builder.Services.AddScoped<Rent.Web.Features.AiChat.Services.IAiChatService,
    Rent.Web.Features.AiChat.Services.AiChatService>();

builder.Services.Configure<Rent.Web.Infrastructure.Storage.StorageOptions>(
    builder.Configuration.GetSection("ImageStorage"));

var storageProvider = builder.Configuration.GetValue<string>("ImageStorage:Provider") ?? "Local";
if (string.Equals(storageProvider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<Rent.Web.Infrastructure.Storage.IImageStorage,
        Rent.Web.Infrastructure.Storage.AzureBlobImageStorage>();
}
else
{
    builder.Services.AddScoped<Rent.Web.Infrastructure.Storage.IImageStorage,
        Rent.Web.Infrastructure.Storage.LocalImageStorage>();
}

builder.Services.AddHealthChecks();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(LocalizationConfig.DefaultCulture);
    options.SupportedCultures = LocalizationConfig.SupportedCultureInfos;
    options.SupportedUICultures = LocalizationConfig.SupportedCultureInfos;
    options.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider
    {
        RouteDataStringKey = "culture",
        UIRouteDataStringKey = "culture",
        Options = options
    });
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.ConstraintMap["locale"] = typeof(LocaleRouteConstraint);
});

var razorBuilder = builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Features";
});

if (builder.Environment.IsDevelopment())
{
    razorBuilder.AddRazorRuntimeCompilation();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseSerilogRequestLogging();
app.UseMiddleware<PathLocaleRedirectMiddleware>();
app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapHealthChecks("/health");

if (!googleConfigured)
{
    app.Logger.LogWarning(
        "Google authentication not configured (missing Authentication:Google:ClientId/ClientSecret). External login disabled.");
}

if (!emailConfigured)
{
    app.Logger.LogWarning(
        "Email not configured (missing Email:ApiKey). Falling back to NoOpEmailSender.");
}

if (!aiConfigured)
{
    app.Logger.LogWarning(
        "AI assistant not configured (missing Ai:OpenRouterApiKey). Falling back to NoOpOpenRouterClient.");
}

if (!app.Environment.IsEnvironment("Testing"))
{
    await DatabaseSeeder.RunAsync(app.Services);
}

app.Run();

public partial class Program { }
