using Microsoft.Extensions.Logging;

namespace InstaPipe.Tests.TestUtils;

public class TestLoggerProvider : ILoggerProvider, ILoggerFactory
{
    public readonly TestLogger Logger = new();

    public void AddProvider(ILoggerProvider provider)
    {
        return;
    }

    public ILogger CreateLogger(string categoryName) => Logger;

    public void Dispose() { }
}
