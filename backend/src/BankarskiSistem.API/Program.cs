using BankarskiSistem.API.Configuration;
using BankarskiSistem.API.Health;
using BankarskiSistem.API.Middleware;
using BankarskiSistem.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddApiPresentation()
    .AddJwtAuthentication(builder.Configuration)
    .AddApiRateLimiting(builder.Configuration)
    .AddFrontendCors(builder.Configuration);

if (builder.Environment.IsDevelopment())
    builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(ServiceCollectionExtensions.FrontendCorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DatabaseHealthCheck.ReadyTag)
}).AllowAnonymous();
app.MapControllers();

await app.SeedAdministratorAsync();

app.Run();

public partial class Program;
