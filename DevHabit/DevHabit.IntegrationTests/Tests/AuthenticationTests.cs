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

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenEmailAlreadyExists()
    {
        var dto = new RegisterUserDto
        {
            Name = "test2@example.com",
            Email = "test2@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        HttpClient client = CreateClient();

        await client.PostAsJsonAsync(Routes.Auth.Register, dto);

        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Auth.Register, dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "test@example.com", "Password123!", "Password123!")]
    [InlineData("test@example.com", "", "Password123!", "Password123!")]
    [InlineData("test@example.com", "invalid-email", "Password123!", "Password123!")]
    [InlineData("test@example.com", "test@example.com", "", "Password123!")]
    [InlineData("test@example.com", "test@example.com", "Password123!", "")]
    [InlineData("test@example.com", "test@example.com", "Password123!", "DifferentPassword123!")]
    [InlineData("test@example.com", "test@example.com", "test", "test")]
    public async Task Register_ShouldFail_WithInvalidParameters(
        string name,
        string email,
        string password,
        string confirmPassword)
    {
        var dto = new RegisterUserDto
        {
            Name = name,
            Email = email,
            Password = password,
            ConfirmPassword = confirmPassword
        };

        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Auth.Register, dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldSucceed_WithValidParameters()
    {
        string email = "login@example.com";
        string password = "Password123!";

        var registerDto = new RegisterUserDto
        {
            Name = email,
            Email = email,
            Password = password,
            ConfirmPassword = password
        };

        HttpClient client = CreateClient();

        HttpResponseMessage registerResponse = await client.PostAsJsonAsync(Routes.Auth.Register, registerDto);
        registerResponse.EnsureSuccessStatusCode();

        var loginDto = new LoginUserDto
        {
            Email = email,
            Password = password
        };
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync(Routes.Auth.Login, loginDto);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        AccessTokenDto? accessTokenDto = await loginResponse.Content.ReadFromJsonAsync<AccessTokenDto>();
        Assert.NotNull(accessTokenDto);
        Assert.NotNull(accessTokenDto.AccessToken);
        Assert.NotNull(accessTokenDto.RefreshToken);
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData("invalid-email", "Password123!")]
    [InlineData("test@example.com", "")]
    public async Task Login_ShouldFail_WithInvalidParameters(string email, string password)
    {
        var dto = new LoginUserDto
        {
            Email = email,
            Password = password
        };

        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Auth.Login, dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ShouldSucceed_WithValidToken()
    {
        await CleanupDatabaseAsync();

        string email = "refresh@example.com";
        string password = "Password123!";

        var registerDto = new RegisterUserDto
        {
            Name = email,
            Email = email,
            Password = password,
            ConfirmPassword = password
        };

        HttpClient client = CreateClient();

        HttpResponseMessage registerResponse = await client.PostAsJsonAsync(Routes.Auth.Register, registerDto);
        registerResponse.EnsureSuccessStatusCode();

        AccessTokenDto? initialTokens = await registerResponse.Content.ReadFromJsonAsync<AccessTokenDto>();
        Assert.NotNull(initialTokens);

        var refreshTokenDto = new RefreshTokenDto
        {
            RefreshToken = initialTokens.RefreshToken
        };

        HttpResponseMessage refreshResponse = await client.PostAsJsonAsync(Routes.Auth.Refresh, refreshTokenDto);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        AccessTokenDto? newTokens = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenDto>();
        Assert.NotNull(newTokens);
        Assert.NotNull(newTokens.AccessToken);
        Assert.NotNull(newTokens.RefreshToken);
        Assert.NotEqual(initialTokens.AccessToken, newTokens.AccessToken);
        Assert.NotEqual(initialTokens.RefreshToken, newTokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ShouldFail_WithInvalidToken()
    {
        // Arrange
        var refreshDto = new RefreshTokenDto
        {
            RefreshToken = "invalid-refresh-token"
        };
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Auth.Refresh, refreshDto);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refresh_ShouldFail_WithInvalidParameters(string refreshToken)
    {
        // Arrange
        var refreshDto = new RefreshTokenDto
        {
            RefreshToken = refreshToken
        };
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Auth.Refresh, refreshDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ShouldIssueNewTokens_WithValidToken()
    {
        // Arrange
        await CleanupDatabaseAsync();

        const string email = "refresh2@test.com";
        const string password = "Test123!";

        // Register and get initial tokens
        var registerDto = new RegisterUserDto
        {
            Name = email,
            Email = email,
            Password = password,
            ConfirmPassword = password
        };
        HttpClient client = CreateClient();
        HttpResponseMessage registerResponse = await client.PostAsJsonAsync(Routes.Auth.Register, registerDto);
        AccessTokenDto? initialTokens = await registerResponse.Content.ReadFromJsonAsync<AccessTokenDto>();
        Assert.NotNull(initialTokens);

        // First refresh
        var firstRefreshDto = new RefreshTokenDto
        {
            RefreshToken = initialTokens.RefreshToken
        };
        HttpResponseMessage firstRefreshResponse = await client.PostAsJsonAsync(Routes.Auth.Refresh, firstRefreshDto);
        AccessTokenDto? firstRefreshTokens = await firstRefreshResponse.Content
            .ReadFromJsonAsync<AccessTokenDto>();
        Assert.NotNull(firstRefreshTokens);

        // Second refresh with new token
        var secondRefreshDto = new RefreshTokenDto
        {
            RefreshToken = firstRefreshTokens.RefreshToken
        };

        // Act
        HttpResponseMessage secondRefreshResponse = await client
            .PostAsJsonAsync(Routes.Auth.Refresh, secondRefreshDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, secondRefreshResponse.StatusCode);
        AccessTokenDto? finalTokens = await secondRefreshResponse.Content.ReadFromJsonAsync<AccessTokenDto>();
        Assert.NotNull(finalTokens);
        Assert.NotEqual(initialTokens.AccessToken, finalTokens.AccessToken);
        Assert.NotEqual(initialTokens.RefreshToken, finalTokens.RefreshToken);
        Assert.NotEqual(firstRefreshTokens.AccessToken, finalTokens.AccessToken);
        Assert.NotEqual(firstRefreshTokens.RefreshToken, finalTokens.RefreshToken);
    }
}
