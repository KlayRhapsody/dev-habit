
using System.Dynamic;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.Services;

namespace DevHabit.UnitTests.Services;

public sealed class DataShapingServiceTests
{
    private readonly DataShapingService _dataShapingService = new();

    private sealed record TestDto
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string? Description { get; init; }
        public int Value { get; init; }
    }

    [Fact]
    public void ShapeData_ShouldReturnAllProperties_WhenFieldsAreNull()
    {
        var dto = new TestDto
        {
            Id = "1",
            Name = "Test",
            Description = "Test description",
            Value = 42
        };

        ExpandoObject result = _dataShapingService.ShapeData(dto, null);

        IDictionary<string, object?> dict = result;
        Assert.Equal(4, dict.Count);
        Assert.Equal("1", dict["Id"]);
        Assert.Equal("Test", dict["Name"]);
        Assert.Equal("Test description", dict["Description"]);
        Assert.Equal(42, dict["Value"]);
    }

    [Fact]
    public void ShapeData_ShouldReturnRequestedProperties_WhenFieldsAreSpecified()
    {
        var dto = new TestDto
        {
            Id = "1",
            Name = "Test",
            Description = "Test description",
            Value = 42
        };

        ExpandoObject result = _dataShapingService.ShapeData(dto, "id,name");

        IDictionary<string, object?> dict = result;
        Assert.Equal(2, dict.Count);
        Assert.Equal("1", dict["Id"]);
        Assert.Equal("Test", dict["Name"]);
        Assert.False(dict.ContainsKey("Description"));
        Assert.False(dict.ContainsKey("Value"));
    }

    [Fact]
    public void ShapeData_ShouldBeCaseInsensitive_WhenMatchingFields()
    {
        // Arrange
        var entity = new TestDto
        {
            Id = "1",
            Name = "Test",
            Description = "Description",
            Value = 42
        };

        // Act
        ExpandoObject result = _dataShapingService.ShapeData(entity, "ID,NAME");

        // Assert
        IDictionary<string, object?> dict = result;
        Assert.Equal(2, dict.Count);
        Assert.Equal("1", dict["Id"]);
        Assert.Equal("Test", dict["Name"]);
    }

    [Fact]
    public void ShapeCollectionData_ShouldReturnAllProperties_WhenFieldsAreNull()
    {
        var dtos = new List<TestDto>
        {
            new() { Id = "1", Name = "Test1", Description = "Description1", Value = 10 },
            new() { Id = "2", Name = "Test2", Description = "Description2", Value = 20 }
        };

        List<ExpandoObject> result = _dataShapingService.ShapeCollectionData(dtos, null);

        Assert.Equal(2, result.Count);
        Assert.All(result, item =>
        {
            IDictionary<string, object?> dict = item;
            Assert.Equal(4, dict.Count);
            Assert.Contains("Id", dict.Keys);
            Assert.Contains("Name", dict.Keys);
            Assert.Contains("Description", dict.Keys);
            Assert.Contains("Value", dict.Keys);
        });

        IDictionary<string, object?> firstItem = result[0];
        Assert.Equal(4, firstItem.Count);
        Assert.Equal("1", firstItem["Id"]);
        Assert.Equal("Test1", firstItem["Name"]);
        Assert.Equal("Description1", firstItem["Description"]);
        Assert.Equal(10, firstItem["Value"]);
    }

    [Fact]
    public void ShapeCollectionData_ShouldIncludeLinks_WhenLinksFactoryIsProvided()
    {
        var dtos = new List<TestDto>
        {
            new() { Id = "1", Name = "Test1", Description = "Description1", Value = 10 },
            new() { Id = "2", Name = "Test2", Description = "Description2", Value = 20 }
        };

        static List<LinkDto> CreateLinks(TestDto dto) =>
        [
            new ()
            {
                Rel = "self",
                Href = $"test/{dto.Id}",
                Method = "GET"
            }
        ];
        
        List<ExpandoObject> result = _dataShapingService.ShapeCollectionData(dtos, null, CreateLinks);

        IDictionary<string, object?> firstItem = result[0];
        Assert.True(firstItem.ContainsKey("links"));
        var links = (List<LinkDto>)firstItem["links"]!;
        Assert.Single(links);
        Assert.Equal("self", links[0].Rel);
        Assert.Equal("test/1", links[0].Href);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("id,name", true)]
    [InlineData("ID,NAME", true)]
    [InlineData("Id,UnknownField", false)]
    [InlineData("name,INVALIDFIELD", false)]
    public void Validate_ShouldReturnExpectedResult_WhenValidatingFields(string? fields, bool expected)
    {
        // Act
        bool result = _dataShapingService.Validate<TestDto>(fields);

        // Assert
        Assert.Equal(expected, result);
    }
}
