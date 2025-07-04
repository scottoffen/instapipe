using System.Reflection;
using InstaPipe.Tests.Steps;
using InstaPipe.Tests.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InstaPipe.Tests;

public class PipelineTests
{
    private static readonly Assembly StepAssembly = typeof(StepA1).Assembly;

    [Fact]
    public void Pipeline_Discover_FromTypeMarker()
    {
        var logger = new TestLogger();
        var factory = new TestLoggerProvider();

        var descriptors_1 = Pipeline.DiscoverFromType<StepContext>(typeof(StepContext), null, logger);
        var descriptors_2 = Pipeline.DiscoverFromType<StepContext>(typeof(StepContext), null, factory);

        descriptors_1.ShouldNotBeNull();
        descriptors_1.ShouldNotBeEmpty();
        descriptors_1.Count().ShouldBe(1);
        descriptors_1.First().ImplementationType.ShouldBe(typeof(StepD));

        descriptors_2.ShouldNotBeNull();
        descriptors_2.ShouldNotBeEmpty();
        descriptors_2.Count().ShouldBe(1);
        descriptors_2.First().ImplementationType.ShouldBe(typeof(StepD));

        logger.LogEntries.Count.ShouldBe(2);
        factory.Logger.LogEntries.Count.ShouldBe(2);
    }

    [Fact]
    public void Pipeline_Discover_FromAssembly()
    {
        var logger = new TestLogger();
        var factory = new TestLoggerProvider();

        var descriptors_1 = Pipeline.DiscoverFromAssembly<StepContext>(StepAssembly, null, logger);
        var descriptors_2 = Pipeline.DiscoverFromAssembly<StepContext>(StepAssembly, null, factory);

        descriptors_1.ShouldNotBeNull();
        descriptors_1.ShouldNotBeEmpty();
        descriptors_1.Count().ShouldBe(1);
        descriptors_1.First().ImplementationType.ShouldBe(typeof(StepD));

        descriptors_2.ShouldNotBeNull();
        descriptors_2.ShouldNotBeEmpty();
        descriptors_2.Count().ShouldBe(1);
        descriptors_2.First().ImplementationType.ShouldBe(typeof(StepD));

        logger.LogEntries.Count.ShouldBe(2);
        factory.Logger.LogEntries.Count.ShouldBe(2);
    }

    [Fact]
    public void Pipeline_Discover_FromAssemblies()
    {
        var logger = new TestLogger();
        var factory = new TestLoggerProvider();

        var descriptors_1 = Pipeline.Discover<StepContext>(new[] { StepAssembly }, null, logger);
        var descriptors_2 = Pipeline.Discover<StepContext>(new[] { StepAssembly }, null, factory);

        descriptors_1.ShouldNotBeNull();
        descriptors_1.ShouldNotBeEmpty();
        descriptors_1.Count().ShouldBe(1);
        descriptors_1.First().ImplementationType.ShouldBe(typeof(StepD));

        descriptors_2.ShouldNotBeNull();
        descriptors_2.ShouldNotBeEmpty();
        descriptors_2.Count().ShouldBe(1);
        descriptors_2.First().ImplementationType.ShouldBe(typeof(StepD));

        logger.LogEntries.Count.ShouldBe(2);
        factory.Logger.LogEntries.Count.ShouldBe(2);
    }

    [Fact]
    public void Pipeline_Discover_AllAssemblies()
    {
        var logger = new TestLogger();
        var factory = new TestLoggerProvider();

        var descriptors_1 = Pipeline.Discover<StepContext>(null, null, logger);
        var descriptors_2 = Pipeline.Discover<StepContext>(null, null, factory);

        descriptors_1.ShouldNotBeNull();
        descriptors_1.ShouldNotBeEmpty();
        descriptors_1.Count().ShouldBe(1);
        descriptors_1.First().ImplementationType.ShouldBe(typeof(StepD));

        descriptors_2.ShouldNotBeNull();
        descriptors_2.ShouldNotBeEmpty();
        descriptors_2.Count().ShouldBe(1);
        descriptors_2.First().ImplementationType.ShouldBe(typeof(StepD));

        logger.LogEntries.Count.ShouldBe(2);
        factory.Logger.LogEntries.Count.ShouldBe(2);
    }

    [Fact]
    public void Pipeline_Discover_Using_Environment()
    {
        var descriptors_1 = Pipeline.Discover<StepContext>(null, "1").ToList();
        var descriptors_2 = Pipeline.Discover<StepContext>(null, "2").ToList();

        descriptors_1.ShouldNotBeNull();
        descriptors_1.ShouldNotBeEmpty();
        descriptors_1.Count().ShouldBe(4);
        descriptors_1[0].ImplementationType.ShouldBe(typeof(StepA1));
        descriptors_1[1].ImplementationType.ShouldBe(typeof(StepB1));
        descriptors_1[2].ImplementationType.ShouldBe(typeof(StepC1));
        descriptors_1[3].ImplementationType.ShouldBe(typeof(StepD));

        descriptors_2.ShouldNotBeNull();
        descriptors_2.ShouldNotBeEmpty();
        descriptors_2.Count().ShouldBe(4);
        descriptors_2[0].ImplementationType.ShouldBe(typeof(StepA2));
        descriptors_2[1].ImplementationType.ShouldBe(typeof(StepC2));
        descriptors_2[2].ImplementationType.ShouldBe(typeof(StepB2));
        descriptors_2[3].ImplementationType.ShouldBe(typeof(StepD));
    }

    [Fact]
    public void Pipeline_Register_DoesNotAllowDuplicateRunners()
    {
        var logger = new TestLogger();
        var services = new ServiceCollection();
        var descriptors = Pipeline.Discover<StepContext>();

        services.AddScoped<IPipelineRunner<StepContext>, PipelineRunner<StepContext>>();
        Pipeline.Register<StepContext>(services, descriptors, ServiceLifetime.Scoped, logger);

        logger.LogEntries.Count.ShouldBe(1);
        logger.ShouldContainMessage("Pipeline runner for StepContext already registered. Skipping registration.", LogLevel.Debug);
    }

    [Fact]
    public void Pipeline_Register_RegistersSteps()
    {
        var logger = new TestLogger();
        var services = new ServiceCollection();
        var descriptors = Pipeline.Discover<StepContext>(null, "1");

        Pipeline.Register<StepContext>(services, descriptors, ServiceLifetime.Scoped, logger);

        logger.LogEntries.Count.ShouldBe(5);
        logger.ShouldContainMessage("Registering pipeline step InstaPipe.Tests.Steps.StepA1 with Scoped lifetime", LogLevel.Debug);
        logger.ShouldContainMessage("Registering pipeline step InstaPipe.Tests.Steps.StepB1 with Scoped lifetime", LogLevel.Debug);
        logger.ShouldContainMessage("Registering pipeline step InstaPipe.Tests.Steps.StepC1 with Scoped lifetime", LogLevel.Debug);
        logger.ShouldContainMessage("Registering pipeline step InstaPipe.Tests.Steps.StepD with Scoped lifetime", LogLevel.Debug);
        logger.ShouldContainMessage("Registering pipeline runner IPipelineRunner`1 with Scoped lifetime", LogLevel.Debug);

        services.Count(s => s.ServiceType == typeof(IPipelineStep<StepContext>)).ShouldBe(4);
    }
}
