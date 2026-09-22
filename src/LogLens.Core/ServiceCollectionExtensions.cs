using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LogLens.Core;

/// <summary>
/// Registrierung der Kernbausteine. Die Bibliothek bleibt frei von UI- und I/O-Details.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLogLensCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new ParserOptions());

        return services;
    }
}
