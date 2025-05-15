using System.Net.Mime;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Github;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;

namespace DevHabit.Api.Controllers;

[Authorize(Roles = Roles.Member)]
[ApiController]
[Route("github")]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class GithubController(
    GitHubAccessTokenService gitHubAccessTokenService,
    RefitGitHubService gitHubService,
    UserContext userContext,
    LinkService linkService
) : ControllerBase
{
    /// <summary>
    /// Stores a GitHub personal access token for the current user
    /// </summary>
    /// <param name="storeGithubAccessTokenDto">The GitHub access token details</param>
    /// <param name="validator">Validator for the token storage request</param>
    /// <returns>No content on success</returns>
    [HttpPut("personal-access-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StoreAccessToken(
        StoreGithubAccessTokenDto storeGithubAccessTokenDto,
        IValidator<StoreGithubAccessTokenDto> validator)
    {
        await validator.ValidateAndThrowAsync(storeGithubAccessTokenDto);

        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await gitHubAccessTokenService.StoreAsync(userId, storeGithubAccessTokenDto);

        return NoContent();
    }

    /// <summary>
    /// Revokes the stored GitHub personal access token for the current user
    /// </summary>
    /// <returns>No content on success</returns>
    [HttpDelete("personal-access-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAccessToken()
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await gitHubAccessTokenService.RevokeAsync(userId);

        return NoContent();
    }

    /// <summary>
    /// Retrieves the GitHub profile of the current user
    /// </summary>
    /// <param name="acceptHeader">Controls HATEOAS link generation</param>
    /// <returns>The user's GitHub profile</returns>
    [HttpGet("profile")]
    [ProducesResponseType<GitHubUserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GitHubUserProfileDto>> GetUserProfile([FromHeader] AcceptHeaderDto acceptHeader)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }
        
        string? accessToken = await gitHubAccessTokenService.GetAsync(userId);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return NotFound();
        }

        GitHubUserProfileDto userProfile = await gitHubService.GetUserProfileAsync(accessToken);
        if (userProfile is null)
        {
            return NotFound();
        }

        if (acceptHeader.IncludLinks)
        {
            userProfile.Links = 
            [
                linkService.Create(nameof(GetUserProfile), "self", HttpMethods.Get),
                linkService.Create(nameof(StoreAccessToken), "store-token", HttpMethods.Put),
                linkService.Create(nameof(DeleteAccessToken), "revoke-token", HttpMethods.Delete)
            ];
        }

        return Ok(userProfile);
    }

    /// <summary>
    /// Retrieves the GitHub events for the current user
    /// </summary>
    /// <returns>List of GitHub events</returns>
    [HttpGet("events")]
    [ProducesResponseType<IReadOnlyList<GitHubEventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GitHubEventDto>>> GetUserEvents()
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        string? accessToken = await gitHubAccessTokenService.GetAsync(userId);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Unauthorized();
        }

        GitHubUserProfileDto profile = await gitHubService.GetUserProfileAsync(accessToken);

        if (profile is null)
        {
            return NotFound();
        }

        IReadOnlyList<GitHubEventDto>? events = await gitHubService.GetUserEventsAsync(
            username: profile.Login,
            accessToken: accessToken);

        if (events is null || !events.Any())
        {
            return NotFound();
        }

        return Ok(events);
    }
}
