using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DevHabit.Api.Database;

public sealed class ApplicationIdentityDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationIdentityDbContext>
{
    public ApplicationIdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationIdentityDbContext>();

        optionsBuilder
            .UseNpgsql(
                "CONNECTION_STRING",
                npgsqlOptions => npgsqlOptions
                    .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema.Identity))
                    .UseSnakeCaseNamingConvention();

        return new ApplicationIdentityDbContext(optionsBuilder.Options);
    }
}
