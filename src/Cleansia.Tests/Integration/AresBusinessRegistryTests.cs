using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Cleansia.Infra.Services;
using Cleansia.Infra.Services.BusinessRegistry;
using Cleansia.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The ARES reply turned into the three facts approval reads, on bodies shaped like the live register's
/// (checked against ares.gov.cz on 2026-10-04): an unknown IČO is a 404, a known one carries
/// <c>datumZaniku</c> only once the business has ended, and <c>seznamRegistraci.stavZdrojeRzp</c> reads
/// AKTIVNI while a trade licence is in force.
/// </summary>
public class AresBusinessRegistryTests
{
    private const string Ico = "27082440";

    private static string Subject(string? datumZaniku = null, string? stavZdrojeRzp = "AKTIVNI")
    {
        var ended = datumZaniku is null ? string.Empty : $"\"datumZaniku\":\"{datumZaniku}\",";
        var trade = stavZdrojeRzp is null ? string.Empty : $",\"stavZdrojeRzp\":\"{stavZdrojeRzp}\"";
        return "{\"ico\":\"" + Ico + "\",\"obchodniJmeno\":\"Alza.cz a.s.\",\"datumVzniku\":\"2003-08-26\"," + ended
            + "\"seznamRegistraci\":{\"stavZdrojeVr\":\"AKTIVNI\",\"stavZdrojeRes\":\"AKTIVNI\"" + trade + "}}";
    }

    [Fact]
    public async Task A_Business_In_Force_With_A_Trade_Licence_Is_Registered_And_Asked_For_By_Its_Ico()
    {
        var handler = Replying(HttpStatusCode.OK, Subject());

        var record = await Registry(handler).LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: true), record);
        Assert.Equal(AresBusinessRegistry.Endpoint + Ico, Assert.Single(handler.Urls));
    }

    [Fact]
    public async Task A_Business_With_An_End_Date_Has_Ceased()
    {
        var record = await Registry(Replying(HttpStatusCode.OK, Subject(datumZaniku: "2024-05-31")))
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryAnswer.Registered, record.Answer);
        Assert.True(record.Ceased);
    }

    [Theory]
    [InlineData("HISTORICKY")]
    [InlineData("NEEXISTUJICI")]
    [InlineData(null)]
    public async Task A_Trade_Register_Entry_That_Is_Not_Active_Holds_No_Trade_Licence(string? stavZdrojeRzp)
    {
        var record = await Registry(Replying(HttpStatusCode.OK, Subject(stavZdrojeRzp: stavZdrojeRzp)))
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryAnswer.Registered, record.Answer);
        Assert.False(record.TradeLicenceActive);
    }

    [Fact]
    public async Task An_Ico_The_Register_Does_Not_Hold_Is_Not_Registered()
    {
        const string notFound = """{"kod":"NENALEZENO","subKod":"VYSTUP_SUBJEKT_NENALEZEN"}""";

        var record = await Registry(Replying(HttpStatusCode.NotFound, notFound))
            .LookupAsync("CZE", "12345678", CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.NotRegistered, record);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Any_Other_Status_Is_Unavailable(HttpStatusCode status)
    {
        var record = await Registry(Replying(status, "{}")).LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.Unavailable, record);
    }

    [Fact]
    public async Task A_Reply_That_Is_Not_Json_Is_Unavailable()
    {
        var record = await Registry(Replying(HttpStatusCode.OK, "<html>maintenance</html>"))
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.Unavailable, record);
    }

    [Fact]
    public async Task A_Transport_Failure_Is_Unavailable()
    {
        var record = await Registry(Throwing(new HttpRequestException("connection refused")))
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.Unavailable, record);
    }

    [Fact]
    public async Task A_Timeout_Is_Unavailable()
    {
        var record = await Registry(Throwing(new TaskCanceledException("timed out")))
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.Unavailable, record);
    }

    [Fact]
    public async Task A_Request_The_Caller_Abandoned_Is_Not_Turned_Into_An_Answer()
    {
        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Registry(Throwing(new TaskCanceledException())).LookupAsync("CZE", Ico, abandoned.Token));
    }

    [Theory]
    [InlineData("CZE")]
    [InlineData("CZ")]
    [InlineData("cze")]
    public async Task Czechia_Is_Asked_By_Either_Iso_Code(string countryIsoCode)
    {
        var handler = Replying(HttpStatusCode.OK, Subject());

        var record = await Registry(handler).LookupAsync(countryIsoCode, Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryAnswer.Registered, record.Answer);
        Assert.Single(handler.Urls);
    }

    [Theory]
    [InlineData("SVK")]
    [InlineData("DEU")]
    public async Task Another_Country_Consults_Nothing(string countryIsoCode)
    {
        var handler = Replying(HttpStatusCode.OK, Subject());

        var record = await Registry(handler).LookupAsync(countryIsoCode, Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.NotConsulted, record);
        Assert.Empty(handler.Urls);
    }

    [Fact]
    public async Task Switched_Off_It_Consults_Nothing()
    {
        var handler = Replying(HttpStatusCode.OK, Subject());

        var record = await Registry(handler, Config(("Ares:Enabled", "false")))
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.NotConsulted, record);
        Assert.Empty(handler.Urls);
    }

    [Fact]
    public void A_Host_With_No_Ares_Section_Checks()
    {
        Assert.True(Config().Enabled);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("1234567a")]
    public async Task A_Number_That_Cannot_Be_A_Czech_Ico_Is_Not_Registered_Without_Asking(string number)
    {
        var handler = Replying(HttpStatusCode.OK, Subject());

        var record = await Registry(handler).LookupAsync("CZE", number, CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.NotRegistered, record);
        Assert.Empty(handler.Urls);
    }

    [Fact]
    public async Task Surrounding_Whitespace_Is_Not_Part_Of_The_Ico()
    {
        var handler = Replying(HttpStatusCode.OK, Subject());

        await Registry(handler).LookupAsync("CZE", $" {Ico} ", CancellationToken.None);

        Assert.Equal(AresBusinessRegistry.Endpoint + Ico, Assert.Single(handler.Urls));
    }

    /// <summary>
    /// The registry as the hosts compose it, its named client's resilience handler included, over a
    /// primary handler that fails once: the failure is retried, so one blip is not an answer.
    /// </summary>
    [Fact]
    public async Task The_Composed_Registry_Retries_A_Transient_Failure_Before_Answering()
    {
        var replies = new Queue<HttpResponseMessage>(
        [
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Subject(), Encoding.UTF8, "application/json") },
        ]);
        var handler = new StubHandler(replies.Dequeue);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddInfrastructureServices();
        services.AddHttpClient(AresBusinessRegistry.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var record = await scope.ServiceProvider.GetRequiredService<IBusinessRegistry>()
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryAnswer.Registered, record.Answer);
        Assert.Equal(2, handler.Urls.Count);
    }

    /// <summary>
    /// As the API hosts compose it, under the service defaults that give every client the standard handler.
    /// A register that keeps failing is asked once and twice again, within the lookup's own 12 s. With the
    /// standard handler around it, each of that handler's four attempts ran all three: twelve requests in up
    /// to 30 s, on a save and an approval that wait for the answer.
    /// </summary>
    [Fact]
    public async Task Under_The_Host_Defaults_A_Register_That_Keeps_Failing_Is_Asked_Three_Times()
    {
        var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await using var provider = HostComposition(handler, new CapturingLoggerProvider());
        await using var scope = provider.CreateAsyncScope();

        var record = await scope.ServiceProvider.GetRequiredService<IBusinessRegistry>()
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Equal(BusinessRegistryRecord.Unavailable, record);
        Assert.Equal(3, handler.Urls.Count);
    }

    /// <summary>
    /// The HTTP client's request logging writes the URL at Information, and an ARES URL ends in the cleaner's
    /// IČO. The registry's own refusal is still logged, so the capture is known to be listening.
    /// </summary>
    [Fact]
    public async Task Under_The_Host_Defaults_No_Log_Line_Carries_The_Ico()
    {
        var logs = new CapturingLoggerProvider();
        await using var provider = HostComposition(Replying(HttpStatusCode.Forbidden, "{}"), logs);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IBusinessRegistry>()
            .LookupAsync("CZE", Ico, CancellationToken.None);

        Assert.Contains(logs.Entries, e => e.EventId == AresBusinessRegistry.UnavailableEvent.Id);
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains(Ico, StringComparison.Ordinal));
    }

    private static ServiceProvider HostComposition(StubHandler primary, CapturingLoggerProvider logs)
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddServiceDefaults(
            configuration,
            Mock.Of<IHostEnvironment>(e => e.ApplicationName == "Cleansia.Tests" && e.EnvironmentName == "Production"));
        services.AddInfrastructureServices();
        services.AddHttpClient(AresBusinessRegistry.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => primary);
        return services.BuildServiceProvider();
    }

    private static AresBusinessRegistry Registry(StubHandler handler, AresConfig? config = null)
        => new(new StubHttpClientFactory(handler), config ?? Config(), NullLogger<AresBusinessRegistry>.Instance);

    private static AresConfig Config(params (string Key, string Value)[] settings)
        => new(new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build());

    private static StubHandler Replying(HttpStatusCode status, string body)
        => new(() => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    private static StubHandler Throwing(Exception exception) => new(() => throw exception);

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(respond());
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(int EventId, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<(int EventId, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue((eventId.Id, formatter(state, exception)));
        }
    }
}
