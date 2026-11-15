using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BankarskiSistem.API.BackgroundJobs;
using BankarskiSistem.API.Health;
using BankarskiSistem.API.Middleware;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Security;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace BankarskiSistem.API.Configuration
{
    public class RateLimitSettings
    {
        public const string SectionName = "RateLimiting";

        public bool Enabled { get; set; } = true;
        public int LoginPermitLimit { get; set; } = 10;
        public int PaymentsPermitLimit { get; set; } = 30;
        public int SensitivePermitLimit { get; set; } = 10;
        public int SubmissionsPermitLimit { get; set; } = 10;
    }

    public static class ServiceCollectionExtensions
    {
        public const string FrontendCorsPolicy = "Frontend";

        public static IServiceCollection AddApiPresentation(this IServiceCollection services)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .SelectMany(entry => entry.Value!.Errors)
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "Neispravan format podataka."
                            : error.ErrorMessage)
                        .Distinct()
                        .ToList();

                    var problem = ProblemWriter.Create(
                        context.HttpContext,
                        StatusCodes.Status400BadRequest,
                        "validation",
                        "Podaci nisu ispravni",
                        errors.FirstOrDefault() ?? "Validacija nije uspela.",
                        errors);

                    return new BadRequestObjectResult(problem) { ContentTypes = { ProblemWriter.ContentType } };
                };
            });

            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();
            services.AddHttpContextAccessor();
            services.AddScoped<IRequestContext, HttpRequestContext>();
            services.AddDataProtection().SetApplicationName("BankarskiSistem");
            services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("mysql", tags: [DatabaseHealthCheck.ReadyTag]);

            services.AddOptions<ScheduledPaymentsOptions>().BindConfiguration(ScheduledPaymentsOptions.SectionName);
            services.AddHostedService<ScheduledPaymentsWorker>();
            services.AddOptions<StandingOrdersOptions>().BindConfiguration(StandingOrdersOptions.SectionName);
            services.AddHostedService<StandingOrdersWorker>();
            services.AddOptions<DataRetentionOptions>().BindConfiguration(DataRetentionOptions.SectionName);
            services.AddHostedService<DataRetentionWorker>();
            services.AddOptions<SupportContactOptions>().BindConfiguration(SupportContactOptions.SectionName);

            return services;
        }

        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
            var signingKey = JwtTokenService.CreateSigningKey(jwtSettings);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidAudience = jwtSettings.Audience,
                        IssuerSigningKey = signingKey,
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = ValidateSessionAsync
                    };
                });

            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build())
                .AddPolicy(Policies.Administrator, policy => policy.RequireRole("Administrator"))
                .AddPolicy(Policies.Banker, policy => policy.RequireRole("Bankar"))
                .AddPolicy(Policies.Customer, policy => policy.RequireRole("Korisnik"))
                .AddPolicy(Policies.Staff, policy => policy.RequireRole("Bankar", "Administrator"));

            return services;
        }

        private static async Task ValidateSessionAsync(TokenValidatedContext context)
        {
            var principal = context.Principal;
            var userId = principal?.FindUserId();
            var role = principal?.FindFirstValue(ClaimTypes.Role);
            var versionClaim = principal?.FindFirstValue(JwtTokenService.TokenVersionClaim);
            var sessionId = principal?.FindSessionId();

            if (userId == null || role == null || sessionId == null || !int.TryParse(versionClaim, out var tokenVersion))
            {
                context.Fail("Token ne sadrži potrebne podatke.");
                return;
            }

            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
            if (!await authService.IsSessionValidAsync(userId.Value, tokenVersion, role, sessionId.Value))
                context.Fail("Sesija više nije važeća.");
        }

        public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, cancellationToken) =>
                    new ValueTask(ProblemWriter.WriteAsync(
                        context.HttpContext,
                        StatusCodes.Status429TooManyRequests,
                        "too-many-requests",
                        "Previše zahteva",
                        "Previše zahteva u kratkom roku. Sačekajte trenutak pa pokušajte ponovo.",
                        [],
                        cancellationToken));

                options.AddPolicy(RateLimitPolicies.Login, httpContext =>
                    FixedWindow(settings, ClientAddress(httpContext), settings.LoginPermitLimit, TimeSpan.FromMinutes(1)));

                options.AddPolicy(RateLimitPolicies.Sensitive, httpContext =>
                    FixedWindow(settings, UserOrClient(httpContext), settings.SensitivePermitLimit, TimeSpan.FromMinutes(5)));

                options.AddPolicy(RateLimitPolicies.Submissions, httpContext =>
                    FixedWindow(settings, UserOrClient(httpContext), settings.SubmissionsPermitLimit, TimeSpan.FromMinutes(10)));

                options.AddPolicy(RateLimitPolicies.Payments, httpContext =>
                    settings.Enabled
                        ? RateLimitPartition.GetSlidingWindowLimiter(
                            UserOrClient(httpContext),
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = settings.PaymentsPermitLimit,
                                Window = TimeSpan.FromMinutes(1),
                                SegmentsPerWindow = 6,
                                QueueLimit = 0
                            })
                        : RateLimitPartition.GetNoLimiter(UserOrClient(httpContext)));
            });

            return services;
        }

        private static RateLimitPartition<string> FixedWindow(RateLimitSettings settings, string key, int permitLimit, TimeSpan window) =>
            settings.Enabled
                ? RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0
                })
                : RateLimitPartition.GetNoLimiter(key);

        private static string ClientAddress(HttpContext httpContext) =>
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private static string UserOrClient(HttpContext httpContext) =>
            httpContext.User.FindUserId()?.ToString() ?? ClientAddress(httpContext);

        public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            services.AddCors(options =>
                options.AddPolicy(FrontendCorsPolicy, policy =>
                    policy.WithOrigins(allowedOrigins)
                          .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", RefreshTokenCookie.CsrfHeader)
                          .WithMethods("GET", "POST", "PUT", "DELETE")
                          .AllowCredentials()));

            return services;
        }

        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Bankarski Sistem API", Version = "v1" });
                options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT token (bez prefiksa Bearer).",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            return services;
        }
    }
}
