using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.DTOs.Tags;
using DevHabit.FunctionalTests.Infrastructure;

namespace DevHabit.FunctionalTests.Tests;

public sealed class HabitManagementFlowTests(DevHabitWebAppFactory factory) : FunctionalTestFixture(factory)
{
     [Fact]
    public async Task CompleteHabitManagementFlow_ShouldSucceed()
    {
        await CleanupDatabaseAsync();

        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage habitResponse = await client.PostAsJsonAsync(Routes.Habits.Create, TestData.Habit.CreateReadingHabit());
        Assert.Equal(HttpStatusCode.Created, habitResponse.StatusCode);
        HabitDto? habitDto = await habitResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(habitDto);

        HttpResponseMessage tagResponse = await client.PostAsJsonAsync(Routes.Tags.Create, TestData.Tags.CreateImportantTag());
        Assert.Equal(HttpStatusCode.Created, tagResponse.StatusCode);
        TagDto? tagDto = await tagResponse.Content.ReadFromJsonAsync<TagDto>();
        Assert.NotNull(tagDto);

        HttpResponseMessage habitTagResponse = await client.PutAsJsonAsync(
            Routes.HabitTags.UpsertTags(habitDto.Id), TestData.HabitTags.CreateUpsertDto(tagDto.Id));
        Assert.Equal(HttpStatusCode.NoContent, habitTagResponse.StatusCode);

        HttpResponseMessage response = await client.GetAsync(Routes.Habits.GetById(habitDto.Id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HabitWithTagsDto? habitWithTagsDto = await response.Content.ReadFromJsonAsync<HabitWithTagsDto>();
        Assert.NotNull(habitWithTagsDto);
        Assert.Single(habitWithTagsDto.Tags);
        Assert.Equal(tagDto.Name, habitWithTagsDto.Tags[0]);
    }
}
