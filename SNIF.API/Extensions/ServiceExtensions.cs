// SNIF.API/Extensions/ServiceExtensions.cs
using AutoMapper.EquivalencyExpression;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SNIF.Application.Services;
using SNIF.Busniess.Services;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Mappings;
using SNIF.Infrastructure.Data;
using SNIF.Infrastructure.Repository;
using SNIF.Infrastructure.Services;
using System.Text;
using System.Text.Json.Serialization;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {

        // Add AutoMapper
        services.AddAutoMapper(cfg =>
        {
            cfg.AddCollectionMappers();
        }, typeof(LocationMappingProfile).Assembly,
           typeof(PetMappingProfile).Assembly,
           typeof(MatchMappingProfile).Assembly);

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        // Core services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<IPetService, PetService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // API Explorer for Swagger
        services.AddEndpointsApiExplorer();

        return services;
    }

    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SNIF API",
                Version = "v1",
                Description = "Social Network for Furry Friends API"
            });

            // Add JWT Authentication
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement()
            {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    },
                    Scheme = "oauth2",
                    Name = "Bearer",
                    In = ParameterLocation.Header,
                },
                new List<string>()
            }
            });
        });

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddIdentityCore<User>(opt =>
        {
            opt.Password.RequireDigit = true;
            opt.Password.RequireLowercase = true;
            opt.Password.RequireUppercase = true;
            opt.Password.RequireNonAlphanumeric = false;
            opt.Password.RequiredLength = 8;
            opt.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<SNIFContext>()
        .AddSignInManager<SignInManager<User>>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(config["Jwt:Key"]!))
                };
            });

        return services;
    }
}