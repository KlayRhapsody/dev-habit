using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.EntryImports;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace DevHabit.IntegrationTests.Tests;

public sealed class EntryImportsTests(DevHabitWebAppFactory factory)
    : IntegrationTestFixture(factory)
{
    [Fact]
    public async Task GetImportJobs_ShouldReturnEmptyList_WhenNoJobsExist()
    {
        // Arrange
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        // Act
        HttpResponseMessage response = await client.GetAsync(Routes.EntryImports.GetAll);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PaginationResult<EntryImportJobDto>? result = 
            await response.Content.ReadFromJsonAsync<PaginationResult<EntryImportJobDto>>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task CreateImportJob_ShouldSucceed_WithValidFile()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        CreateHabitDto habitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, habitDto);
        HabitDto? habit = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);

        string csvContent = $"""
            habit_id,date,notes
            {habit.Id},2024-01-01,Started the year strong
            {habit.Id},2024-01-02,Making progress
            {habit.Id},2024-01-03,Getting better
            """;
        
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new ("text/csv");
        using var content = new MultipartFormDataContent
        {
            { fileContent, "file", "entries.csv" }
        };

        // Act
        HttpResponseMessage response = await client.PostAsync(Routes.EntryImports.Create, content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        EntryImportJobDto? result = await response.Content.ReadFromJsonAsync<EntryImportJobDto>();
        Assert.NotNull(result);
        Assert.Equal(EntryImportStatus.Pending, result.Status);
    }

    [Fact]
    public async Task GetImportJob_ShouldReturnJob_WhenExists()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        CreateHabitDto habitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, habitDto);
        HabitDto? habit = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);

        string csvContent = $"""
            habit_id,date,notes
            {habit.Id},2024-01-01,Started the year strong
            """;

        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new ("text/csv");
        using var content = new MultipartFormDataContent
        {
            { fileContent, "file", "entries.csv" }
        };
        HttpResponseMessage createResponse = await client.PostAsync(Routes.EntryImports.Create, content);
        EntryImportJobDto? createdJob = await createResponse.Content.ReadFromJsonAsync<EntryImportJobDto>();
        Assert.NotNull(createdJob);

        // Act
        HttpResponseMessage response = await client.GetAsync(Routes.EntryImports.GetById(createdJob.Id));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        EntryImportJobDto? result = await response.Content.ReadFromJsonAsync<EntryImportJobDto>();
        Assert.NotNull(result);
        Assert.Equal(createdJob.Id, result.Id);
    }

    [Fact]
    public async Task GetImportJob_ShouldReturnNotFound_WhenJobDoesNotExist()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Act
        HttpResponseMessage response = await client.GetAsync(Routes.EntryImports.GetById("not-found-id"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetImportJobs_ShouldIncludeHateoasLinks_WhenRequested()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();
        client.DefaultRequestHeaders.Accept.Add(new (CustomMediaTypeNames.Application.HateoasJson));

        CreateHabitDto habitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage habitReponse = await client.PostAsJsonAsync(Routes.Habits.Create, habitDto);
        HabitDto? habit = await habitReponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);

        string csvContent = $"""
            habit_id,date,notes
            {habit.Id},2024-01-01,Started the year strong
            {habit.Id},2024-01-02,Making progress
            {habit.Id},2024-01-03,Getting better
            """;

        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new("text/csv");
        using var content = new MultipartFormDataContent
        {
            { fileContent, "file", "entries.csv" }
        };

        await client.PostAsJsonAsync(Routes.EntryImports.Create, content);

        HttpResponseMessage response = await client.GetAsync(Routes.EntryImports.GetAll);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PaginationResult<EntryImportJobDto>? result = await response.Content
            .ReadFromJsonAsync<PaginationResult<EntryImportJobDto>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Links);
        Assert.NotEmpty(result.Links);
        Assert.NotNull(result.Items[0].Links);
        Assert.NotEmpty(result.Items[0].Links ?? []);
    }

    [Fact]
    public async Task GetImportJobs_ShouldSupportPagination()
    {
        // Arrange
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit first
        CreateHabitDto habitDto = TestData.Habit.CreateReadingHabit();
        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, habitDto);
        HabitDto? habit = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);

        // Create multiple import jobs
        for (int i = 0; i < 3; i++)
        {
            // Create CSV content
            string csvContent =
                $"""
                 habit_id,date,notes
                 {habit.Id},2024-01-01,Started the year strong
                 {habit.Id},2024-01-02,Making progress
                 {habit.Id},2024-01-03,Getting better
                 """;

            // Create file content
            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
            fileContent.Headers.ContentType = new("text/csv");
            content.Add(fileContent, "file", "entries.csv");

            await client.PostAsync(Routes.EntryImports.Create, content);
        }

        // Act
        HttpResponseMessage response = await client.GetAsync($"{Routes.EntryImports.GetAll}?page=1&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PaginationResult<EntryImportJobDto>? result = await response.Content
            .ReadFromJsonAsync<PaginationResult<EntryImportJobDto>>();

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.True(result.HasNextPage);
    }
}
