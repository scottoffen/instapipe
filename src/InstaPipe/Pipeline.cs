using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using InstaPipe.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace InstaPipe;

/// <summary>
/// Provides reflection-based utilities for discovering and registering pipeline steps.
/// </summary>
public static class Pipeline
{
    /// <summary>
    /// Discovers all <see cref="IPipelineStep{T}"/> implementations in the assembly containing the specified type.
    /// </summary>
    public static IEnumerable<PipelineStepDescriptor> DiscoverFromType<T>(
        Type typeMarker,
        string? environment = null,
        ILogger? logger = null)
        => Discover<T>(new[] { typeMarker.Assembly }, environment, logger);

    /// <summary>
    /// Discovers all <see cref="IPipelineStep{T}"/> implementations in the assembly containing the specified type.
    /// </summary>
    public static IEnumerable<PipelineStepDescriptor> DiscoverFromType<T>(
        Type typeMarker,
        string? environment,
        ILoggerFactory? loggerFactory)
        => Discover<T>(new[] { typeMarker.Assembly }, environment, loggerFactory?.CreateLogger($"Pipeline<{typeof(T).Name}>"));

    /// <summary>
    /// Discovers all <see cref="IPipelineStep{T}"/> implementations in the specified assembly.
    /// </summary>
    public static IEnumerable<PipelineStepDescriptor> DiscoverFromAssembly<T>(
        Assembly assembly,
        string? environment = null,
        ILogger? logger = null)
        => Discover<T>(new[] { assembly }, environment, logger);

    /// <summary>
    /// Discovers all <see cref="IPipelineStep{T}"/> implementations in the specified assembly.
    /// </summary>
    public static IEnumerable<PipelineStepDescriptor> DiscoverFromAssembly<T>(
        Assembly assembly,
        string? environment,
        ILoggerFactory? loggerFactory)
        => Discover<T>(new[] { assembly }, environment, loggerFactory?.CreateLogger($"Pipeline<{typeof(T).Name}>"));

    /// <summary>
    /// Discovers all <see cref="IPipelineStep{T}"/> implementations in the provided assemblies.
    /// </summary>
    public static IEnumerable<PipelineStepDescriptor> Discover<T>(
        IEnumerable<Assembly>? assemblies = null,
        string? environment = null,
        ILogger? logger = null)
    {
        assemblies ??= AppDomain.CurrentDomain.GetAssemblies();
        var stepType = typeof(IPipelineStep<T>);

        var descriptors = assemblies
            .SelectMany(SafeGetTypes)
            .Where(t => !t.IsAbstract && !t.IsInterface && stepType.IsAssignableFrom(t))
            .Select(t => new PipelineStepDescriptor(t))
            .Where(d => d.IsEnabled)
            .Where(d => d.Environment is null || d.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Order)
            .ToList();

        if (logger is not null)
        {
            if (descriptors.Any())
            {
                logger.LogDebug("Discovered {Count} pipeline steps for {ContextType}.", descriptors.Count, typeof(T).Name);
                foreach (var descriptor in descriptors)
                {
                    logger.LogDebug(
                        "Discovered pipeline step {StepType} [Order={Order}, Env={Environment}, Enabled={IsEnabled}]",
                        descriptor.ImplementationType.FullName ?? descriptor.ImplementationType.Name,
                        descriptor.Order,
                        descriptor.Environment ?? "(none)",
                        descriptor.IsEnabled);
                }
            }
            else
            {
                logger.LogDebug("No pipeline steps found for {ContextType} in the specified assemblies.", typeof(T).Name);
            }
        }

        return descriptors;
    }

    /// <summary>
    /// Discovers steps using a logger generated from the supplied <see cref="ILoggerFactory"/>.
    /// </summary>
    public static IEnumerable<PipelineStepDescriptor> Discover<T>(
        IEnumerable<Assembly>? assemblies,
        string? environment,
        ILoggerFactory? loggerFactory)
        => Discover<T>(assemblies, environment, loggerFactory?.CreateLogger($"Pipeline<{typeof(T).Name}>"));

    /// <summary>
    /// Registers discovered pipeline steps and the pipeline runner into the DI container.
    /// </summary>
    public static void Register<T>(
        IServiceCollection services,
        IEnumerable<PipelineStepDescriptor> descriptors,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        ILogger? logger = null)
    {
        var stepInterface = typeof(IPipelineStep<T>);
        var lazyStepInterface = typeof(Lazy<>).MakeGenericType(stepInterface);
        var runnerType = typeof(IPipelineRunner<T>);

        if (services.Any(s => s.ServiceType == runnerType))
        {
            logger?.LogDebug("Pipeline runner for {ContextType} already registered. Skipping registration.", typeof(T).Name);
            return;
        }

        foreach (var descriptor in descriptors)
        {
            logger?.LogDebug("Registering pipeline step {StepType} with {Lifetime} lifetime",
                descriptor.ImplementationType.FullName ?? descriptor.ImplementationType.Name, lifetime);

            // Register the IPipelineStep<T> implementation
            services.TryAddEnumerable(ServiceDescriptor.Describe(stepInterface, descriptor.ImplementationType, lifetime));

            // Register the concrete implementation type
            services.Add(ServiceDescriptor.Describe(descriptor.ImplementationType, descriptor.ImplementationType, lifetime));

            // Register Lazy<IPipelineStep<T>>
            services.Add(ServiceDescriptor.Describe(lazyStepInterface, sp =>
            {
                var funcType = typeof(Func<>).MakeGenericType(stepInterface);

                var methodInfo = typeof(ServiceProviderServiceExtensions)
                    .GetMethod(nameof(ServiceProviderServiceExtensions.GetRequiredService), new[] { typeof(IServiceProvider) });

                if (methodInfo is null)
                    throw new InvalidOperationException("Could not find GetRequiredService<T>(IServiceProvider).");

                var genericMethod = methodInfo.MakeGenericMethod(descriptor.ImplementationType);

                var factoryDelegate = Delegate.CreateDelegate(funcType, sp, genericMethod);

                return Activator.CreateInstance(lazyStepInterface, factoryDelegate)
                    ?? throw new InvalidOperationException($"Could not create Lazy<{stepInterface}>.");
            }, lifetime));

        }

        logger?.LogDebug("Registering pipeline runner {RunnerType} with {Lifetime} lifetime",
            runnerType.Name, lifetime);

        services.TryAdd(ServiceDescriptor.Describe(runnerType, typeof(PipelineRunner<T>), lifetime));
    }

    /// <summary>
    /// Registers steps using a logger generated from the supplied <see cref="ILoggerFactory"/>.
    /// </summary>
    public static void Register<T>(
        IServiceCollection services,
        IEnumerable<PipelineStepDescriptor> descriptors,
        ServiceLifetime lifetime,
        ILoggerFactory? loggerFactory)
        => Register<T>(services, descriptors, lifetime, loggerFactory?.CreateLogger($"Pipeline<{typeof(T).Name}>"));

    /// <summary>
    /// Safely retrieves all loadable types from the specified assembly, skipping those that cannot be loaded.
    /// </summary>
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
