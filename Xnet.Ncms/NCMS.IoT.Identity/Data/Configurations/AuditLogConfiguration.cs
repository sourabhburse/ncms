using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.Identity.Entities;

namespace NCMS.IoT.Identity.Data.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", "identity");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.EventType).HasConversion<string>().HasMaxLength(64);
        builder.Property(a => a.SubjectDisplay).HasMaxLength(256);
        builder.Property(a => a.ActorDisplay).HasMaxLength(256);
        builder.Property(a => a.Description).IsRequired().HasMaxLength(1024);
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => a.EventType);
        builder.HasIndex(a => a.SubjectUserId);
    }
}
