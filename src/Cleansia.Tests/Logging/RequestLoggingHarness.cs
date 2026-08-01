using System.Reflection;
using System.Text;
using System.Text.Json;
using Cleansia.Config.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Cleansia.Tests.Logging;

/// <summary>
/// Drives a host's real RequestLoggingMiddleware over a real <see cref="DefaultHttpContext"/> and hands
/// back what it actually logged. Shared by the redaction and sensitive-path suites so both exercise the
/// production middleware rather than a re-implementation of it.
///
/// The five hosts each carry their own copy of the middleware, so every assertion runs against all five.
/// </summary>
internal static class RequestLoggingHarness
{
    public static TheoryData<Type> HostMiddlewareTypes() =>
    [
        typeof(Cleansia.Web.Customer.Middleware.RequestLoggingMiddleware),
        typeof(Cleansia.Web.Mobile.Customer.Middleware.RequestLoggingMiddleware),
        typeof(Cleansia.Web.Partner.Middleware.RequestLoggingMiddleware),
        typeof(Cleansia.Web.Admin.Middleware.RequestLoggingMiddleware),
        typeof(Cleansia.Web.Mobile.Partner.Middleware.RequestLoggingMiddleware),
    ];

    /// <summary>The wire shape the hosts actually emit — MVC's web defaults plus the shared converters.</summary>
    public static readonly JsonSerializerOptions WireOptions = BuildWireOptions();

    private static JsonSerializerOptions BuildWireOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        CleansiaStartupBase.ConfigureJsonSerialization(options);
        return options;
    }

    public static int LimitOf(Type middlewareType, string constName) =>
        (int)middlewareType
            .GetField(constName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;

    public static async Task<List<string>> RunAsync(
        Type middlewareType,
        string path,
        string responseJson,
        string? requestJson = null,
        string method = "GET")
    {
        var factory = new CapturingLoggerFactory();
        var loggerType = typeof(Logger<>).MakeGenericType(middlewareType);
        var logger = Activator.CreateInstance(loggerType, factory)!;

        RequestDelegate next = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(responseJson));
        };

        var middleware = Activator.CreateInstance(middlewareType, next, logger)!;

        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = requestJson is null
            ? new MemoryStream()
            : new MemoryStream(Encoding.UTF8.GetBytes(requestJson));
        context.Response.Body = new MemoryStream();

        var invoke = middlewareType.GetMethod("InvokeAsync", BindingFlags.Public | BindingFlags.Instance)!;
        await (Task)invoke.Invoke(middleware, [context])!;

        return factory.Messages;
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }

        private sealed class CapturingLogger(List<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
