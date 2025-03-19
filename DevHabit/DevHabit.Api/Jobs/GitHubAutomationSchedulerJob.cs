using DevHabit.Api.Database;
using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DevHabit.Api.Jobs;

public sealed class GitHubAutomationSchedulerJob(
    ApplicationDbContext dbContext,
    ILogger<GitHubAutomationSchedulerJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            logger.LogInformation("Starting GitHub automation scheduler job");

            List<Habit> habits = await dbContext.Habits
                .Where(h => h.AutomationSource == AutomationSource.Github && !h.IsArchived)
                .ToListAsync(context.CancellationToken);

            logger.LogInformation("Found {HabitCount} habits with GitHub automation source", habits.Count);

            foreach (Habit habit in habits)
            {
                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity($"github-habit-{habit.Id}", "github-habits")
                    .StartNow()
                    .Build();

                IJobDetail jobDetail = JobBuilder.Create<GitHubHabitProcessorJob>()
                    .WithIdentity($"github-habit-{habit.Id}", "github-habits")
                    .UsingJobData("habitId", habit.Id)
                    .Build();

                await context.Scheduler.ScheduleJob(jobDetail, trigger, context.CancellationToken);
                
                logger.LogInformation("Scheduled processor job for habit {HabitId}", habit.Id);
            }

            logger.LogInformation("Finished GitHub automation scheduler job");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while executing the GitHub automation scheduler job");
            throw;
        }
    }
}
