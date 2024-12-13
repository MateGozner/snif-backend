using Microsoft.EntityFrameworkCore;
using SNIF.Infrastructure.Data;

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

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins",
        builder => builder.WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowedOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseStaticFiles();

app.Run();