using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevHabit.IntegrationTests.Infrastructure;

// [Collection(nameof(IntegrationTestCollection))]
public abstract class IntegrationTestFixture(DevHabitWebAppFactory factory) : IClassFixture<DevHabitWebAppFactory>
{
    public HttpClient CreateClient() => factory.CreateClient();

    private HttpClient? _authorizedClient;

    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "test@test.com",
        string password = "Password123!")
    {
        if (_authorizedClient is not null)
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

        _authorizedClient = client;

        return client;
    }
}
