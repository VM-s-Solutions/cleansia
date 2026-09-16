using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// An <see cref="ILoggerProvider"/> a test registers on a host to read back what the host logged, for
/// the acceptance criteria that name a log line as the operations fact: a Stripe webhook a frozen
/// company's books refused is answered 200 and the Error entry is what an operator sees.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToList();

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    public sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

    private sealed class RecordingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}
