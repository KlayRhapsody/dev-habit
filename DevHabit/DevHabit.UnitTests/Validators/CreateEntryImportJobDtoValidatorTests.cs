using DevHabit.Api.DTOs.EntryImports;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DevHabit.UnitTests.Validators;

public sealed class CreateEntryImportJobDtoValidatorTests
{
    private readonly CreateEntryImportJobDtoValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldNotReturnError_WhenAllPropertiesAreValid()
    {
        // Arrange
        var dto = new CreateEntryImportJobDto 
        { 
            File = CreateFormFile("test.csv", "text/csv", 1024)
        };

        // Act
        TestValidationResult<CreateEntryImportJobDto> result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldReturnError_WhenFileIsNotCsv()
    {
        // Arrange
        var dto = new CreateEntryImportJobDto 
        { 
            File = CreateFormFile("test.txt", "text/plain", 1024)
        };

        // Act
        TestValidationResult<CreateEntryImportJobDto> result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File.FileName);
    }

    [Fact]
    public async Task Validate_ShouldReturnError_WhenFileExceedsMaxSize()
    {
        // Arrange
        var dto = new CreateEntryImportJobDto 
        { 
            File = CreateFormFile("test.csv", "text/csv", 11 * 1024 * 1024 )
        };

        // Act
        TestValidationResult<CreateEntryImportJobDto> result = await _validator.TestValidateAsync(dto);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File.Length);
    }


    private IFormFile CreateFormFile(string fileName, string contentType, long length)
    {
        IFormFile mockFile = Substitute.For<IFormFile>();
        mockFile.FileName.Returns(fileName);
        mockFile.ContentType.Returns(contentType);
        mockFile.Length.Returns(length);
        
        return mockFile;
    }
}
