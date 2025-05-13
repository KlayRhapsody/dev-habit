using System.Globalization;
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
        Assert.Equal(entry1Dto.Notes, result.Items[0].Notes);
        Assert.Equal(habit1.Id, result.Items[0].Habit.Id);
    }
    
    [Fact]
    public async Task GetEntries_ShouldFilterByDateRange()
    {
        // Arrange
        await CleanupDatabaseAsync();
        HttpClient client = await CreateAuthenticatedClientAsync();
        
        // Create a habit
        CreateHabitDto createHabitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage createHabitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, createHabitDto);
        HabitDto? habitDto = await createHabitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        // Create entries for different dates
        CreateEntryDto entry1 = TestData.Entry.CreateEntryForDate(
            habitId: habitDto.Id, 
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            note: "Yesterday's entry");
        CreateEntryDto entry2 = TestData.Entry.CreateEntryForDate(
            habitId: habitDto.Id, 
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            note: "Tomorrow's entry");

        await client.PostAsJsonAsync(Routes.Entries.Create, entry1);
        await client.PostAsJsonAsync(Routes.Entries.Create, entry2);
        
        // Act
        string from = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string to = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string url = $"{Routes.Entries.GetAll}?fromDate={from}&toDate={to}";
        HttpResponseMessage response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PaginationResult<EntryDto>? result = await response.Content.ReadFromJsonAsync<PaginationResult<EntryDto>>();
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(entry1.Value, result.Items[0].Value);
    }

    [Fact]
    public async Task CreateEntry_ShouldSucceed_WithValidParameters()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        CreateHabitDto createHabitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage createHabitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, createHabitDto);
        HabitDto? habitDto = await createHabitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        // Create an entry
        CreateEntryDto createEntryDto = TestData.Entry.CreateEntry(habitDto.Id);
        
        // Act
        HttpResponseMessage createEntryResponse = await client.PostAsJsonAsync(Routes.Entries.Create, createEntryDto);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, createEntryResponse.StatusCode);
        EntryDto? createdEntry = await createEntryResponse.Content.ReadFromJsonAsync<EntryDto>();
        Assert.NotNull(createdEntry);
        Assert.Equal(createEntryDto.Value, createdEntry.Value);
    }

    [Fact]
    public async Task UpdateEntry_ShouldSucceed_WithValidParameters()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        CreateHabitDto createHabitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage createHabitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, createHabitDto);
        HabitDto? habitDto = await createHabitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        // Create an entry
        CreateEntryDto createEntryDto = TestData.Entry.CreateEntry(habitDto.Id);
        HttpResponseMessage createEntryResponse = await client.PostAsJsonAsync(Routes.Entries.Create, createEntryDto);
        EntryDto? createdEntry = await createEntryResponse.Content.ReadFromJsonAsync<EntryDto>();
        Assert.NotNull(createdEntry);

        // Update the entry
        UpdateEntryDto updateEntryDto = TestData.Entry.CreateUpdateEntry();

        // Act
        HttpResponseMessage updateEntryResponse = await client.PutAsJsonAsync(
            Routes.Entries.Update(createdEntry.Id), updateEntryDto);
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, updateEntryResponse.StatusCode);
        HttpResponseMessage getEntryResponse = await client.GetAsync(Routes.Entries.GetById(createdEntry.Id));
        EntryDto? updatedEntry = await getEntryResponse.Content.ReadFromJsonAsync<EntryDto>();
        Assert.NotNull(updatedEntry);
        Assert.Equal(updateEntryDto.Value, updatedEntry.Value);
        Assert.Equal(updateEntryDto.Notes, updatedEntry.Notes);
    }

    [Fact]
    public async Task DeleteEntry_ShouldSucceed_WhenEntryExists()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        CreateHabitDto createHabitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage createHabitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, createHabitDto);
        HabitDto? habitDto = await createHabitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        // Create an entry
        CreateEntryDto createEntryDto = TestData.Entry.CreateEntry(habitDto.Id);
        HttpResponseMessage createEntryResponse = await client.PostAsJsonAsync(Routes.Entries.Create, createEntryDto);
        EntryDto? createdEntry = await createEntryResponse.Content.ReadFromJsonAsync<EntryDto>();
        Assert.NotNull(createdEntry);

        // Act
        HttpResponseMessage deleteEntryResponse = await client.DeleteAsync(Routes.Entries.Delete(createdEntry.Id));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteEntryResponse.StatusCode);
        
        // Verify the entry is deleted
        HttpResponseMessage getEntryResponse = await client.GetAsync(Routes.Entries.GetById(createdEntry.Id));
        Assert.Equal(HttpStatusCode.NotFound, getEntryResponse.StatusCode);
    }

    [Fact]
    public async Task CreateBatch_ShouldSucceed_WithValidParameters()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        CreateHabitDto createHabitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage createHabitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, createHabitDto);
        HabitDto? habitDto = await createHabitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        // Create a batch of entries
        CreateEntryBatchDto createEntryBatchDto = TestData.Entry.CreateEntryBatch(habitDto.Id, 
            (DateOnly.FromDateTime(DateTime.UtcNow), 10),
            (DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 20),
            (DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), 30));
        
        // Act
        HttpResponseMessage createBatchResponse = await client.PostAsJsonAsync(Routes.Entries.CreateBatch, createEntryBatchDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, createBatchResponse.StatusCode);
        List<EntryDto>? createdEntries = await createBatchResponse.Content.ReadFromJsonAsync<List<EntryDto>>();
        Assert.NotNull(createdEntries);
        Assert.Equal(3, createdEntries.Count);
        Assert.Equal(createEntryBatchDto.Entries[0].Value, createdEntries[0].Value);
        Assert.Equal(createEntryBatchDto.Entries[1].Value, createdEntries[1].Value);
        Assert.Equal(createEntryBatchDto.Entries[2].Value, createdEntries[2].Value);
    }

    [Fact]
    public async Task GetStats_ShouldReturnStats_WhenEntriesExist()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        CreateHabitDto createHabitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage createHabitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, createHabitDto);
        HabitDto? habitDto = await createHabitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        // Create entries
        CreateEntryDto[] entries = new[]
        {
            TestData.Entry.CreateEntryForDate(habitDto.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), 10),
            TestData.Entry.CreateEntryForDate(habitDto.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), 20),
            TestData.Entry.CreateEntryForDate(habitDto.Id, DateOnly.FromDateTime(DateTime.UtcNow), 30)
        };

        foreach (CreateEntryDto entry in entries)
        {
            await client.PostAsJsonAsync(Routes.Entries.Create, entry);
        }

        // Act
        HttpResponseMessage response = await client.GetAsync(Routes.Entries.Stats);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        EntryStatsDto? stats = await response.Content.ReadFromJsonAsync<EntryStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(3, stats.TotalEntries);
        Assert.Equal(3, stats.CurrentStreak);
        Assert.Equal(3, stats.LongestStreak);
        Assert.Equal(3, stats.DailyStats.Count);
    }
}
