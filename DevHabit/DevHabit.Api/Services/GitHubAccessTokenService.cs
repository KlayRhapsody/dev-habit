
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Github;
using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Services;

public sealed class GitHubAccessTokenService(ApplicationDbContext dbContext, EncryptionService encryptionService)
{
    public async Task StoreAsync(
        string userId,
        StoreGithubAccessTokenDto storeGithubAccessTokenDto,
        CancellationToken cancellationToken = default)
    {
        GithubAccessToken? accessToken = await GetAccessTokenAsync(userId, cancellationToken);

        string encryptedToken = encryptionService.Encrypt(storeGithubAccessTokenDto.AccessToken);

        if (accessToken is null)
        {
            var githubAccessToken = new GithubAccessToken
            {
                Id = $"gh_{Guid.CreateVersion7()}",
                UserId = userId,
                Token = encryptedToken,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(storeGithubAccessTokenDto.ExpiresInDays),
            };

            dbContext.GithubAccessTokens.Add(githubAccessToken);

        }
        else
        {
            accessToken.Token = encryptedToken;
            accessToken.ExpiresAtUtc = DateTime.UtcNow.AddDays(storeGithubAccessTokenDto.ExpiresInDays);
        }
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        GithubAccessToken? accessToken = await GetAccessTokenAsync(userId, cancellationToken);

        if (accessToken is null)
        {
            return null;
        }
        
        string encryptedToken = encryptionService.Decrypt(accessToken.Token);

        return encryptedToken;
    }

    public async Task RevokeAsync(string userId, CancellationToken cancellationToken = default)
    {
        GithubAccessToken? accessToken = await GetAccessTokenAsync(userId, cancellationToken);

        if (accessToken is null)
        {
            return;
        }

        dbContext.GithubAccessTokens.Remove(accessToken);
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<GithubAccessToken?> GetAccessTokenAsync(string userId, CancellationToken cancellationToken)
    {
        return await dbContext.GithubAccessTokens
            .SingleOrDefaultAsync(gh => gh.UserId == userId, cancellationToken);
    }
}
