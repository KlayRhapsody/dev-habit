
using DevHabit.Api.Database;
using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DevHabit.Api.Jobs;

public sealed class CleanupEntryImportJobsJob(
    ApplicationDbContext dbContext,
    ILogger<CleanupEntryImportJobsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            DateTime completedJobsCutoffDate = DateTime.UtcNow.AddDays(-7);

            int deletedCount = await dbContext.EntryImportJobs
                .Where(e => e.Status == EntryImportStatus.Completed)
                .Where(e => e.CompletedAtUtc < completedJobsCutoffDate)
                .ExecuteDeleteAsync();

            if (deletedCount > 0)
            {
                logger.LogInformation("Deleted {Count} old import jobs", deletedCount);
            }

            DateTime failedJobsCutoffDate = DateTime.UtcNow.AddDays(-30);

            deletedCount = await dbContext.EntryImportJobs
                .Where(e => e.Status == EntryImportStatus.Failed)
                .Where(e => e.CompletedAtUtc < failedJobsCutoffDate)
                .ExecuteDeleteAsync();

            if (deletedCount > 0)
            {
                logger.LogInformation("Deleted {Count} old failed import jobs", deletedCount);
            }

            DateTime processingJobsCutoffDate = DateTime.UtcNow.AddHours(-2);

            deletedCount = await dbContext.EntryImportJobs
                .Where(j => j.Status == EntryImportStatus.Processing)
                .Where(j => j.CreatedAtUtc < processingJobsCutoffDate)
                .ExecuteDeleteAsync();

            if (deletedCount > 0)
            {
                logger.LogWarning("Deleted {Count} stuck import jobs", deletedCount);
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up old import jobs");
        }
    }
}
