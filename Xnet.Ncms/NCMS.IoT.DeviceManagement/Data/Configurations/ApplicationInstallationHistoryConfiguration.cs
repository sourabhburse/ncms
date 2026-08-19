using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ApplicationInstallationHistoryConfiguration : IEntityTypeConfiguration<ApplicationInstallationHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationInstallationHistory> builder)
    {
        builder.ToTable("application_installation_history", "public");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Action).HasConversion<int>().IsRequired();
        builder.Property(h => h.Result).HasConversion<int>().IsRequired();
        builder.Property(h => h.Source).HasConversion<int>().IsRequired();
        builder.Property(h => h.OccurredAt).IsRequired();

        builder.HasOne(h => h.Device)
            .WithMany()
            .HasForeignKey(h => h.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ApplicationPackageVersion)
            .WithMany()
            .HasForeignKey(h => h.ApplicationPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.ApplicationTaskDevice)
            .WithMany()
            .HasForeignKey(h => h.ApplicationTaskDeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(h => new { h.DeviceId, h.OccurredAt })
            .HasDatabaseName("ix_application_installation_history_device_occurred");
    }
}
