using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using catalog.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault
var keyVaultUrl = builder.Configuration["KeyVault:BaseUrl"];
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
    builder.Configuration.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());
}

// CHANGE THIS TO false WHEN MIGRATING TO AZURE SQL
bool useInMemory = false;

// Add services to the container.
builder.Services.AddRazorPages();

if (useInMemory)
{
    builder.Services.AddDbContext<BookContext>(options => 
        options.UseInMemoryDatabase(databaseName: "test"));
}
else
{
    builder.Services.AddDbContext<BookContext>(options => 
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("BooksDB"),
            sqlServerOptionsAction: sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            }));
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseDeveloperExceptionPage();

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
