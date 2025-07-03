using InstaPipe.Tests.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace InstaPipe.Tests;

public class CompositionExtensionTests
{
    [Fact]
    public async Task AddPipeline()
    {
        var context = new StepContext();
        var services = new ServiceCollection();

        services.AddPipeline<StepContext>();

        var provider = services.BuildServiceProvider();
        var steps = provider.GetRequiredService<IEnumerable<Lazy<IPipelineStep<StepContext>>>>().ToList();

        var runner = provider.GetRequiredService<IPipelineRunner<StepContext>>();
        runner.ShouldNotBeNull();

        await runner.ExecuteAsync(context);
        var result = context.ToString();
        context.ToString().ShouldBe("D0");
    }
}
