
using System.Security.Cryptography;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Github;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevHabit.UnitTests.Services;

public sealed class GitHubAccessTokenServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EncryptionService _encryptionService;
    private readonly GitHubAccessTokenService _githubAccessTokenService;

    public GitHubAccessTokenServiceTests()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);

        IOptions<EncryptionOptions> encryptionOptions = Options.Create(new EncryptionOptions
        {
            Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        });

        _encryptionService = new EncryptionService(encryptionOptions);

        _githubAccessTokenService = new GitHubAccessTokenService(_dbContext, _encryptionService);
    }
    
    [Fact]
    public async Task StoreAsync_ShouldCreateNewToken_WhenUserDoesNotHaveOne()
    {
        const string userId = "user123";
        var dto = new StoreGithubAccessTokenDto
        {
            AccessToken = "sample_token",
            ExpiresInDays = 30
        };

        await _githubAccessTokenService.StoreAsync(userId, dto);

        GithubAccessToken? token = await _dbContext.GithubAccessTokens
            .Where(x => x.UserId == userId)
            .SingleOrDefaultAsync();

        Assert.NotNull(token);
        Assert.Equal(userId, token.UserId);
        Assert.NotEqual(dto.AccessToken, token.Token);
        Assert.True(token.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task StoreAsync_ShouldUpdateNewToken_WhenUserDoesHaveOne()
    {
        const string userId = "user123";
        var existingToken = new GithubAccessToken
        {
            Id = GithubAccessToken.NewId(),
            UserId = userId,
            Token = "old_token",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.GithubAccessTokens.Add(existingToken);
        await _dbContext.SaveChangesAsync();

        var dto = new StoreGithubAccessTokenDto
        {
            AccessToken = "new-token",
            ExpiresInDays = 30
        };

        _dbContext.ChangeTracker.Clear();
        await _githubAccessTokenService.StoreAsync(userId, dto);

        GithubAccessToken? token = await _dbContext.GithubAccessTokens
            .Where(x => x.UserId == userId)
            .SingleOrDefaultAsync();

        Assert.NotNull(token);
        Assert.Equal(existingToken.UserId, token.UserId);
        Assert.NotEqual(existingToken.Token, token.Token);
        Assert.True(token.ExpiresAtUtc > existingToken.ExpiresAtUtc);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnDecryptedToken_WhenTokenExists()
    {
        const string userId = "user123";
        const string originalToken = "sample_token";
        var existingToken = new GithubAccessToken
        {
            Id = GithubAccessToken.NewId(),
            UserId = userId,
            Token = _encryptionService.Encrypt(originalToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.GithubAccessTokens.Add(existingToken);
        await _dbContext.SaveChangesAsync();

        string? token = await _githubAccessTokenService.GetAsync(userId);

        Assert.Equal(originalToken, token);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenTokenDoesNotExist()
    {
        const string userId = "user123";

        string? token = await _githubAccessTokenService.GetAsync(userId);

        Assert.Null(token);
    }

    [Fact]
    public async Task RevokeAsync_ShouldRemoveToken_WhenTokenExists()
    {
        const string userId = "user123";
        const string originalToken = "sample_token";
        var existingToken = new GithubAccessToken
        {
            Id = GithubAccessToken.NewId(),
            UserId = userId,
            Token = _encryptionService.Encrypt(originalToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.GithubAccessTokens.Add(existingToken);
        await _dbContext.SaveChangesAsync();

        await _githubAccessTokenService.RevokeAsync(userId);
        
        Assert.False(await _dbContext.GithubAccessTokens.AnyAsync(t => t.UserId == userId));
    }

    [Fact]
    public async Task RevokeAsync_ShouldNotThrow_WhenTokenDoesNotExist()
    {
        // Arrange
        const string userId = "user123";

        // Act & Assert
        await _githubAccessTokenService.RevokeAsync(userId);
        Assert.False(await _dbContext.GithubAccessTokens.AnyAsync(t => t.UserId == userId));
    }
    

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
