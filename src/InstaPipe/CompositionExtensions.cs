using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace InstaPipe;

/// <summary>
/// Provides extensions for registering pipeline steps and runners with dependency injection
/// </summary>
public static class CompositionExtensions
{
    /// <summary>
    /// Scans the current application domain for types that implement IPipelineStep&lt;T&gt;, 
    /// reads their metadata from PipelineStepAttribute, and registers them in the correct order.
    /// </summary>
    /// <typeparam name="T">The pipeline context type</typeparam>
    /// <param name="services">The service collection to register into</param>
    /// <param name="environment">The active environment name, used to filter environment-specific steps</param>
    /// <param name="lifetime">The lifetime to use when registering pipeline steps and the runner</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection AddPipeline<T>(
        this IServiceCollection services,
        string? environment = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var stepInterface = typeof(IPipelineStep<T>);
        var runnerInterface = typeof(IPipelineRunner<T>);

        // Prevent duplicate registration
        if (services.Any(s => s.ServiceType == runnerInterface))
            return services;

        var descriptors = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => !t.IsAbstract && !t.IsInterface && stepInterface.IsAssignableFrom(t))
            .Select(t => new PipelineStepDescriptor(t))
            .Where(d => d.IsEnabled)
            .Where(d => d.Environment is null || d.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Order)
            .ToList();

        foreach (var descriptor in descriptors)
        {
            var serviceDescriptor = ServiceDescriptor.Describe(stepInterface, descriptor.ImplementationType, lifetime);
            services.TryAddEnumerable(serviceDescriptor);
        }

        services.TryAdd(ServiceDescriptor.Describe(runnerInterface, typeof(PipelineRunner<T>), lifetime));

        return services;
    }

    /// <summary>
    /// Attempts to retrieve all loadable types from the given assembly, skipping those that throw
    /// </summary>
    /// <param name="assembly">The assembly to inspect</param>
    /// <returns>A collection of loadable types</returns>
    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
        catch
        {
            return Enumerable.Empty<Type>();
        }
    }
}
