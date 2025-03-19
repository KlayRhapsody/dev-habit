namespace DevHabit.Api.Settings;

public sealed class GitHubAutomationOptions
{
    public const string GitHubAutomation = "GitHubAutomation";

    public required int ScanIntervalMinutes { get; init; }
}
