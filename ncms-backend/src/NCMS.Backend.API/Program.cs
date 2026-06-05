using Microsoft.EntityFrameworkCore;
using NCMS.Backend.Core.Entities;
using NCMS.Backend.Core.Provisioning;
using NCMS.Backend.Infrastructure.Data;
using NCMS.Backend.Infrastructure.Pki;
using NCMS.Backend.Infrastructure.Provisioning;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Register DbContext with PostgreSQL and enable dynamic JSON serialization
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<NcmsDbContext>(options =>
    options.UseNpgsql(dataSource));

builder.Services.Configure<ProvisioningOptions>(builder.Configuration.GetSection("Provisioning"));
builder.Services.AddSingleton<IDeviceCertificateIssuer, PersistentDeviceCertificateIssuer>();
builder.Services.AddScoped<IProvisioningService, ProvisioningService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("cors",
                      policy =>
                      {
                          policy.AllowAnyOrigin()
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Perform migration and seeding on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<NcmsDbContext>();
        context.Database.Migrate();
        SeedData(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();

static void SeedData(NcmsDbContext context)
{
    if (!context.Tenants.Any())
    {
        var tenant = new Tenant
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Niseva",
            Slug = "niseva",
            ContactEmail = "admin@niseva.com",
            IsActive = true
        };
        context.Tenants.Add(tenant);

        var vendor = new Vendor
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Name = "Niseva"
        };
        context.Vendors.Add(vendor);

        var product = new Product
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            VendorId = vendor.Id,
            ModelName = "XE-33",
            Architecture = "mips",
            ConfigFormat = "uci"
        };
        context.Products.Add(product);

        var inventory = new HardwareInventory
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
            TenantId = tenant.Id,
            ProductId = product.Id,
            SerialNumber = "SN-001",
            IdentityPolicy = "serial_only",
            IdentityClaims = new Dictionary<string, string?>
            {
                { "base_mac", "AA:BB:CC:DD:EE:01" }
            },
            Status = "PENDING_ACTIVATION"
        };
        context.HardwareInventory.Add(inventory);

        context.SaveChanges();
    }
}
