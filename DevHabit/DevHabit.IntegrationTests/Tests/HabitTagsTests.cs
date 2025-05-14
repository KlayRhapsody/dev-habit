using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.DTOs.Tags;
using DevHabit.Api.Entities;
using DevHabit.IntegrationTests.Infrastructure;

namespace DevHabit.IntegrationTests.Tests;

public sealed class HabitTagsTests(DevHabitWebAppFactory factory)
    : IntegrationTestFixture(factory)
{
    [Fact]
    public async Task UpsertTags_ShouldSucceed_WhenTagsExist()
    {
        // Arrange
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(
            Routes.Habits.Create,
            TestData.Habit.CreateReadingHabit());
        HabitDto? habitDto = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();

        Assert.NotNull(habitDto);

        HttpResponseMessage tag1Response = await client.PostAsJsonAsync(
            Routes.Tags.Create, TestData.Tags.CreateImportantTag());
        HttpResponseMessage tag2Response = await client.PostAsJsonAsync(
            Routes.Tags.Create, TestData.Tags.CreateProductivityTag());

        TagDto? tagDto1 = await tag1Response.Content.ReadFromJsonAsync<TagDto>();
        TagDto? tagDto2 = await tag2Response.Content.ReadFromJsonAsync<TagDto>();

        Assert.NotNull(tagDto1);
        Assert.NotNull(tagDto2);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            Routes.HabitTags.UpsertTags(habitDto.Id), 
            TestData.HabitTags.CreateUpsertDto(tagDto1.Id, tagDto2.Id));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpsertTags_ShouldSucceed_WhenRemovingAllTags()
    {
        // Arrange
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(
            Routes.Habits.Create, 
            TestData.Habit.CreateReadingHabit());
        HabitDto? habit = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);

        // Create a tag
        HttpResponseMessage tagResponse = await client.PostAsJsonAsync(
            Routes.Tags.Create, 
            TestData.Tags.CreateImportantTag());
        TagDto? tag = await tagResponse.Content.ReadFromJsonAsync<TagDto>();
        Assert.NotNull(tag);

        // Assign the tag
        await client.PutAsJsonAsync(
            Routes.HabitTags.UpsertTags(habit.Id),
            TestData.HabitTags.CreateUpsertDto(tag.Id));

        // Act - Remove all tags
        HttpResponseMessage response = await client.PutAsJsonAsync(
            Routes.HabitTags.UpsertTags(habit.Id),
            TestData.HabitTags.CreateUpsertDto());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpsertTags_ShouldFail_WhenHabitDoesNotExist()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a tag
        HttpResponseMessage tagResponse = await client.PostAsJsonAsync(
            Routes.Tags.Create, 
            TestData.Tags.CreateImportantTag());
        TagDto? tag = await tagResponse.Content.ReadFromJsonAsync<TagDto>();
        Assert.NotNull(tag);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync(
            Routes.HabitTags.UpsertTags(Habit.NewId()),
            TestData.HabitTags.CreateUpsertDto(tag.Id));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertTags_ShouldFail_WhenTagDoesNotExist()
    {
        // Arrange
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(
            Routes.Habits.Create, 
            TestData.Habit.CreateReadingHabit());
        HabitDto? habit = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync(
            Routes.HabitTags.UpsertTags(habit.Id),
            TestData.HabitTags.CreateUpsertDto(Tag.NewId()));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTag_ShouldSucceed_WhenTagAssigned()
    {
        // Arrange
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(
            Routes.Habits.Create, 
            TestData.Habit.CreateReadingHabit());
        HabitDto? habit = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);

        // Create a tag
        HttpResponseMessage tagResponse = await client.PostAsJsonAsync(
            Routes.Tags.Create, 
            TestData.Tags.CreateImportantTag());
        TagDto? tag = await tagResponse.Content.ReadFromJsonAsync<TagDto>();
        Assert.NotNull(tag);

        // Assign the tag
        await client.PutAsJsonAsync(
            Routes.HabitTags.UpsertTags(habit.Id),
            TestData.HabitTags.CreateUpsertDto(tag.Id));

        // Act
        HttpResponseMessage response = await client.DeleteAsync(
            Routes.HabitTags.DeleteTag(habit.Id, tag.Id));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTag_ShouldReturnNotFound_WhenTagNotAssigned()
    {
        // Arrange
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        // Create a habit
        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(
            Routes.Habits.Create, 
            TestData.Habit.CreateReadingHabit());
        HabitDto? habit = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habit);

        // Create a tag
        HttpResponseMessage tagResponse = await client.PostAsJsonAsync(
            Routes.Tags.Create, 
            TestData.Tags.CreateImportantTag());
        TagDto? tag = await tagResponse.Content.ReadFromJsonAsync<TagDto>();
        Assert.NotNull(tag);

        // Act - Try to delete without assigning first
        HttpResponseMessage response = await client.DeleteAsync(
            Routes.HabitTags.DeleteTag(habit.Id, tag.Id));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

