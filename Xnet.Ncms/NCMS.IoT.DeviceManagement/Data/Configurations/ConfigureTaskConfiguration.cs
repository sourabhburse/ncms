using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ConfigureTaskConfiguration : IEntityTypeConfiguration<ConfigureTask>
{
    public void Configure(EntityTypeBuilder<ConfigureTask> builder)
    {
        builder.ToTable("configure_tasks", "config");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TaskNumber).IsRequired().HasMaxLength(64);
        builder.Property(t => t.ProfileName).IsRequired().HasMaxLength(256);
        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.Status).HasConversion<int>().IsRequired();
        builder.Property(t => t.ConfigTimeout).HasColumnType("interval");
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.UseXminConcurrencyToken();

        builder.HasOne(t => t.Profile)
            .WithMany(p => p.ConfigureTasks)
            .HasForeignKey(t => t.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Status)
            .HasFilter($"\"Status\" = {(int)ConfigureTaskStatus.NotStarted}")
            .HasDatabaseName("ix_configure_tasks_pending");

        builder.HasIndex(t => t.ProfileId)
            .HasDatabaseName("ix_configure_tasks_profile_id");
    }
}
