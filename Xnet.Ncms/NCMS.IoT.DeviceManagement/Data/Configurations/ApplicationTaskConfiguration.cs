using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ApplicationTaskConfiguration : IEntityTypeConfiguration<ApplicationTask>
{
    public void Configure(EntityTypeBuilder<ApplicationTask> builder)
    {
        builder.ToTable("application_tasks", "application");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(256);
        builder.Property(t => t.Action).HasConversion<int>().IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.Status).HasConversion<int>().IsRequired();
        builder.Property(t => t.Timeout).HasColumnType("interval");
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.UseXminConcurrencyToken();

        builder.HasOne(t => t.TargetApplicationPackageVersion)
            .WithMany()
            .HasForeignKey(t => t.TargetApplicationPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Common filter: find all NotStarted tasks awaiting dispatch.
        builder.HasIndex(t => t.Status)
            .HasFilter($"\"Status\" = {(int)ApplicationTaskStatus.NotStarted}")
            .HasDatabaseName("ix_application_tasks_pending");

        builder.HasIndex(t => t.TargetApplicationPackageVersionId)
            .HasDatabaseName("ix_application_tasks_target_version_id");
    }
}
