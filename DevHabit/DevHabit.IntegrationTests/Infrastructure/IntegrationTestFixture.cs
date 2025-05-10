using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WireMock.Server;

namespace DevHabit.IntegrationTests.Infrastructure;

// [Collection(nameof(IntegrationTestCollection))]
public abstract class IntegrationTestFixture(DevHabitWebAppFactory factory) : IClassFixture<DevHabitWebAppFactory>
{
    private HttpClient? _authorizedClient;
    public HttpClient CreateClient() => factory.CreateClient();
    public WireMockServer WireMockServer => factory.GetWireMockServer();


    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "test@test.com",
        string password = "Password123!",
        bool forceNewClient = false)
    {
        if (_authorizedClient is not null && !forceNewClient)
        {
            return _authorizedClient;
        }

        HttpClient client = CreateClient();

        bool userExists = false;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            using ApplicationDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            userExists = await dbContext.Users.AnyAsync(u => u.Email == email);
        }

        if (!userExists)
        {
            HttpResponseMessage registerResponse = await client.PostAsJsonAsync(Routes.Auth.Register, new RegisterUserDto
            {
                Name = email,
                Email = email,
                Password = password,
                ConfirmPassword = password
            });

            registerResponse.EnsureSuccessStatusCode();
        }

        HttpResponseMessage loginResponse = await client.PostAsJsonAsync(Routes.Auth.Login, new LoginUserDto
            {
                Email = email,
                Password = password
            });

        loginResponse.EnsureSuccessStatusCode();

        AccessTokenDto? accessTokenDto = await loginResponse.Content.ReadFromJsonAsync<AccessTokenDto>();
        
        if (accessTokenDto is null)
        {
            throw new InvalidOperationException("Failed to retrieve access token.");
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessTokenDto.AccessToken);

        if (!forceNewClient)
        {
            _authorizedClient = client;
        }

        return client;
    }

    public async Task CleanupDatabaseAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IConfiguration configuration = scope.ServiceProvider
            .GetRequiredService<IConfiguration>();

        string? connectionString = configuration.GetConnectionString("Database");
        if (connectionString is null)
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }

        await using NpgsqlConnection connection = new (connectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = new(@"
            DO $$
            BEGIN
                -- Truncate application tables
                TRUNCATE TABLE dev_habit.entries CASCADE;
                TRUNCATE TABLE dev_habit.entry_import_jobs CASCADE;
                TRUNCATE TABLE dev_habit.tags CASCADE;
                TRUNCATE TABLE dev_habit.habits CASCADE;
                TRUNCATE TABLE dev_habit.users CASCADE;

                -- Truncate identity tables
                TRUNCATE TABLE identity.asp_net_users CASCADE;
                TRUNCATE TABLE identity.refresh_tokens CASCADE;
            END $$;", connection);

        await command.ExecuteNonQueryAsync();
    }
}
