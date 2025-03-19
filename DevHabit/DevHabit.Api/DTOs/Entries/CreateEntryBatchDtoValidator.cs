using System.Data;
using FluentValidation;

namespace DevHabit.Api.DTOs.Entries;

public sealed class CreateEntryBatchDtoValidator : AbstractValidator<CreateEntryBatchDto>
{
    public CreateEntryBatchDtoValidator(CreateEntryDtoValidator createEntryDtoValidator)
    {
        RuleFor(x => x.Entries)
            .NotEmpty()
            .WithMessage("Entries cannot be empty.")
            .Must(Entries => Entries.Count <= 20)
            .WithMessage("A maximum of 20 entries is allowed.");

        RuleForEach(x => x.Entries)
            .SetValidator(createEntryDtoValidator);
    }
}
