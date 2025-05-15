using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.DTOs.EntryImports;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Routing;

namespace DevHabit.FunctionalTests.Infrastructure;

public sealed class EntryImportFlowTests(DevHabitWebAppFactory factory) : FunctionalTestFixture(factory)
{
    [Fact]
    public async Task CompleteEntryImportFlow_ShouldSucceed()
    {
        // Arrange
        await CleanupDatabaseAsync();
        const string email = "importflow@test.com";

        HttpClient client = await CreateAuthenticatedClientAsync(email);

        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, TestData.Habit.CreateReadingHabit());
        Assert.Equal(HttpStatusCode.Created, habitResponse.StatusCode);
        HabitDto? createdHabit = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(createdHabit);

        string csvContent = $"""
            habit_id,date,value,notes
            {createdHabit.Id},2024-01-01,30,First day of reading
            {createdHabit.Id},2024-01-02,25,Second day of reading
            {createdHabit.Id},2024-01-03,35,Third day of reading
            """;

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new("text/csv");
        content.Add(fileContent, "file", "entries.csv");

        HttpResponseMessage importResponse = await client.PostAsync(Routes.EntryImports.Create, content);
        Assert.Equal(HttpStatusCode.Created, importResponse.StatusCode);
        EntryImportJobDto? importJob = await importResponse.Content.ReadFromJsonAsync<EntryImportJobDto>();
        Assert.NotNull(importJob);

        const int maxAttempts = 10;
        const int delayMs = 500;
        EntryImportJobDto? completedJob = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(delayMs);

            HttpResponseMessage response = await client.GetAsync(Routes.EntryImports.GetById(importJob.Id));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            completedJob = await response.Content.ReadFromJsonAsync<EntryImportJobDto>();
            Assert.NotNull(completedJob);

            if (completedJob.Status is EntryImportStatus.Completed or EntryImportStatus.Failed)
            {
                break;
            }
        }

        Assert.NotNull(completedJob);
        Assert.Equal(EntryImportStatus.Completed, completedJob.Status);
        Assert.Equal(3, completedJob.ProcessedRecords);
        Assert.Equal(0, completedJob.FailedRecords);

        HttpResponseMessage getEntriesResponse = await client.GetAsync(
            $"{Routes.Entries.GetAll}?habitId={createdHabit.Id}");
        Assert.Equal(HttpStatusCode.OK, getEntriesResponse.StatusCode);
        PaginationResult<EntryDto>? entries = await getEntriesResponse.Content
            .ReadFromJsonAsync<PaginationResult<EntryDto>>();
        Assert.NotNull(entries);
        Assert.Equal(3, entries.Items.Count);
    }
}
