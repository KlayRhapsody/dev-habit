
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DevHabit.Api.DTOs.Auth;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevHabit.UnitTests.Services;

public sealed class TokenProviderTests
{
    private readonly TokenProvider _tokenProvider;

    private readonly JwtAuthOptions _jwtAuthOptions;

    public TokenProviderTests()
    {
        _jwtAuthOptions = new JwtAuthOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Key = "your-secret-key-here-that-should-also-be-fairly-long",
            ExpirationInMinutes = 30,
            RefreshTokenExpirationInDays = 7
        };

        IOptions<JwtAuthOptions> options = Options.Create(_jwtAuthOptions);
        _tokenProvider = new TokenProvider(options);
    }

    [Fact]
    public void Create_ShouldReturnBothTokens()
    {
        var tokenRequest = new TokenRequest("user123", "test@example", [Roles.Member]);

        AccessTokenDto accessTokenDto = _tokenProvider.Create(tokenRequest);

        Assert.NotNull(accessTokenDto.AccessToken);
        Assert.NotNull(accessTokenDto.RefreshToken);
    }

    [Fact]
    public void Create_ShouldGenerateValidAccessToken()
    {
        var tokenRequest = new TokenRequest("user123", "test@example", [Roles.Member]);

        AccessTokenDto accessTokenDto = _tokenProvider.Create(tokenRequest);

        var handler = new JwtSecurityTokenHandler();
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtAuthOptions.Key)),
            ValidateIssuer = true,
            ValidIssuer = _jwtAuthOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtAuthOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        ClaimsPrincipal? claimsPrincipal = handler
            .ValidateToken(accessTokenDto.AccessToken, tokenValidationParameters, out SecurityToken validatedToken);

        Assert.NotNull(validatedToken);
        Assert.Equal(tokenRequest.UserID, claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(tokenRequest.Email, claimsPrincipal.FindFirstValue(ClaimTypes.Email));
        Assert.Contains(claimsPrincipal.FindAll(ClaimTypes.Role), claim => claim.Value == "Member");
    }

    [Fact]
    public void Create_ShouldGenerateUniqueRefreshTokens()
    {
        // Arrange
        var tokenRequest = new TokenRequest("user123", "test@example.com", [Roles.Member]);

        // Act
        AccessTokenDto result1 = _tokenProvider.Create(tokenRequest);
        AccessTokenDto result2 = _tokenProvider.Create(tokenRequest);

        // Assert
        Assert.NotEqual(result1.RefreshToken, result2.RefreshToken);
    }

    [Fact]
    public void Create_ShouldGenerateAccessTokenWithCorrectExpiration()
    {
        // Arrange
        var tokenRequest = new TokenRequest("user123", "test@example.com", [Roles.Member]);

        AccessTokenDto accessTokenDto = _tokenProvider.Create(tokenRequest);

        var handler = new JwtSecurityTokenHandler();

        JwtSecurityToken jwt = handler.ReadJwtToken(accessTokenDto.AccessToken);

        DateTime expectedExpiration = DateTime.UtcNow.AddMinutes(_jwtAuthOptions.ExpirationInMinutes);
        DateTime actualExpiration = jwt.ValidTo;
        Assert.True(Math.Abs((expectedExpiration - actualExpiration).TotalSeconds) < 5);
    }

    [Fact]
    public void Create_ShouldGenerateBase64RefreshToken()
    {
        // Arrange
        var tokenRequest = new TokenRequest("user123", "test@example.com", [Roles.Member]);

        AccessTokenDto accessTokenDto = _tokenProvider.Create(tokenRequest);

        Assert.True(IsBase64String(accessTokenDto.RefreshToken));
    }

    private static bool IsBase64String(string str)
    {
        Span<byte> buffer = new byte[str.Length];
        return Convert.TryFromBase64String(str, buffer, out _);
    }
}
