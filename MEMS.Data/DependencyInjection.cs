using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace MEMS.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the tenant context and the service layer. The host registers <see cref="AppDbContext"/> itself
    /// (connection string from Aspire) and fills in <see cref="TenantContext"/> per request.
    /// </summary>
    public static IServiceCollection AddMemsData(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<AppDbContextFactory>();

        services.AddScoped<IMunicipalityService, MunicipalityService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<ICandidateService, CandidateService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
