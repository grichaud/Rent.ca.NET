using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rent.Web.Domain;
using Rent.Web.Infrastructure.Data;
using Rent.Web.Infrastructure.Data.Seed;
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
        options.UseSqlServer(connectionString));
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

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped<Rent.Web.Features.Search.SearchHandler>();

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
app.UseRouting();
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

if (!app.Environment.IsEnvironment("Testing"))
{
    await DatabaseSeeder.RunAsync(app.Services);
}

app.Run();

public partial class Program { }
