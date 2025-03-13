using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHabit.Api.Database.Configurations;

public sealed class HabitConfigurations : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasMaxLength(500);

        builder.Property(h => h.UserId).HasMaxLength(500);

        builder.Property(h => h.Name).HasMaxLength(100);

        builder.Property(h => h.Description).HasMaxLength(500);

        builder.OwnsOne(h => h.Frequency);

        builder.OwnsOne(h => h.Target, targetBuilder =>
        {
            targetBuilder.Property(t => t.Unit).HasMaxLength(100);
        });

        builder.OwnsOne(h => h.Milestone);

        // 讓 EF Core 知道 Habit 和 Tag 的關係應該透過 HabitTag 來處理
        // 而不是直接讓 EF Core 自動建立一張隱藏的關聯表。
        builder.HasMany(h => h.Tags)
            .WithMany()
            .UsingEntity<HabitTag>();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(h => h.UserId);
    }
}
