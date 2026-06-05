using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NCMS.Backend.Identity.Authorization.Jwt
{
    public static class JwtAuthenticationExtensions
    {
        internal static IServiceCollection ConfigureJwtAuth(this IServiceCollection services)
        {
            services.AddOptions<JwtOptions>()
                .BindConfiguration(nameof(JwtOptions))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        }
    }
}