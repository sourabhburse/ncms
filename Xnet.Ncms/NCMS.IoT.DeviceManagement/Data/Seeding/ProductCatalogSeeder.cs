using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Seeding;

public static class ProductCatalogSeeder
{
    public static async Task SeedAsync(DeviceManagementDbContext db, CancellationToken ct = default)
    {
        if (await db.ProductCategories.IgnoreQueryFilters().AnyAsync(ct))
            return;

        var catNetwork = ProductCategory.Create("Network Equipment");
        var catIndustrial = ProductCategory.Create("Industrial Controllers");

        db.ProductCategories.AddRange(catNetwork, catIndustrial);
        await db.SaveChangesAsync(ct);

        var typeRouters = ProductType.Create("Routers", catNetwork.Id);
        var typeModbus  = ProductType.Create("Modbus Gateways", catIndustrial.Id);

        db.ProductTypes.AddRange(typeRouters, typeModbus);
        await db.SaveChangesAsync(ct);

        db.Products.AddRange(
            Product.Create(
                name: "Niseva XE33-2S",
                productTypeId: typeRouters.Id,
                supportsFirmwareUpgrade: true,
                supportsCommands: true,
                supportsRealtimeLogs: true,
                supportsTelemetry: true),
            Product.Create(
                name: "Generic Modbus Gateway MB100",
                productTypeId: typeModbus.Id,
                supportsFirmwareUpgrade: true,
                supportsTelemetry: true)
        );

        await db.SaveChangesAsync(ct);
    }
}
