# Rent.ca.NET

[![Live on Azure](https://img.shields.io/badge/Live%20on-Azure%20App%20Service-0078D4?logo=microsoftazure&logoColor=white)](https://rent-ca-net.azurewebsites.net)
[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)

> ASP.NET Core 9 port of [rent-ca.vercel.app](https://rent-ca.vercel.app/en) &mdash; a marketplace for Canadian rentals originally built in Next.js + Supabase.

This repo exists to show the same product in a pure Microsoft stack: **ASP.NET Core MVC + Razor Pages, EF Core, SQL Server, ASP.NET Core Identity, Tailwind CSS, deployed to Azure.** The Next.js original and this .NET port are feature-equivalent for the MVP slice and serve the same data model.

| &nbsp; | Next.js version | .NET version (this repo) |
| --- | --- | --- |
| Live URL | https://rent-ca.vercel.app/en | https://rent-ca-net.azurewebsites.net |
| Runtime | Node.js | .NET 9 |
| Web framework | Next.js 16 (App Router) | ASP.NET Core 9 (MVC + Razor Pages) |
| Database | Supabase (Postgres) | SQL Server |
| Auth | Supabase Auth | ASP.NET Core Identity |
| Styling | Tailwind + shadcn/ui | Tailwind + Liquid Glass |
| Hosting | Vercel | Azure App Service F1 + Azure SQL Free |

## MVP slice

This slice covers the core marketplace loop &mdash; enough to demonstrate the full stack end-to-end:

1. **Auth** &mdash; email + password signup/login with role selection (Renter / Landlord). Built on `IdentityUser<Guid>` with cookie auth.
2. **Public listings** &mdash; `/`, `/{city}`, `/{city}/{slug}`, server-rendered for SEO.
3. **Search filters** &mdash; by min/max price, bedrooms, property type, pet-friendly.
4. **Landlord portal** &mdash; create/edit/deactivate listings, multi-photo upload via `IImageStorage` abstraction (local filesystem in dev, Azure Blob Storage in prod).
5. **Inquiries** &mdash; renters (or anonymous visitors) contact landlords via a validated form; landlords manage inquiries in an inbox with unread-filter and mark-read toggle.

Deferred to later slices (wired up, not built): Google OAuth, transactional emails (Resend), renter portal with favorites and alerts, AI chat assistant (OpenRouter + Claude), Google Maps, French i18n, landlord tier upgrades, rent specials.

## Architecture

Vertical Slice layout under `src/Rent.Web/`:

```
Features/
  Auth/           signup, login, logout, forgot-password
  Home/           hero + popular cities + landlord landing + privacy
  Search/         /{city} results with filters + paginated handler
  ListingDetail/  /{city}/{slug} with gallery, units, amenities, contact form
  Inquiries/      /inquiries/submit handler
  LandlordManage/ /landlord dashboard + listings CRUD + inbox
  Shared/         _Layout, error page, validation scripts, SlugGenerator
Domain/           POCO entities (10 for MVP: ApplicationUser, LandlordProfile,
                  City, Property, Unit, PropertyImage, Amenity, ContactInquiry)
Infrastructure/
  Data/           AppDbContext (IdentityDbContext + 7 aggregates),
                  Migrations/, Seed/ (cities, amenities, sample properties)
  Identity/       Role constants
  Storage/        IImageStorage + LocalImageStorage (+ AzureBlobImageStorage in Fase 7)
```

Handlers live in the page model (`.cshtml.cs`); validators are FluentValidation `AbstractValidator<T>` implementations registered via `AddValidatorsFromAssemblyContaining<Program>`. Serilog provides structured logging via `UseSerilog`.

## Run locally

Prerequisites: .NET SDK 9.x, Node.js 20+, SQL Server (LocalDB works fine on Windows).

```bash
# 1) Restore and migrate
dotnet restore
dotnet ef database update --project src/Rent.Web

# 2) Build Tailwind once
cd src/Rent.Web
npm ci
npm run build:css

# 3) Run
cd ../..
dotnet run --project src/Rent.Web
```

On first run with `ASPNETCORE_ENVIRONMENT=Development`, the seeder creates:

* 30 Canadian cities (6 featured on the home page)
* 39 amenities
* 3 roles (Renter, Landlord, Admin)
* 10 sample listings from a demo landlord account

Demo landlord credentials (dev only):

```
email:    demo.landlord@rentca.net
password: DemoLandlord1!
```

## Database connection strings

* **Dev** (`appsettings.Development.json`): LocalDB &mdash; `Server=(localdb)\MSSQLLocalDB;Database=RentCaNet;Trusted_Connection=True;Encrypt=False;`
* **Prod** (Azure App Service App Settings): Azure SQL Database &mdash; injected via `ConnectionStrings__DefaultConnection` env var. Use the "Free Offer" tier (32 GB, 100K vCore-sec/month, auto-pause after 1h idle).

## Image storage

Property images go through the `IImageStorage` abstraction.

* **Dev**: `LocalImageStorage` writes to `wwwroot/uploads/{propertyId}/...` (gitignored).
* **Prod**: `AzureBlobImageStorage` writes to an Azure Blob Storage container with public read. Switched via the `ImageStorage:Provider` config key.

## Running tests

```bash
dotnet test
```

Integration tests use `WebApplicationFactory` from `Microsoft.AspNetCore.Mvc.Testing` with an in-memory EF Core provider to avoid touching the real database.

## Deployment (Azure, 100% free tier)

Target stack:

| Resource | Tier | Notes |
| --- | --- | --- |
| App Service Plan + App Service | **F1** (Linux, .NET 9) | Free forever. 1 GB RAM, 60 min CPU/day. Cold start ~30 s after idle. No custom domain. URL: `{app-name}.azurewebsites.net`. |
| Azure SQL Database | **Free Offer** | 32 GB, 100 K vCore-sec/month, auto-pause after 1 h idle (first query after idle takes ~20 s). |
| Storage Account | Standard LRS | First 5 GB free for 12 months, then pay-as-you-go pennies. Container `property-images` with public blob read. |
| GitHub Actions | N/A | `.github/workflows/deploy-azure.yml` runs on push to `main`. |

### One-time provisioning (via Azure Portal or `az` CLI)

Replace placeholders with your own values. The names below are examples.

```bash
RG=rg-rent-ca-net
LOCATION=canadacentral
APP=rent-ca-net
PLAN=asp-rent-ca-net-free
SQL_SERVER=sql-rent-ca-net
SQL_DB=rentcanet
SQL_ADMIN_USER=rentadmin
SQL_ADMIN_PASSWORD="<pick-a-strong-password>"
STORAGE=strentcanet$(date +%s | tail -c 5)   # must be globally unique

# 1) Resource group + App Service plan (F1 free)
az group create -n $RG -l $LOCATION
az appservice plan create -g $RG -n $PLAN --sku F1 --is-linux

# 2) App Service, .NET 9 on Linux
az webapp create -g $RG -p $PLAN -n $APP --runtime "DOTNETCORE:9.0"

# 3) Azure SQL Server + Database on the Free Offer
az sql server create -g $RG -n $SQL_SERVER -l $LOCATION \
  -u $SQL_ADMIN_USER -p "$SQL_ADMIN_PASSWORD"
az sql server firewall-rule create -g $RG -s $SQL_SERVER \
  -n "AllowAzureServices" --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
az sql db create -g $RG -s $SQL_SERVER -n $SQL_DB \
  --service-objective GP_S_Gen5_2 --compute-model Serverless \
  --use-free-limit --free-limit-exhaustion-behavior AutoPause

# 4) Storage account + container
az storage account create -g $RG -n $STORAGE -l $LOCATION --sku Standard_LRS
STORAGE_KEY=$(az storage account keys list -g $RG -n $STORAGE --query [0].value -o tsv)
az storage container create -n property-images --account-name $STORAGE \
  --account-key $STORAGE_KEY --public-access blob

# 5) Gather connection strings
SQL_CS="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=$SQL_DB;User ID=$SQL_ADMIN_USER;Password=$SQL_ADMIN_PASSWORD;Encrypt=True;TrustServerCertificate=False;"
STORAGE_CS="DefaultEndpointsProtocol=https;AccountName=$STORAGE;AccountKey=$STORAGE_KEY;EndpointSuffix=core.windows.net"

# 6) Set App Service app settings
az webapp config appsettings set -g $RG -n $APP --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__DefaultConnection="$SQL_CS" \
  ConnectionStrings__AzureStorage="$STORAGE_CS" \
  ImageStorage__Provider=AzureBlob \
  ImageStorage__ContainerName=property-images

# 7) Download the publish profile (needed for the GitHub Actions secret)
az webapp deployment list-publishing-profiles -g $RG -n $APP --xml > publish-profile.xml
```

### GitHub Actions secrets

In your fork's **Settings &rarr; Secrets and variables &rarr; Actions**, add:

| Secret name | Value |
| --- | --- |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | The full XML contents of `publish-profile.xml` from step 7. |
| `AZURE_SQL_CONNECTION_STRING` | Same value as `$SQL_CS` above &mdash; used by the `dotnet ef database update` step of the pipeline. |

Push to `main` (or trigger **Deploy to Azure App Service** manually from the Actions tab). The workflow builds, tests, applies EF Core migrations against Azure SQL, and deploys the published output to App Service.

### Limitations (documented for honesty)

* Cold start ~30 s after inactivity &mdash; F1 does not support "Always On".
* If daily CPU exceeds 60 minutes, the app pauses until the next day.
* Azure SQL auto-pause after 1 h idle means the first request after idle can take ~20 s while the database wakes.
* F1 does not allow custom domains &mdash; the app lives at `{app-name}.azurewebsites.net`.
* Everything above is acceptable for a portfolio demo; a paying production workload should start at B1 + a dedicated Azure SQL tier.

## Why this repo exists

I'm building out my portfolio as a .NET developer. The [Next.js version](https://rent-ca.vercel.app/en) showcases the product; this .NET version showcases the stack I actually work in day-to-day. Same product, two implementations, two deployments &mdash; both public, both live.

&mdash; Giovanni Richaud
