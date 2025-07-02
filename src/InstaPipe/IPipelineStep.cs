namespace InstaPipe;

/// <summary>
/// Represents the next step to be invoked in the pipeline sequence.
/// </summary>
public delegate Task PipelineDelegate<T>(T context, CancellationToken cancellationToken = default);

/// <summary>
/// Defines a single executable step in the pipeline that operates on a context of type <typeparamref name="T"/> 
/// and may alter the context and/or short-circuit the pipeline
/// </summary>
/// <typeparam name="T">The type of context passed through the pipeline</typeparam>
public interface IPipelineStep<T> : IPipelineStepMetadata
{
    /// <summary>
    /// Executes the logic for this step and optionally invokes the next step in the pipeline
    /// </summary>
    /// <param name="context">The context object to process</param>
    /// <param name="next">A delegate representing the next step in the pipeline</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation</param>
    Task InvokeAsync(T context, PipelineDelegate<T> next, CancellationToken cancellationToken = default);
}

