using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vendor.Application;
using Vendor.Infrastructure;

namespace Vendor.Api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApiLayerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register Application and Infrastructure layers
        services.AddApplication();
        services.AddInfrastructure(configuration);

        // Response Compression
        services.AddResponseCompression();

        // Authentication & Authorization
        var secretKey = configuration["Jwt:SecretKey"] ?? "super-secret-jwt-key-minimum-32-characters-long!";
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey.Length >= 32 ? secretKey : secretKey.PadRight(32, '0')))
                };
            });
        services.AddAuthorization();

        // API Versioning
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        // 4 Named Rate Limiting Policies
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("auth", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 10;
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("catalog", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 300;
                opt.QueueLimit = 10;
            });

            options.AddFixedWindowLimiter("webhook", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 50;
                opt.QueueLimit = 5;
            });

            options.AddFixedWindowLimiter("default", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 100;
                opt.QueueLimit = 10;
            });
        });

        // CORS configuration with AllowCredentials for SignalR
        services.AddCors(options =>
        {
            options.AddPolicy("VendorCorsPolicy", builder =>
            {
                builder.SetIsOriginAllowed(_ => true)
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials();
            });
        });

        // Swagger / OpenAPI
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // SignalR & Health Checks
        services.AddSignalR();
        services.AddHealthChecks();

        return services;
    }
}
