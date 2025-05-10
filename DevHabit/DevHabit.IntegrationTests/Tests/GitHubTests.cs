using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Github;
using DevHabit.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Formatters;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Newtonsoft.Json;

namespace DevHabit.IntegrationTests.Tests;

public sealed class GitHubTests(DevHabitWebAppFactory factory) : IntegrationTestFixture(factory)
{
    private const string TestAccessToken = "gho_test123456789";

    private static readonly GitHubUserProfileDto User = new(
        Login: "testuser",
        Name: "Test User",
        AvatarUrl: "https://github.com/testuser.png",
        Bio: "Test bio",
        PublicRepos: 10,
        Followers: 20,
        Following: 30
    );

    // private static readonly GitHubEventDto TestEvent = new(
    //     Id: "1234567890",
    //     Type: "PushEvent",
    //     Actor: new GitHubActorDto(
    //         Id: 1,
    //         Login: "testuser",
    //         DisplayLogin: "testuser",
    //         AvatarUrl: "https://github.com/testuser.png"
    //     ),
    //     Repository: new GitHubRepositoryDto(
    //         Id: 1,
    //         Name: "testuser/repo",
    //         Url: "https://api.github.com/repos/testuser/repo"
    //     ),
    //     Payload: new GitHubPayloadDto(
    //         Action: "test-action",
    //         Ref: "refs/heads/main",
    //         Commits:
    //         [
    //             new GitHubCommitDto(
    //                 Sha: "abc123",
    //                 Message: "Test commit",
    //                 Url: "https://github.com/testuser/repo/commit/abc123"
    //             )
    //         ]
    //     ),
    //     IsPublic: true,
    //     CreatedAt: DateTime.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture)
    // );

    [Fact]
    public async Task GetProfile_ShouldReturnUserProfile_WhenAccessTokenIsValid()
    {
        // Arrange
        WireMockServer
            .Given(Request.Create()
                .WithPath("/user")
                .WithHeader("Authorization", $"Bearer {TestAccessToken}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", MediaTypeNames.Application.Json)
                .WithBodyAsJson(User));

        HttpClient client = await CreateAuthenticatedClientAsync();

        var dto = new StoreGithubAccessTokenDto
        {
            AccessToken = TestAccessToken,
            ExpiresInDays = 30
        };

        Console.WriteLine($"WireMock Url: {WireMockServer.Urls[0]}");

        HttpResponseMessage updateAccessTokenResponse = await client.PutAsJsonAsync(Routes.Github.StoreAccessToken, dto);

        updateAccessTokenResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await client.GetAsync(Routes.Github.GetProfile);

        // Assert
        response.EnsureSuccessStatusCode();

        GitHubUserProfileDto? profile = JsonConvert.DeserializeObject<GitHubUserProfileDto>(
            await response.Content.ReadAsStringAsync());
        
        Assert.NotNull(profile);
        Assert.Equivalent(User, profile);
    }
}
