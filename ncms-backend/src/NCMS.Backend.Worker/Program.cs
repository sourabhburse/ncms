using Microsoft.EntityFrameworkCore;
using NCMS.Backend.Infrastructure.Data;
using NCMS.Backend.Worker;
using Npgsql;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var connectionString = hostContext.Configuration.GetConnectionString("DefaultConnection");
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<NcmsDbContext>(options =>
            options.UseNpgsql(dataSource));

        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();
