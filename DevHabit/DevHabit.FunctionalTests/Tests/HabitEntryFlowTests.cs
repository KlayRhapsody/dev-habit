using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.DTOs.Habits;
using DevHabit.FunctionalTests.Infrastructure;

namespace DevHabit.FunctionalTests.Tests;

public sealed class HabitEntryFlowTests(DevHabitWebAppFactory factory) : FunctionalTestFixture(factory)
{
    [Fact]
    public async Task CompleteHabitEntryFlow_ShouldSucceed()
    {
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage habit1Response = await client.PostAsJsonAsync(Routes.Habits.Create, TestData.Habit.CreateReadingHabit());
        Assert.Equal(HttpStatusCode.Created, habit1Response.StatusCode);
        HabitDto? habitDto = await habit1Response.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        HttpResponseMessage entry1Response = await client.PostAsJsonAsync(Routes.Entries.Create, TestData.Entry.CreateEntry(habitDto.Id));
        Assert.Equal(HttpStatusCode.Created, entry1Response.StatusCode);
        EntryDto? entry1Dto = await entry1Response.Content.ReadFromJsonAsync<EntryDto>();
        Assert.NotNull(entry1Dto);

        CreateEntryDto createEntryDto = TestData.Entry.CreateEntryForDate(
            habitId: habitDto.Id,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        HttpResponseMessage entry2Response = await client.PostAsJsonAsync(Routes.Entries.Create, createEntryDto);
        Assert.Equal(HttpStatusCode.Created, entry2Response.StatusCode);
        EntryDto? entry2Dto = await entry2Response.Content.ReadFromJsonAsync<EntryDto>();
        Assert.NotNull(entry2Dto);

        HttpResponseMessage entriesResponse = await client.GetAsync($"{Routes.Entries.GetAll}?habit={habitDto.Id}");
        Assert.Equal(HttpStatusCode.OK, entriesResponse.StatusCode);
        PaginationResult<EntryDto>? entriesDto = await entriesResponse.Content.ReadFromJsonAsync<PaginationResult<EntryDto>>();
        Assert.NotNull(entriesDto);
        Assert.Equal(2, entriesDto.Items.Count);

        HttpResponseMessage statsResponse = await client.GetAsync(Routes.Entries.Stats);
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        EntryStatsDto? entryStatsDto = await statsResponse.Content.ReadFromJsonAsync<EntryStatsDto>();
        Assert.NotNull(entryStatsDto);
        Assert.Equal(2, entryStatsDto.TotalEntries);
    }
}
