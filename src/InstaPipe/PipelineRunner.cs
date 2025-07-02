using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace InstaPipe;

public class PipelineRunner<T> : IPipelineRunner<T>
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IEnumerable<Lazy<IPipelineStep<T>>> _lazySteps;
    private readonly ILogger? _logger;

    public PipelineRunner(IEnumerable<Lazy<IPipelineStep<T>>> lazySteps, ILoggerFactory? loggerFactory = null)
    {
        _lazySteps = lazySteps;
        _logger = loggerFactory?.CreateLogger($"PipelineRunner<{typeof(T).Name}>");
    }

    public string Describe()
    {
        var steps = _lazySteps
            .Select(s => s.Value)
            .Select((step, index) => new
            {
                Order = index,
                step.Name,
                step.Description,
                step.MayShortCircuit,
                step.ShortCircuitCondition
            });

        return JsonSerializer.Serialize(steps, _options);
    }

    public string DescribeSchema()
    {
        var schema = new Dictionary<string, object?>
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = "PipelineStep",
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["Order"] = new { type = "integer", description = "Execution order of the step (inferred)" },
                ["Name"] = new { type = "string", description = "Display name of the step" },
                ["Description"] = new { type = "string", description = "Optional description of the step" },
                ["MayShortCircuit"] = new { type = "boolean", description = "Whether the step may halt pipeline execution early" },
                ["ShortCircuitCondition"] = new { type = "string", description = "Explanation of the short-circuit condition, if any" },
            },
            ["required"] = new[] { "Order", "Name", "MayShortCircuit" }
        };

        return JsonSerializer.Serialize(schema, _options);
    }

    public async Task ExecuteAsync(T context, CancellationToken cancellationToken = default)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        PipelineDelegate<T> next = (_, _) => Task.CompletedTask;

        var steps = _lazySteps
            .Select((lazy, index) => (LazyStep: lazy, Order: index))
            .Reverse()
            .ToList();

        foreach (var (lazyStep, order) in steps)
        {
            var previous = next;

            next = async (ctx, ct) =>
            {
                var step = lazyStep.Value; // resolves only if this step runs

                try
                {
                    var sw = Stopwatch.StartNew();
                    var wasCalled = false;

                    // Wrap next to detect short-circuiting
                    async Task wrappedNext(T c, CancellationToken token = default)
                    {
                        wasCalled = true;
                        await previous(c, token);
                    }

                    _logger?.LogTrace("Executing pipeline step {StepName} (Order {StepOrder})", step.Name, order);
                    await step.InvokeAsync(ctx, wrappedNext, ct);

                    sw.Stop();

                    if (wasCalled)
                    {
                        _logger?.LogTrace("Completed pipeline step {StepName} (Order {StepOrder}) in {Elapsed} ms",
                            step.Name, order, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger?.LogInformation("Pipeline short-circuited by step {StepName} (Order {StepOrder}) after {Elapsed} ms",
                            step.Name, order, sw.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is PipelineExecutionException<T>) throw;

                    _logger?.LogError(ex, "Exception in pipeline step {StepName} (Order {StepOrder})", step.Name, order);
                    throw new PipelineExecutionException<T>(step.Name, order, ex);
                }
            };
        }

        await next(context, cancellationToken);
    }
}
