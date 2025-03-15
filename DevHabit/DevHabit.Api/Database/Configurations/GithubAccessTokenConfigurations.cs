using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHabit.Api.Database.Configurations;

public class GithubAccessTokenConfigurations : IEntityTypeConfiguration<GithubAccessToken>
{
    public void Configure(EntityTypeBuilder<GithubAccessToken> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasMaxLength(500);
        builder.Property(g => g.UserId).HasMaxLength(500);
        builder.Property(g => g.Token).HasMaxLength(1000);

        builder.HasIndex(g => g.UserId).IsUnique();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<GithubAccessToken>(g => g.UserId);
    }
}
