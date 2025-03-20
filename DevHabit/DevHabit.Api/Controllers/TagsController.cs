using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Tags;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Settings;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevHabit.Api.Controllers;

[Authorize(Roles = Roles.Member)]
[ApiController]
[Route("tags")]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1)]
public sealed class TagsController(
    ApplicationDbContext dbContext, 
    LinkService linkService,
    UserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TagCollectionDto>> GetTags(
        [FromHeader] AcceptHeaderDto acceptHeader,
        IOptions<TagsOptions> options)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        List<TagDto> tags = await dbContext.Tags
            .Where(t => t.UserId == userId)
            .Select(TagQueries.ProjectToDto())
            .ToListAsync();

        TagCollectionDto tagCollectionDto = new()
        {
            Items = tags
        };

        if (acceptHeader.IncludLinks)
        {
            tagCollectionDto.Links = CreateLinksForTags(tags.Count, options.Value.MaxAllowedTags);
            foreach (TagDto tag in tagCollectionDto.Items)
            {
                tag.Links = CreateLinksForTag(tag.Id);
            }
        }

        return Ok(tagCollectionDto);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TagDto>> GetTag(
        string id,
        [FromHeader] AcceptHeaderDto acceptHeaderDto)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        TagDto? tag = await dbContext.Tags
            .Where(t => t.UserId == userId && t.Id == id)
            .Select(TagQueries.ProjectToDto())
            .FirstOrDefaultAsync();

        if (tag is null)
        {
            return NotFound();
        }

        if (acceptHeaderDto.IncludLinks)
        {
            tag.Links = CreateLinksForTag(id);
        }

        return Ok(tag);
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> CreateTag(
        CreateTagDto createTagDto,
        [FromHeader] AcceptHeaderDto acceptHeaderDto,
        IValidator<CreateTagDto> validator,
        ProblemDetailsFactory problemDetailsFactory)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // await validator.ValidateAndThrowAsync(createTagDto);
        ValidationResult validationResult = await validator.ValidateAsync(createTagDto);
        if (!validationResult.IsValid)
        {
            ProblemDetails problem = problemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest);
            problem.Extensions.Add("errors", validationResult.ToDictionary());

            return BadRequest(problem);
        }

        Tag tag = createTagDto.ToEntity(userId);

        if (await dbContext.Tags.AnyAsync(t => t.Name == tag.Name && t.UserId == userId))
        {
            return Problem(
                detail: $"A tag with the same name: {tag.Name} already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        dbContext.Tags.Add(tag);

        await dbContext.SaveChangesAsync();

        TagDto tagDto = tag.ToDto();

        if (acceptHeaderDto.IncludLinks)
        {
            tagDto.Links = CreateLinksForTag(tagDto.Id);
        }

        return CreatedAtAction(nameof(GetTag), new { id = tagDto.Id}, tagDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateTag(string id, UpdateTagDto updateTagDto)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }
        
        Tag? tag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (tag is null)
        {
            return NotFound();
        }

        tag.UpdateFromDto(updateTagDto);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTag(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }
        
        Tag? tag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (tag is null)
        {
            return NotFound();
        }

        dbContext.Tags.Remove(tag);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private List<LinkDto> CreateLinksForTags(int count, int maxAllowedTags)
    {
        List<LinkDto> links = 
        [
            linkService.Create(nameof(GetTags), "self", HttpMethods.Get),
        ];

        if (count < maxAllowedTags)
        {
            links.Add(linkService.Create(nameof(CreateTag), "create", HttpMethods.Post));
        }

        return links;
    }

    private List<LinkDto> CreateLinksForTag(string id)
    {
        List<LinkDto> links =
        [
            linkService.Create(nameof(GetTag), "self", HttpMethods.Get, new { id }),
            linkService.Create(nameof(UpdateTag), "update", HttpMethods.Put, new { id }),
            linkService.Create(nameof(DeleteTag), "delete", HttpMethods.Delete, new { id })
        ];

        return links;
    }
}
