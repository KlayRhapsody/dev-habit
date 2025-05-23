using FluentValidation;

namespace DevHabit.Api.DTOs.Tags;

public sealed class CreateTagDtoValidator : AbstractValidator<CreateTagDto>
{
    public CreateTagDtoValidator()
    {
        RuleFor(t => t.Name).NotEmpty().MinimumLength(3).MaximumLength(50);
        RuleFor(t => t.Description).MaximumLength(500);
    }
}
