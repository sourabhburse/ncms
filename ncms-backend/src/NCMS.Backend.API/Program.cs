using NCMS.Backend.Core.Provisioning;
using NCMS.Backend.Infrastructure.Pki;
using NCMS.Backend.Infrastructure.Provisioning;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ProvisioningOptions>(builder.Configuration.GetSection("Provisioning"));
builder.Services.AddSingleton<IDeviceCertificateIssuer, PersistentDeviceCertificateIssuer>();
builder.Services.AddSingleton<IProvisioningService, ProvisioningService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
