using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Database;

public sealed class ApplicationIdentityDbContext(DbContextOptions<ApplicationIdentityDbContext> options)
    : IdentityDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema.Identity);

        builder.Entity<IdentityUser>().ToTable("aps_net_users");
        builder.Entity<IdentityRole>().ToTable("aps_net_roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("aps_net_user_roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("aps_net_user_claims");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("aps_net_role_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("aps_net_user_logins");
        builder.Entity<IdentityUserToken<string>>().ToTable("aps_net_user_tokens");
    }
}
