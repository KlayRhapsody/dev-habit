using FluentValidation;

namespace DevHabit.Api.DTOs.Entries;

public sealed class CreateEntryDtoValidator : AbstractValidator<CreateEntryDto>
{
    public CreateEntryDtoValidator()
    {
        RuleFor(entry => entry.HabitId)
            .NotEmpty()
            .WithMessage("HabitId cannot be empty.");

        RuleFor(entry => entry.Value)
            .GreaterThan(0)
            .WithMessage("Value must be greater than 0.");

        RuleFor(entry => entry.Note)
            .MaximumLength(1000)
            .When(entry => entry.Note is not null)
            .WithMessage("Note must be less than 1000 characters.");

        RuleFor(entry => entry.Date)
            .NotEmpty()
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date cannot be in the future.");
    }
}
