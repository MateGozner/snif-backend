using Microsoft.EntityFrameworkCore;
using SNIF.Infrastructure.Data;
using SNIF.SignalR.Hubs;
using System.Collections;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSwaggerServices();
builder.Services.AddIdentityServices(builder.Configuration);

// Static files will use the default web root. We'll ensure required directories exist after building the app.

// Configure PostgreSQL with fallback to App Service connection string env vars
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
    {
        var key = envVar.Key?.ToString() ?? string.Empty;
        if (key.StartsWith("POSTGRESQLCONNSTR_", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = envVar.Value?.ToString();
            break;
        }
        if (connectionString is null && key.StartsWith("CUSTOMCONNSTR_", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback for custom connection strings if provider uses CUSTOMCONNSTR_
            connectionString = envVar.Value?.ToString();
        }
    }
}

builder.Services.AddDbContext<SNIFContext>(options => options.UseNpgsql(connectionString));

// Add CORS - Configurable policy via configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                            ?? new[] { "http://localhost:3000" };

        policyBuilder
            .SetIsOriginAllowed(origin => {
                var uri = new Uri(origin);
                return uri.Host == "localhost" || uri.Host == "127.0.0.1" || allowedOrigins.Contains(origin);
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Authorization");
    });
});

var app = builder.Build();

// Configure middleware pipeline
var swaggerEnabled = app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure upload directories exist under the resolved web root
var env = app.Environment;
var resolvedWebRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(Path.Combine(resolvedWebRoot, "uploads", "profiles"));
Directory.CreateDirectory(Path.Combine(resolvedWebRoot, "uploads", "pets", "photos"));
Directory.CreateDirectory(Path.Combine(resolvedWebRoot, "uploads", "pets", "videos"));

// Apply database migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SNIFContext>();
    context.Database.Migrate();

    // Seed database with test data if empty
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
}

// The order of middleware is important
app.UseRouting();

// CORS must be between UseRouting and UseEndpoints
app.UseCors("CorsPolicy");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

// Use endpoints after all middleware
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<MatchHub>("/matchHub");
    endpoints.MapHub<OnlineHub>("/onlineHub").RequireAuthorization();
    endpoints.MapHub<VideoHub>("/videoHub").RequireAuthorization();
    endpoints.MapHub<ChatHub>("/chatHub").RequireAuthorization();
});

app.Run();