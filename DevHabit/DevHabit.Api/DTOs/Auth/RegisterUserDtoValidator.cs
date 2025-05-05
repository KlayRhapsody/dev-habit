
using FluentValidation;

namespace DevHabit.Api.DTOs.Auth;

public sealed class RegisterUserDtoValidator : AbstractValidator<RegisterUserDto>
{
    public RegisterUserDtoValidator()
    {
        RuleFor(u => u.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);
        
        RuleFor(u => u.Name)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);
        
        RuleFor(u => u.Password)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(100);
        
        RuleFor(u => u.ConfirmPassword)
            .NotEmpty()
            .Equal(u => u.Password)
            .WithMessage("Passwords do not match");
    }
}

