
using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.Entities;
using FluentValidation.Results;

namespace DevHabit.UnitTests.Validators;

public sealed class CreateEntryDtoValidatorTests
{
    private readonly CreateEntryDtoValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldSuccess_WhenInputDtoIsValid()
    {
        // Arrange
        var dto = new CreateEntryDto
        {
            HabitId = Habit.NewId(),
            Value = 1,
            Date = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        // Act
        ValidationResult result = await _validator.ValidateAsync(dto);
    
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenHabitIdIsEmpty()
    {
        // Arrange
        var dto = new CreateEntryDto
        {
            HabitId = string.Empty,
            Value = 1,
            Date = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        // Act
        ValidationResult result = await _validator.ValidateAsync(dto);
    
        // Assert
        Assert.False(result.IsValid);
        ValidationFailure validationFailure = Assert.Single(result.Errors);
        Assert.Equal(nameof(dto.HabitId), validationFailure.PropertyName);
    }
}
