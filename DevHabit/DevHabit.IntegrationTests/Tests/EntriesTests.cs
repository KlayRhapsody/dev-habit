using System.Net;
using System.Net.Http.Json;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.DTOs.Habits;
using DevHabit.IntegrationTests.Infrastructure;

namespace DevHabit.IntegrationTests.Tests;

public class EntriesTests(DevHabitWebAppFactory factory)
    : IntegrationTestFixture(factory)
{
    [Fact]
    public async Task GetEntries_ShouldReturnEmptyList_WhenNoEntriesExist()
    {
        await CleanupDatabaseAsync();
        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.GetAsync(Routes.Entries.GetAll);

        response.EnsureSuccessStatusCode();

        PaginationResult<EntryDto>? result = await response.Content.ReadFromJsonAsync<PaginationResult<EntryDto>>();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetEntries_ShouldReturnEntries_WhenEntriesExist()
    {
        // Arrange
        await CleanupDatabaseAsync();
        HttpClient client = await CreateAuthenticatedClientAsync();
        
        // Create a habit first
        CreateHabitDto createHabitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage createHabitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, createHabitDto);
        HabitDto? habitDto = await createHabitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        // Create an entry
        CreateEntryDto createEntryDto = TestData.Entry.CreateEntry(habitDto.Id);
        await client.PostAsJsonAsync(Routes.Entries.Create, createEntryDto);

        // Act
        HttpResponseMessage response = await client.GetAsync(Routes.Entries.GetAll);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PaginationResult<EntryDto>? result = await response.Content.ReadFromJsonAsync<PaginationResult<EntryDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        Assert.Equal(createEntryDto.Value, result.Items[0].Value);
    }

    [Fact]
    public async Task GetEntries_ShouldFilterByHabitId()
    {
        // Arrange
        await CleanupDatabaseAsync();
        HttpClient client = await CreateAuthenticatedClientAsync();
        
        // Create two habits
        CreateHabitDto habit1Dto = TestData.Habit.CreateReadingHabit();
        CreateHabitDto habit2Dto = TestData.Habit.CreateExerciseHabit();

        HttpResponseMessage habit1Response = await client.PostAsJsonAsync(Routes.Habits.Create, habit1Dto);
        HttpResponseMessage habit2Response = await client.PostAsJsonAsync(Routes.Habits.Create, habit2Dto);
        
        HabitDto? habit1 = await habit1Response.Content.ReadFromJsonAsync<HabitDto>();
        HabitDto? habit2 = await habit2Response.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit1);
        Assert.NotNull(habit2);

        // Create entries for both habits
        CreateEntryDto entry1Dto = TestData.Entry.CreateEntry(habit1.Id, note: "Reading entry");
        CreateEntryDto entry2Dto = TestData.Entry.CreateEntry(habit2.Id, value: 20, note: "Exercise entry");
        await client.PostAsJsonAsync(Routes.Entries.Create, entry1Dto);
        await client.PostAsJsonAsync(Routes.Entries.Create, entry2Dto);

        // Act
        HttpResponseMessage response = await client.GetAsync($"{Routes.Entries.GetAll}?habitId={habit1.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PaginationResult<EntryDto>? result = await response.Content.ReadFromJsonAsync<PaginationResult<EntryDto>>();
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(entry1Dto.Value, result.Items[0].Value);
        Assert.Equal(entry1Dto.Note, result.Items[0].Notes);
        Assert.Equal(habit1.Id, result.Items[0].Habit.Id);
    }
}
