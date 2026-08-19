using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace NCMS.IoT.DeviceManagement.Data;

public sealed class DeviceManagementDbContextFactory : IDesignTimeDbContextFactory<DeviceManagementDbContext>
{
    public DeviceManagementDbContext CreateDbContext(string[] args)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(
            "Host=localhost;Database=ncms_iot;Username=postgres;Password=12345");
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        var options = new DbContextOptionsBuilder<DeviceManagementDbContext>()
            .UseNpgsql(
                dataSource,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "device_management"))
            // Mirrors the runtime registration in DeviceManagementModule — see the comment there
            // for why this specific, reviewed warning is suppressed rather than acted on.
            .ConfigureWarnings(w =>
                w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;
        return new DeviceManagementDbContext(options);
    }
}
