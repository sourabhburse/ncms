using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class UpgradeTaskConfiguration : IEntityTypeConfiguration<UpgradeTask>
{
    public void Configure(EntityTypeBuilder<UpgradeTask> builder)
    {
        builder.ToTable("upgrade_tasks", "firmware");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(256);
        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.Status).HasConversion<int>().IsRequired();
        builder.Property(t => t.UpgradeTimeout).HasColumnType("interval");
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.UseXminConcurrencyToken();

        builder.HasOne(t => t.Firmware)
            .WithMany(f => f.UpgradeTasks)
            .HasForeignKey(t => t.FirmwareId)
            .OnDelete(DeleteBehavior.Restrict);

        // Common filter: find all NotStarted tasks awaiting dispatch.
        builder.HasIndex(t => t.Status)
            .HasFilter($"\"Status\" = {(int)UpgradeTaskStatus.NotStarted}")
            .HasDatabaseName("ix_upgrade_tasks_pending");

        // Support listing tasks by firmware package.
        builder.HasIndex(t => t.FirmwareId)
            .HasDatabaseName("ix_upgrade_tasks_firmware_id");
    }
}
