using System.Security.Claims;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Users;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;

namespace DevHabit.Api.Controllers;

[Authorize(Roles = Roles.Member)]
[ApiController]
[Route("users")]
public sealed class UsersController(
    ApplicationDbContext dbContext,
    UserContext userContext,
    LinkService linkService) : ControllerBase
{
    [Authorize(Roles = Roles.Admin)]
    [HttpGet("{id}")]
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

    [HttpGet("me")]
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

    [HttpPut("me/profile")]
    public async Task<ActionResult> UpdateUserProfile(UpdateUserProfileDto updateUserProfileDto)
    {
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
