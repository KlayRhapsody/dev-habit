using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Auth;
using DevHabit.IntegrationTests.Infrastructure;

namespace DevHabit.IntegrationTests.Tests;

public sealed class AuthenticationTests(DevHabitWebAppFactory factory)
    : IntegrationTestFixture(factory)
{
    [Fact]
    public async Task Register_ShouldSucceed_WithValidParameters()
    {
        var dto = new RegisterUserDto
        {
            Name = "test@example.com",
            Email = "test@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Auth.Register, dto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldReturnAccessTokens_WithValidParameters()
    {
        var dto = new RegisterUserDto
        {
            Name = "test1@example.com",
            Email = "test1@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Auth.Register, dto);

        response.EnsureSuccessStatusCode();

        AccessTokenDto? accessTokenDto = await response.Content.ReadFromJsonAsync<AccessTokenDto>();
        Assert.NotNull(accessTokenDto);
    }
}
