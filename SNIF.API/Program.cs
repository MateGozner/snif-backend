using Microsoft.EntityFrameworkCore;
using SNIF.Infrastructure.Data;
using SNIF.SignalR.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSwaggerServices();
builder.Services.AddIdentityServices(builder.Configuration);

// Configure static files
var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(webRootPath);
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "profiles"));
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "pets", "photos"));
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "pets", "videos"));
builder.WebHost.UseWebRoot(webRootPath);

// Configure PostgreSQL
builder.Services.AddDbContext<SNIFContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add CORS - Single policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Authorization")); // Add this line
});

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
});

app.Run();