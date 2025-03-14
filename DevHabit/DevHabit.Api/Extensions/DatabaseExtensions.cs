
using System.Security.Cryptography.X509Certificates;
using DevHabit.Api.Database;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        await using ApplicationDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await using ApplicationIdentityDbContext identityDbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationIdentityDbContext>();
        
        try
        {
            await dbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Migrations app applied successfully.");

            await identityDbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Migrations identity applied successfully.");
        } 
        catch (Exception e)
        {
            app.Logger.LogError(e, "An error occurred while applying migrations.");
            throw;
        }
    }
    

    public static async Task SeedInitialDataAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        RoleManager<IdentityRole> roleManager = 
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        try
        {
            if (!await roleManager.RoleExistsAsync(Roles.Admin))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
            }

            if (!await roleManager.RoleExistsAsync(Roles.Member))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Member));
            }

            app.Logger.LogInformation("Initial roles seeded successfully.");
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "An error occurred while seeding initial data.");
            throw;
        }
    }
}
