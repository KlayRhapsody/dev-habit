
using DevHabit.Api;
using DevHabit.Api.Extensions;
using DevHabit.Api.Middleware;
using DevHabit.Api.Settings;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder
    .AddApiServices()
    .AddErrorHandling()
    .AddDatabase()
    .AddObservability()
    .AddApplicationServices()
    .AddAuthenticationServices()
    .AddCorsPolicy()
    .AddBackgroundJobs()
    .AddRateLimiting();
    
WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await app.ApplyMigrationsAsync();

    await app.SeedInitialDataAsync();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseCors(CorsOptions.PolicyName);

app.UseResponseCaching();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.UseMiddleware<ETagMiddleware>();

app.MapControllers();

await app.RunAsync();

public partial class Program;
