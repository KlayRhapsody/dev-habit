using System.Security.Claims;
using DevHabit.Api.Database;
using DevHabit.Api.Entities;
using DevHabit.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DevHabit.Api.Services;

public sealed class UserContext(
    IHttpContextAccessor httpContextAccessor,
    ApplicationDbContext appDbContext,
    IMemoryCache memoryCache)
{
    private const string CacheKeyPrefix = "users:id:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private static readonly SemaphoreSlim SemaphoreSlim = new(1, 1);

    public async Task<string?> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        string? identityId = httpContextAccessor.HttpContext?.User.GetIdentityId();

        if (identityId is null)
        {
            return null;
        }

        string cacheKey = $"{CacheKeyPrefix}{identityId}";

        string? userId = await memoryCache.GetOrCreateAsync(cacheKey, async entity =>
        {
            entity.SlidingExpiration = CacheDuration;

            string? userId = await appDbContext.Users
                .Where(u => u.IdentityId == identityId)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(userId))
            {
                try
                {
                    await SemaphoreSlim.WaitAsync(cancellationToken);

                    userId = await appDbContext.Users
                        .Where(u => u.IdentityId == identityId)
                        .Select(u => u.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        var claims = httpContextAccessor.HttpContext!.User.Claims.ToList();
                        string? email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                            ?? claims.FirstOrDefault(c => c.Type == "email")?.Value;
                        string? name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
                            ?? claims.FirstOrDefault(c => c.Type == "name")?.Value;

                        if (email is not null && name is not null)
                        {
                            var user = new User
                            {
                                Id = User.NewId(),
                                Email = email,
                                Name = name,
                                IdentityId = identityId,
                                CreatedAtUTC = DateTime.UtcNow
                            };

                            appDbContext.Users.Add(user);
                            await appDbContext.SaveChangesAsync(cancellationToken);
                            userId = user.Id;
                        }
                    }
                }
                finally
                {
                    SemaphoreSlim.Release();
                }
            }

            return userId;
        });

        return userId;
    }
}
