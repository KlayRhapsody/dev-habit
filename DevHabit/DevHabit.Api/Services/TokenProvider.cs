using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DevHabit.Api.DTOs.Auth;
using DevHabit.Api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevHabit.Api.Services;

public sealed class TokenProvider(IOptions<JwtAuthOptions> jwtAuthOptions)
{
    private readonly JwtAuthOptions _jwtAuthOptions = jwtAuthOptions.Value;

    public AccessTokenDto Create(TokenRequest tokenRequest)
    {
        return new AccessTokenDto(GenerateAccessToken(tokenRequest), GenerateRefreshToken());
    }

    private string GenerateAccessToken(TokenRequest tokenRequest)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtAuthOptions.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new (JwtRegisteredClaimNames.Sub, tokenRequest.UserID),
            new (JwtRegisteredClaimNames.Email, tokenRequest.Email),
            new (JwtRegisteredClaimNames.Jti , Guid.NewGuid().ToString("N")),
            ..tokenRequest.Role.Select(role => new Claim(ClaimTypes.Role, role))
        ];

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtAuthOptions.ExpirationInMinutes),
            SigningCredentials = credentials,
            Issuer = _jwtAuthOptions.Issuer,
            Audience = _jwtAuthOptions.Audience,
        };

        var handler = new JsonWebTokenHandler();

        string token = handler.CreateToken(tokenDescriptor);

        return token;
    }

    private static string GenerateRefreshToken()
    {
        byte[] randomNumber = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(randomNumber);
    }
}
