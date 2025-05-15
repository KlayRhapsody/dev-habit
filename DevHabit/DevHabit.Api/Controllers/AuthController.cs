using System.Net.Mime;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Auth;
using DevHabit.Api.DTOs.Users;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Settings;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("auth")]
[AllowAnonymous]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1)]
public sealed class AuthController(
    UserManager<IdentityUser> userManager,
    ApplicationDbContext appDbContext,
    ApplicationIdentityDbContext appIdentityDbContext,
    TokenProvider tokenProvider,
    IOptions<JwtAuthOptions> options) : ControllerBase
{
    private readonly JwtAuthOptions _jwtAuthOptions = options.Value;

    /// <summary>
    /// Registers a new user account
    /// </summary>
    /// <param name="registerUserDto">The registration details</param>
    /// <param name="validator">Validator for the registration request</param>
    /// <returns>Access tokens for the newly registered user</returns>
    [HttpPost("register")]
    [ProducesResponseType<AccessTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccessTokenDto>> Register(
        RegisterUserDto registerUserDto,
        IValidator<RegisterUserDto> validator)
    {
        await validator.ValidateAndThrowAsync(registerUserDto);

        using IDbContextTransaction transaction = await appIdentityDbContext.Database.BeginTransactionAsync();
        appDbContext.Database.SetDbConnection(appIdentityDbContext.Database.GetDbConnection());
        await appDbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        // create identity user
        var identityUser = new IdentityUser
        {
            Email = registerUserDto.Email,
            UserName = registerUserDto.Email
        };
        IdentityResult createUserResult = await userManager.CreateAsync(identityUser, registerUserDto.Password);
        if (!createUserResult.Succeeded)
        {
            var extensions = new Dictionary<string, object?>
            {
                {
                    "errors",
                    createUserResult.Errors.ToDictionary(e => e.Code, e => e.Description)
                }
            };
            return Problem(
                detail: "Unable to register user, please try again",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: extensions
            );
        }

        IdentityResult addToRoleResult = await userManager.AddToRoleAsync(identityUser, Roles.Member);
        if (!addToRoleResult.Succeeded)
        {
            var extensions = new Dictionary<string, object?>
            {
                {
                    "errors",
                    addToRoleResult.Errors.ToDictionary(e => e.Code, e => e.Description)
                }
            };
            return Problem(
                detail: "Unable to register user, please try again",
                extensions: extensions
            );
        }

        // create application user
        User user = registerUserDto.ToEntity();
        user.IdentityId = identityUser.Id;

        appDbContext.Users.Add(user);
        
        await appDbContext.SaveChangesAsync();

        var tokenRequest = new TokenRequest(identityUser.Id, identityUser.Email, [Roles.Member]);
        AccessTokenDto accessTokenDto = tokenProvider.Create(tokenRequest);
        
        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = identityUser.Id,
            Token = accessTokenDto.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtAuthOptions.RefreshTokenExpirationInDays)
        };
        appIdentityDbContext.RefreshTokens.Add(refreshToken);

        await appIdentityDbContext.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok(accessTokenDto);
    }

    /// <summary>
    /// Authenticates a user and returns access tokens
    /// </summary>
    /// <param name="loginUserDto">The login credentials</param>
    /// <param name="validator">Validator for the login request</param>
    /// <returns>Access tokens for the authenticated user</returns>
    [HttpPost("login")]
    [ProducesResponseType<AccessTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccessTokenDto>> Login(
        LoginUserDto loginUserDto,
        IValidator<LoginUserDto> validator)
    {
        await validator.ValidateAndThrowAsync(loginUserDto);
        
        IdentityUser? identityUser = await userManager.FindByEmailAsync(loginUserDto.Email);

        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, loginUserDto.Password))
        {
            return Unauthorized();
        }

        IList<string> roles = await userManager.GetRolesAsync(identityUser);
        var tokenRequest = new TokenRequest(identityUser.Id, identityUser.Email!, roles);
        AccessTokenDto accessTokenDto = tokenProvider.Create(tokenRequest);

        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = identityUser.Id,
            Token = accessTokenDto.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtAuthOptions.RefreshTokenExpirationInDays)
        };
        appIdentityDbContext.RefreshTokens.Add(refreshToken);

        await appIdentityDbContext.SaveChangesAsync();

        return Ok(accessTokenDto);
    }

    /// <summary>
    /// Refreshes the access token using a refresh token
    /// </summary>
    /// <param name="refreshTokenDto">The refresh token</param>
    /// <param name="validator">Validator for the refresh token request</param>
    /// <returns>New access tokens</returns>
    [HttpPost("refresh")]
    [ProducesResponseType<AccessTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccessTokenDto>> Refresh(
        RefreshTokenDto refreshTokenDto,
        IValidator<RefreshTokenDto> validator)
    {
        await validator.ValidateAndThrowAsync(refreshTokenDto);
        
        RefreshToken? refreshToken = await appIdentityDbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenDto.RefreshToken);

        if (refreshToken is null)
        {
            return Unauthorized();
        }

        if (refreshToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Unauthorized();
        }

        IList<string> roles = await userManager.GetRolesAsync(refreshToken.User);
        var tokenRequest = new TokenRequest(refreshToken.User.Id, refreshToken.User.Email!, roles);
        AccessTokenDto accessTokenDto = tokenProvider.Create(tokenRequest);
        
        refreshToken.Token = accessTokenDto.RefreshToken;
        refreshToken.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtAuthOptions.RefreshTokenExpirationInDays);

        await appIdentityDbContext.SaveChangesAsync();

        return Ok(accessTokenDto);
    }
}
