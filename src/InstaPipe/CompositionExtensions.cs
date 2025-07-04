using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace InstaPipe;

/// <summary>
/// Provides extension methods for registering pipeline steps and runners with dependency injection.
/// </summary>
public static class CompositionExtensions
{
    private static readonly Type loggerFactoryType = typeof(ILoggerFactory);

    /// <summary>
    /// Registers all pipeline steps implementing <see cref="IPipelineStep{T}"/> discovered in all currently loaded assemblies.
    /// </summary>
    /// <typeparam name="T">The context type for the pipeline</typeparam>
    /// <param name="services">The DI container</param>
    /// <param name="environment">Optional environment filter (e.g., "Development", "Production")</param>
    /// <param name="lifetime">The service lifetime to apply (default: Scoped)</param>
    public static IServiceCollection AddPipeline<T>(
        this IServiceCollection services,
        string? environment = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        return AddPipeline<T>(services, AppDomain.CurrentDomain.GetAssemblies(), environment, lifetime);
    }

    /// <summary>
    /// Registers pipeline steps from the assembly containing the specified marker type.
    /// </summary>
    /// <typeparam name="T">The context type for the pipeline</typeparam>
    /// <param name="services">The DI container</param>
    /// <param name="typeMarker">A type located in the target assembly</param>
    /// <param name="environment">Optional environment filter</param>
    /// <param name="lifetime">The service lifetime to apply</param>
    public static IServiceCollection AddPipeline<T>(
        this IServiceCollection services,
        Type typeMarker,
        string? environment = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        return AddPipeline<T>(services, new[] { typeMarker.Assembly }, environment, lifetime);
    }

    /// <summary>
    /// Registers pipeline steps from a specific assembly.
    /// </summary>
    /// <typeparam name="T">The context type for the pipeline</typeparam>
    /// <param name="services">The DI container</param>
    /// <param name="assembly">The assembly to scan for pipeline steps</param>
    /// <param name="environment">Optional environment filter</param>
    /// <param name="lifetime">The service lifetime to apply</param>
    public static IServiceCollection AddPipeline<T>(
        this IServiceCollection services,
        Assembly assembly,
        string? environment = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        return AddPipeline<T>(services, new[] { assembly }, environment, lifetime);
    }

    /// <summary>
    /// Registers pipeline steps from a collection of assemblies.
    /// </summary>
    /// <typeparam name="T">The context type for the pipeline</typeparam>
    /// <param name="services">The DI container</param>
    /// <param name="assemblies">The assemblies to scan for pipeline steps</param>
    /// <param name="environment">Optional environment filter</param>
    /// <param name="lifetime">The service lifetime to apply</param>
    public static IServiceCollection AddPipeline<T>(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        string? environment = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ILoggerFactory? loggerFactory = null;

        if (services.Any(s => s.ServiceType == loggerFactoryType))
        {
            var provider = services.BuildServiceProvider();
            loggerFactory = provider.GetService<ILoggerFactory>();
        }

        var descriptors = Pipeline.Discover<T>(assemblies, environment, loggerFactory);
        Pipeline.Register<T>(services, descriptors, lifetime, loggerFactory);

        return services;
    }
}
