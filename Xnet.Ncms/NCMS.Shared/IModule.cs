using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NCMS.Shared;

public interface IModule
{
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder app) { }

    Task InitializeAsync(IServiceProvider services, CancellationToken ct = default) => Task.CompletedTask;
}
