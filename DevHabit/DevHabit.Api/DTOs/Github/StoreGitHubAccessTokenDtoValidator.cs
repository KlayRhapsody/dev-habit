using FluentValidation;

namespace DevHabit.Api.DTOs.Github;

public sealed class StoreGithubAccessTokenDtoValidator : AbstractValidator<StoreGithubAccessTokenDto>
{
    public StoreGithubAccessTokenDtoValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.ExpiresInDays)
            .GreaterThan(0)
            .LessThanOrEqualTo(365); // Maximum 1 year expiration
    }
}
