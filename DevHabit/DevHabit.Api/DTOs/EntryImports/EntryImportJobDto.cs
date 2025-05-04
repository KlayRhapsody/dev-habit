
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.Entities;

namespace DevHabit.Api.DTOs.EntryImports;

public sealed record EntryImportJobDto
{
    public required string Id { get; set; }
    public EntryImportStatus Status { get; set; }
    public required string FileName { get; set; }
    public required int TotalRecords { get; set; }
    public required int ProcessedRecords { get; set; }
    public required int SuccessfulRecords { get; set; }
    public required int FailedRecords { get; set; }
    public required List<string> Errors { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public List<LinkDto>? Links { get; set; }
}
