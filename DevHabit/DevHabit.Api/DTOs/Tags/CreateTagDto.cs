using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace DevHabit.Api.DTOs.Tags;

public sealed record CreateTagDto
{
    public required string Name { get; init; }

    public string? Description { get; init; }
}

public sealed class CreateTagDtoValidator : AbstractValidator<CreateTagDto>
{
    public CreateTagDtoValidator()
    {
        RuleFor(t => t.Name).NotEmpty().MinimumLength(5);
        RuleFor(t => t.Description).MaximumLength(50);
    }
}
