using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Auth;
using DevHabit.Api.DTOs.Users;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Settings;
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
public sealed class AuthController(
    UserManager<IdentityUser> userManager,
    ApplicationDbContext appDbContext,
    ApplicationIdentityDbContext appIdentityDbContext,
    TokeProvider tokenProvider,
    IOptions<JwtAuthOptions> options) : ControllerBase
{
    private readonly JwtAuthOptions _jwtAuthOptions = options.Value;

    [HttpPost("register")]
    public async Task<ActionResult<AccessTokenDto>> Register(RegisterUserDto registerUserDto)
    {
        using IDbContextTransaction transaction = await appIdentityDbContext.Database.BeginTransactionAsync();
        appDbContext.Database.SetDbConnection(appIdentityDbContext.Database.GetDbConnection());
        await appDbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        // create identity user
        var identityUser = new IdentityUser
        {
            Email = registerUserDto.Email,
            UserName = registerUserDto.Email
        };
        IdentityResult identityResult = await userManager.CreateAsync(identityUser, registerUserDto.Password);

        if (!identityResult.Succeeded)
        {
            var extensions = new Dictionary<string, object?>
            {
                {
                    "errors",
                    identityResult.Errors.ToDictionary(e => e.Code, e => e.Description)
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

        var tokenRequest = new TokenRequest(identityUser.Id, identityUser.Email);
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

    [HttpPost("login")]
    public async Task<ActionResult<AccessTokenDto>> Login(LoginUserDto loginUserDto)
    {
        IdentityUser? identityUser = await userManager.FindByEmailAsync(loginUserDto.Email);

        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, loginUserDto.Password))
        {
            return Unauthorized();
        }

        var tokenRequest = new TokenRequest(identityUser.Id, identityUser.Email!);
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

    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokenDto>> Refresh(RefreshTokenDto refreshTokenDto)
    {
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

        var tokenRequest = new TokenRequest(refreshToken.User.Id, refreshToken.User.Email!);
        AccessTokenDto accessTokenDto = tokenProvider.Create(tokenRequest);
        
        refreshToken.Token = accessTokenDto.RefreshToken;
        refreshToken.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtAuthOptions.RefreshTokenExpirationInDays);

        await appIdentityDbContext.SaveChangesAsync();

        return Ok(accessTokenDto);
    }
}
