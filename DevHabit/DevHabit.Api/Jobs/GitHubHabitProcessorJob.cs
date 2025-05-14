using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Github;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DevHabit.Api.Jobs;

public class GitHubHabitProcessorJob(
    ApplicationDbContext dbContext,
    GitHubAccessTokenService gitHubAccessTokenService,
    RefitGitHubService gitHubService,
    ILogger<GitHubHabitProcessorJob> logger) : IJob
{
    private const string PushEvent = "PushEvent";

    public async Task Execute(IJobExecutionContext context)
    {
        string habitId = context.JobDetail.JobDataMap.GetString("habitId") ??
            throw new InvalidOperationException("Habit ID not found in job data");
        
        try
        {
            logger.LogInformation("Starting GitHub habit processor job for habit {HabitId}", habitId);

            Habit? habit = await dbContext.Habits
                .FirstOrDefaultAsync(
                    h => h.Id == habitId && 
                    h.AutomationSource == AutomationSource.Github && 
                    !h.IsArchived,
                    context.CancellationToken);

            if (habit is null)
            {
                logger.LogWarning("Habit {HabitId} not found or is archived", habitId);
                return;
            }

            string? accessToken = await gitHubAccessTokenService.GetAsync(
                habit.UserId, 
                context.CancellationToken);
            
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                logger.LogWarning("Access token not found for user {UserId}", habit.UserId);
                return;
            }

            GitHubUserProfileDto profile = await gitHubService.GetUserProfileAsync(
                accessToken, 
                context.CancellationToken);

            if (profile is null)
            {
                logger.LogWarning("Couldn't retrieve GitHub profile for user {UserId}", habit.UserId);
                return;
            }

            List<GitHubEventDto> githubEvents = [];
            const int perPage = 100;
            const int pagesToFetch = 10;
            
            for (int i = 1; i <= pagesToFetch; i++)
            {
                IReadOnlyList<GitHubEventDto>? pageEvents = await gitHubService.GetUserEventsAsync(
                username: profile.Login,
                accessToken: accessToken,
                page: pagesToFetch,
                perPage: perPage,
                cancellationToken: context.CancellationToken);

                if (pageEvents is null || !pageEvents.Any())
                {
                    break;
                }

                githubEvents.AddRange(pageEvents);
            }

            if (!githubEvents.Any())
            {
                logger.LogInformation("No GitHub events found for user {UserId}", habit.UserId);
                return;
            }
            
            var pushEvents = githubEvents
                .Where(e => e.Type == PushEvent)
                .ToList();
            
            logger.LogInformation("Found {Count} push events for habit {HabitId}", pushEvents.Count, habitId);

            foreach (GitHubEventDto gitHubEventDto in pushEvents)
            {
                bool entryExists = await dbContext.Entries
                    .AnyAsync(e => e.HabitId == habitId && e.ExternalId == gitHubEventDto.Id,
                    context.CancellationToken);

                if (entryExists)
                {
                    logger.LogInformation("Entry for push event {EventId} already exists", gitHubEventDto.Id);
                    continue;
                }

                var entry = new Entry
                {
                    Id = $"e_{Guid.CreateVersion7()}",
                    HabitId = habitId,
                    UserId = habit.UserId,
                    Value = 1,
                    Notes = 
                        $"""
                        {gitHubEventDto.Actor.Login} pushed:

                        {string.Join(
                                Environment.NewLine,
                                gitHubEventDto.Payload.Commits?.Select(c => $"- {c.Message}") ?? [])}
                        """,
                    Date = DateOnly.FromDateTime(gitHubEventDto.CreatedAt),
                    Source = EntrySource.Automation,
                    ExternalId = gitHubEventDto.Id,
                    CreatedAtUtc = DateTime.UtcNow
                };

                dbContext.Entries.Add(entry);

                logger.LogInformation(
                        "Created entry for event {EventId} on habit {HabitId}", 
                        gitHubEventDto.Id, 
                        habitId);
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
            
            logger.LogInformation("Completed processing GitHub events for habit {HabitId}", habitId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error processing GitHub events for habit {HabitId}",
                habitId);
            
            throw;
        }
    }
}
