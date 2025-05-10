using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.IntegrationTests.Infrastructure;

namespace DevHabit.IntegrationTests.Tests;

public sealed class CrossCuttingTests(DevHabitWebAppFactory factory)
    : IntegrationTestFixture(factory)
{
    public static TheoryData<string> ProtectedEndpoints =
    [
        Routes.Habits.GetAll,
        Routes.Entries.GetAll,
        Routes.Tags.GetAll,
        Routes.Github.GetProfile,
        Routes.EntryImports.GetAll
    ];

    public static TheoryData<string> MediaTypes =
    [
        MediaTypeNames.Application.Json,
        CustomMediaTypeNames.Application.HateoasJson
    ];

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task Endpoints_ShouldRequireAuthentication(string route)
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.First().Scheme);
    }

    [Fact]
    public async Task Endpoints_ShouldEnforceResourceOwnership()
    {
        HttpClient client1 = await CreateAuthenticatedClientAsync(email: "client1@example.com", forceNewClient: true);
        HttpClient client2 = await CreateAuthenticatedClientAsync(email: "client2@example.com", forceNewClient: true);

        CreateHabitDto createHabitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage response = await client1.PostAsJsonAsync(Routes.Habits.Create, createHabitDto);
        HabitDto? habitDto = await response.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        HttpResponseMessage response2 = await client2.GetAsync(Routes.Habits.GetById(habitDto.Id));
        Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
    }

    [Theory]
    [MemberData(nameof(MediaTypes))]
    public async Task Api_ShouldSupportContentNegotiation(string mediaType)
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));

        HttpResponseMessage response = await client.GetAsync(Routes.Habits.GetAll);
        
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Api_ShouldReturnProblemDetails_OnError()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        var invaildDto = new CreateHabitDto
        {
            Name = string.Empty, // Invalid - name is required
            Type = HabitType.Measurable,
            Frequency = new FrequencyDto
            {
                Type = FrequencyType.Daily,
                TimesPerPeriod = 1
            },
            Target = new TargetDto
            {
                Value = 1,
                Unit = "tasks"
            }
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Habits.Create, invaildDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.ProblemJson, response.Content.Headers.ContentType?.MediaType);

        Dictionary<string, object>? problemDetails = await response.Content
            .ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.ContainsKey("type"));
        Assert.True(problemDetails.ContainsKey("title"));
        Assert.True(problemDetails.ContainsKey("status"));
        Assert.True(problemDetails.ContainsKey("requestId"));
    }
}
