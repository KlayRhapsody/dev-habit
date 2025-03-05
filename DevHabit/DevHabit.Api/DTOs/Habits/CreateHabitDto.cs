using DevHabit.Api.Entities;
using FluentValidation;

namespace DevHabit.Api.DTOs.Habits;

public sealed record CreateHabitDto
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required HabitType Type { get; init; }
    public required FrequencyDto Frequency { get; init; }
    public required TargetDto Target { get; init; }
    public DateOnly? EndedDate { get; init; }
    public MilestoneDto? Milestone { get; init; }
}

public sealed class CreateHabitDtoValidator : AbstractValidator<CreateHabitDto>
{
    private static readonly string[] AllowedUnits = 
    [
        // 分鐘、小時、步數、公里、卡路里、頁數、書本數、任務數、會話數
        "minutes", "hours", "steps", "km", "cal", 
        "pages", "books", "tasks", "sesstions"
    ];

    private static readonly string[] AllowedUnitsForBinaryHabits = ["sessions", "tasks"];

    public CreateHabitDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(100)
            .WithMessage("Habit name must be between 3 and 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null)
            .WithMessage("Habit description must be less than 500 characters");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Invalid habit type");

        RuleFor(x => x.Frequency.Type)
            .IsInEnum()
            .WithMessage("Invalid frequency type");
        
        RuleFor(x => x.Frequency.TimesPerPeriod)
            .GreaterThan(0)
            .WithMessage("Frequency times per period must be greater than 0");

        RuleFor(x => x.Target.Value)
            .GreaterThan(0)
            .WithMessage("Target value must be greater than 0");

        RuleFor(x => x.Target.Unit)
            .NotEmpty()
            .Must(unit => AllowedUnits.Contains(unit.ToLowerInvariant()))
            .WithMessage($"Unit must be one of the following: {string.Join(", ", AllowedUnits)}");

        RuleFor(x => x.EndedDate)
            .Must(date => date is null || date > DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ended date must be in the future");

        When(x => x.Milestone is not null, () =>
        {
            RuleFor(x => x.Milestone!.Target)
                .GreaterThan(0)
                .WithMessage("Milestone target must be greater than 0");
        });

        RuleFor(x => x.Target.Unit)
            .Must((dto, unit) => IsTargetUnitCompatibleWithType(dto.Type, unit))
            .WithMessage("Target unit is not compatible with habit type");
    }

    private bool IsTargetUnitCompatibleWithType(HabitType type, string unit)
    {
        string normalizedUnit = unit.ToLowerInvariant();

        return type switch
        {
            HabitType.Binary => AllowedUnitsForBinaryHabits.Contains(normalizedUnit),
            HabitType.Measurable => AllowedUnits.Contains(normalizedUnit),
            _ => false
        };
    }
}
