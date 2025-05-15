using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Github;
using DevHabit.FunctionalTests.Infrastructure;
using Newtonsoft.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace DevHabit.FunctionalTests.Tests;

public sealed class GitHubIntegrationFlowTests(DevHabitWebAppFactory factory)
    : FunctionalTestFixture(factory)
{
    private const string TestAccessToken = "gho_test123456789";

    private static readonly GitHubUserProfileDto TestUser = new(
        Login: "testuser",
        Name: "Test User",
        AvatarUrl: "https://github.com/testuser.png",
        Bio: "Test bio",
        PublicRepos: 10,
        Followers: 20,
        Following: 30
    );
    
    [Fact]
    public async Task CompleteGitHubIntegrationFlow_ShouldSucceed()
    {
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        WireMockServer
            .Given(Request.Create()
                .WithPath("/user")
                .WithHeader("Authorization", $"Bearer {TestAccessToken}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", MediaTypeNames.Application.Json)
                .WithBodyAsJson(TestUser));

        var storeGithubAccessTokenDto = new StoreGithubAccessTokenDto
        {
            AccessToken = TestAccessToken,
            ExpiresInDays = 30
        };

        HttpResponseMessage accessTokenResponse = await client.PutAsJsonAsync(
            Routes.Github.StoreAccessToken, storeGithubAccessTokenDto);
        Assert.Equal(HttpStatusCode.NoContent, accessTokenResponse.StatusCode);

        HttpResponseMessage profileResponse = await client.GetAsync(Routes.Github.GetProfile);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        GitHubUserProfileDto? gitHubUserProfileDto = JsonConvert.DeserializeObject<GitHubUserProfileDto>(
            await profileResponse.Content.ReadAsStringAsync());
        Assert.NotNull(gitHubUserProfileDto);
        Assert.Equal(TestUser.Login, gitHubUserProfileDto.Login);
        Assert.Equal(TestUser.Name, gitHubUserProfileDto.Name);
        Assert.Equal(TestUser.Bio, gitHubUserProfileDto.Bio);

        HttpResponseMessage revokeResponse = await client.DeleteAsync(Routes.Github.RevokeAccessToken);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        HttpResponseMessage response = await client.GetAsync(Routes.Github.GetProfile);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
