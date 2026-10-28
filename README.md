# Readit – Catalog Service

The **Catalog** service of the *Readit* online bookstore, built while following an Azure cloud course.
It is an ASP.NET Core Razor Pages app that lists books from **Azure SQL Database**, tracks the shopping cart in **Azure Cache for Redis**, and can read its secrets from **Azure Key Vault**.

> The companion **Cart** service lives in a separate repository.

---

## Table of contents

- [Architecture](#architecture)
- [Tech stack](#tech-stack)
- [Project structure](#project-structure)
- [Data model](#data-model)
- [Pages and handlers](#pages-and-handlers)
- [Configuration](#configuration)
- [Running locally](#running-locally)
- [Database migrations](#database-migrations)
- [Deploying to Azure](#deploying-to-azure)
- [What I learned](#what-i-learned)

---

## Architecture

```text
              ┌──────────────────────────┐
  Browser ───▶│  Catalog (Razor Pages)   │
              │  Azure App Service       │
              └────┬──────────┬──────────┘
                   │          │          (optional)
         EF Core   │          │ ServiceStack.Redis
                   ▼          ▼                    ┌──────────────────┐
         ┌──────────────┐ ┌──────────────────┐     │ Azure Key Vault  │
         │ Azure SQL DB │ │ Azure Cache for  │     │ (secrets loaded  │
         │ (Books)      │ │ Redis ("cart")   │     │  at startup)     │
         └──────────────┘ └──────────────────┘     └──────────────────┘
```

- **Books** (name, author, price, stock…) are stored in Azure SQL and accessed with Entity Framework Core.
- **Shopping cart** is a Redis list called `cart` that holds the IDs of the books that were added.
- **Secrets** (connection strings) can be pulled from Key Vault with `DefaultAzureCredential`, so they don't have to live in `appsettings.json`.

## Tech stack

| Area | Technology |
|------|------------|
| Framework | .NET 10, ASP.NET Core Razor Pages |
| ORM | Entity Framework Core 10 (SQL Server, InMemory, SQLite providers) |
| Database | Azure SQL Database |
| Cache | Azure Cache for Redis via `ServiceStack.Redis` |
| Secrets | Azure Key Vault (`Azure.Identity`, `Azure.Extensions.AspNetCore.Configuration.Secrets`) |
| UI | Bootstrap, jQuery (in `wwwroot/lib`) |

## Project structure

```text
catalog/
├── Program.cs              # App entry point: Key Vault, DbContext, Razor Pages pipeline
├── Startup.cs              # Older Startup-style config (kept for reference, not used by Program.cs)
├── BookLoader.cs           # Seeds the database with sample books
├── Data/
│   └── BookContext.cs      # EF Core DbContext (DbSet<Book> Books)
├── Models/
│   └── Book.cs             # Book entity
├── Migrations/             # EF Core migrations (initmigrate)
├── Pages/
│   ├── Index.cshtml(.cs)   # Book list, "Add to cart", "Load Books to DB"
│   ├── Weather.cshtml(.cs) # Calls an external weather API by IP
│   └── Shared/_Layout.cshtml
├── wwwroot/                # Static files (css, js, images, lib)
├── appsettings.json        # Configuration (see below — do not commit real secrets!)
└── connect_azure_sql.txt   # Notes: commands for EF migrations against Azure SQL
```

## Data model

`Models/Book.cs`

| Property | Type | Description |
|----------|------|-------------|
| `ID` | `int` | Primary key |
| `Name` | `string` | Book title |
| `Author` | `string` | Author name |
| `Pages` | `int` | Number of pages |
| `ImageUrl` | `string` | Cover image file name |
| `Price` | `double` | Price |
| `InStock` | `int` | Copies in stock |

## Pages and handlers

### `/` – Index

| Handler | Method | What it does |
|---------|--------|--------------|
| `OnGet` | GET | Reads all books from SQL and the number of items in the Redis `cart` list. Shows an error message if the DB can't be reached. |
| `OnPostAddToShoppingCart` | POST | If the book isn't already in the cart: decrements `InStock` in SQL and pushes the book ID to the Redis `cart` list. |
| `OnPostLoad` | POST | Runs `BookLoader.LoadBooks` — deletes all books and inserts 4 sample books. |

### `/Weather`

| Handler | Method | What it does |
|---------|--------|--------------|
| `OnPostWeather` | POST | Sends `GET http://{ip}/api/weather` to the IP entered in the form and displays the response (used to test calling a service running on a VM / container). |

## Configuration

Settings are read from `appsettings.json`, environment variables, and (optionally) Azure Key Vault.

| Key | Description |
|-----|-------------|
| `ConnectionStrings:BooksDB` | Azure SQL connection string |
| `Redis:ConnectionString` | Redis connection string (`host:port?password=...&ssl=True&abortConnect=False`) |
| `KeyVault:BaseUrl` | Key Vault URL. When set, Key Vault secrets are added to the configuration at startup |

Example `appsettings.json` (placeholders only):

```json
{
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "BooksDB": "Server=tcp:<your-server>.database.windows.net,1433;Initial Catalog=<your-db>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Redis": {
    "ConnectionString": "<your-cache>.redis.cache.windows.net:6380?password=<key>&ssl=True&abortConnect=False"
  },
  "KeyVault": {
    "BaseUrl": "https://<your-key-vault>.vault.azure.net/"
  }
}
```

A copy of this template is in [`appsettings.example.json`](appsettings.example.json).

### Using Key Vault for secrets

Key Vault secret names map to configuration keys by replacing `:` with `--`:

| Configuration key | Key Vault secret name |
|-------------------|-----------------------|
| `ConnectionStrings:BooksDB` | `ConnectionStrings--BooksDB` |
| `Redis:ConnectionString` | `Redis--ConnectionString` |

Give the App Service a **system-assigned managed identity** and grant it *Get/List* on secrets (or the **Key Vault Secrets User** role). Locally, `DefaultAzureCredential` uses your `az login` / Visual Studio account.

### In-memory database

To run without Azure SQL, set this flag in `Program.cs`:

```csharp
bool useInMemory = true;
```

> Redis is still used by the Index page, so you'll also need a reachable Redis instance (or comment out the Redis calls).

## Running locally

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), access to an Azure SQL database and a Redis cache (or the in-memory option above).

```bash
git clone https://github.com/<your-username>/<repo-name>.git
cd <repo-name>
cp appsettings.example.json appsettings.json   # then fill in your values
dotnet restore
dotnet run
```

Open <https://localhost:5001> (or <http://localhost:5000>), then click **Load Books to DB** to seed the sample data.

> If Azure SQL rejects the connection, add your client IP to the SQL server's firewall rules in the Azure portal.

## Database migrations

```bash
# Install the EF Core CLI (once)
dotnet tool install --global dotnet-ef

# Create a migration
dotnet ef migrations add -c BookContext InitialCreate

# Apply migrations to the database in ConnectionStrings:BooksDB
dotnet ef database update
```

The repository already contains the `initmigrate` migration, so on a fresh database only `dotnet ef database update` is needed.

## Deploying to Azure

1. **Create the resources:** Resource group, Azure SQL server + database, Azure Cache for Redis, App Service (Windows or Linux, .NET 10), and optionally a Key Vault.
2. **Apply the schema:** run `dotnet ef database update` against the Azure SQL database.
3. **Publish the app:**
   ```bash
   dotnet publish -c Release -o ./publish
   ```
   Then deploy the `publish` folder with Visual Studio (*Publish → Azure App Service*), VS Code's Azure extension, or:
   ```bash
   az webapp deploy --resource-group <rg> --name <app-name> --src-path publish.zip --type zip
   ```
4. **Configure secrets:** set the connection strings in *App Service → Configuration*, or store them in Key Vault and set `KeyVault:BaseUrl` with the App Service's managed identity granted access.
5. **Allow access:** on the SQL server enable *Allow Azure services and resources to access this server*.

## What I learned

- Building a Razor Pages app with EF Core and code-first migrations
- Moving from an in-memory database to **Azure SQL Database**
- Using **Azure Cache for Redis** to share state (the cart) between services
- Keeping secrets out of code with **Azure Key Vault** and **Managed Identity**
- Deploying a .NET app to **Azure App Service**
