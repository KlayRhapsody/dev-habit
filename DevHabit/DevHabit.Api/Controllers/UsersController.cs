using System.Net.Mime;
using System.Security.Claims;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Users;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;

namespace DevHabit.Api.Controllers;

[Authorize(Roles = Roles.Member)]
[ApiController]
[Route("users")]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class UsersController(
    ApplicationDbContext dbContext,
    UserContext userContext,
    LinkService linkService) : ControllerBase
{

    /// <summary>
    /// Get a user by their ID (Admin only)
    /// </summary>
    /// <param name="id">The user's unique identifier</param>
    /// <returns>The user details</returns>
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUserById(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (id != userId)
        {
            return Forbid();
        }
        
        UserDto? user = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(UserQueries.ProjectToDto())
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    ///  Gets the currently authenticated user's profile
    /// </summary>
    /// <param name="acceptHeader">Controls HATEOAS link generation</param>
    /// <returns>The current user's details</returns>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetCurrentUser([FromHeader] AcceptHeaderDto acceptHeader)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        UserDto? user = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(UserQueries.ProjectToDto())
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return NotFound();
        }

        if (acceptHeader.IncludLinks)
        {
            user.Links = CreateLinksForUser();
        }

        return Ok(user);
    }

    /// <summary>
    /// Updates the current user's profile information
    /// </summary>
    /// <param name="updateUserProfileDto">The profile update details</param>
    /// <param name="validator">Validator for the update request</param>
    /// <returns>No content on success</returns>
    [HttpPut("me/profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateUserProfile(
        UpdateUserProfileDto updateUserProfileDto,
        IValidator<UpdateUserProfileDto> validator)
    {
        await validator.ValidateAndThrowAsync(updateUserProfileDto);
        
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        User? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return NotFound();
        }

        user.Name = updateUserProfileDto.Name;
        user.UpdatedAtUTC = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private List<LinkDto> CreateLinksForUser()
    {
        List<LinkDto> links = 
        [
            linkService.Create(nameof(GetCurrentUser), "self", HttpMethods.Get),
            linkService.Create(nameof(UpdateUserProfile), "update-profile", HttpMethods.Put),
        ];
        
        return links;
    }
}
