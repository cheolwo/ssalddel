using Ssalddel.Services.WorldProjection.SpatialCatalog;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpatialCatalog(this IServiceCollection services)
    {
        services.AddSingleton<I공간자료CatalogStore, Mongo공간자료CatalogStore>();
        services.AddScoped<공간자료CatalogService>();
        return services;
    }
}
