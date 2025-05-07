using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Trace;

namespace DevHabit.IntegrationTests.Tests;

public sealed class HabitsTests(DevHabitWebAppFactory factory)
    : IntegrationTestFixture(factory)
{
    [Fact]
    public async Task CreateHabit_ShouldSucceed_WithValidParameters()
    {
        var dto = new CreateHabitDto
        {
            Name = "Read Books",
            Description = "Read technical books to improve skills",
            Type = HabitType.Measurable,
            Frequency = new FrequencyDto
            {
                Type = FrequencyType.Daily,
                TimesPerPeriod = 1
            },
            Target = new TargetDto
            {
                Value = 30,
                Unit = "pages"
            }
        };

        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(Routes.Habits.Create, dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<HabitDto>());
    }
}
