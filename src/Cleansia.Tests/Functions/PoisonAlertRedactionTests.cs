using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// S6 on the poison path. <c>PoisonHandlerBase</c> step 2 used to log the whole message body at
/// <c>LogError</c>, and one of the seven bodies that reach it is <c>SendEmailMessage</c>, whose
/// <c>Code</c> is documented on the type itself as "the RAW confirmation/reset token". Since
/// <c>AddSentryMonitoring</c> wired the Sentry logging provider into the Functions worker with
/// <c>MinimumEventLevel = Error</c>, that body left the process into a second vendor as well as the
/// host's retained log stream.
///
/// <para><b>Both halves are asserted, because an alert nobody can act on is its own defect.</b> The
/// token must be absent AND the identifiers that find the <c>DeadLetter</c> row must be present — and
/// "present" is tested operationally (the key the alert prints actually selects the stored body), not by
/// eyeballing a substring.</para>
///
/// <para><b>Anti-vacuity.</b> Every absence assertion is preceded by a positive control on the same
/// value in the same test: the arranged body is asserted to CONTAIN the token before the alert is
/// asserted not to. A fixture that never populated <c>Code</c> would make every one of these green while
/// the leak shipped.</para>
///
/// <para><b>What "the alert" means here.</b> Sentry's logging integration reads three things out of one
/// <c>ILogger</c> call: the formatted message (<c>SentryMessage.Formatted</c>), the template
/// (<c>SentryMessage.Message</c>) and every structured property — each string-valued one becoming an
/// INDEXED TAG, plus a scope breadcrumb that re-attaches to later unrelated events. Asserting only on
/// the formatted string would have missed the tag, so <c>EverythingSentrySees</c> concatenates all of
/// it.</para>
/// </summary>
public class PoisonAlertRedactionTests
{
    // A real, high-entropy token in the shape the reset/confirmation flows mint - not a placeholder that
    // could accidentally be a substring of something else in the body.
    private const string RawResetToken = "Q9vX2mR7-kLp4TzB-8sHn1WdC-eJf6YuA0";
    private const string RecipientEmail = "victim.household@example.com";
    private const string RecipientName = "Jarmila Novakova";
    private const string UserId = "USER-8817";
    private const string TenantId = "TENANT-CZ";

    private static readonly JsonSerializerOptions WireOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// The exact bytes the producer puts on the queue - serialized through the same shape
    /// <c>AzureStorageQueueClient</c> uses, so this fixture cannot drift from the wire.
    /// </summary>
    private static string PoisonedResetEmailBody()
    {
        var payload = new SendEmailMessage(
            EmailType.ResetPassword, RecipientEmail, RecipientName, RawResetToken, "cs", UserId, TenantId);

        var key = MessageKeys.Email(payload.EmailType, payload.UserId, MessageKeys.HashCode(payload.Code));

        return JsonSerializer.Serialize(new QueueEnvelope<SendEmailMessage>(key, TenantId, payload), WireOptions);
    }

    // half 1: the secret does not leave the process

    [Fact]
    public async Task A_Poisoned_Send_Email_Alert_Carries_Neither_The_Reset_Token_Nor_The_Recipient()
    {
        var body = PoisonedResetEmailBody();

        // Positive controls. Without these three lines every assertion below is vacuous.
        Assert.Contains(RawResetToken, body, StringComparison.Ordinal);
        Assert.Contains(RecipientEmail, body, StringComparison.Ordinal);
        Assert.Contains(RecipientName, body, StringComparison.Ordinal);

        var logger = new CapturingLogger<SendEmailPoisonHandler>();
        var store = new Mock<IDeadLetterStore>();

        await new SendEmailPoisonHandler(store.Object, logger).HandleAsync(body, CancellationToken.None);

        var alert = Assert.Single(logger.Entries);
        Assert.DoesNotContain(RawResetToken, alert.EverythingSentrySees, StringComparison.Ordinal);
        Assert.DoesNotContain(RecipientEmail, alert.EverythingSentrySees, StringComparison.Ordinal);
        Assert.DoesNotContain(RecipientName, alert.EverythingSentrySees, StringComparison.Ordinal);
    }

    // half 2: the alert is still an alert, and it still finds the row

    [Fact]
    public async Task The_Alert_Fires_At_Error_So_It_Still_Becomes_A_Sentry_Event()
    {
        var logger = new CapturingLogger<SendEmailPoisonHandler>();

        await new SendEmailPoisonHandler(new Mock<IDeadLetterStore>().Object, logger)
            .HandleAsync(PoisonedResetEmailBody(), CancellationToken.None);

        // AddSentryMonitoring sets MinimumEventLevel = Error; a Warning here would be a breadcrumb
        // attached to some later event rather than a page.
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task The_Alert_Carries_Identifiers_That_Actually_Select_The_Stored_DeadLetter_Row()
    {
        var body = PoisonedResetEmailBody();
        var logger = new CapturingLogger<SendEmailPoisonHandler>();

        string? recordedQueue = null;
        string? recordedBody = null;
        var store = new Mock<IDeadLetterStore>();
        store.Setup(s => s.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((queue, raw, _, _) =>
            {
                recordedQueue = queue;
                recordedBody = raw;
            })
            .Returns(Task.CompletedTask);

        await new SendEmailPoisonHandler(store.Object, logger).HandleAsync(body, CancellationToken.None);

        var alert = Assert.Single(logger.Entries);
        var messageKey = Assert.IsType<string>(alert.Property("MessageKey"));
        var fingerprint = Assert.IsType<string>(alert.Property("Fingerprint"));

        // The recovery walk an operator performs, executed:
        //   SELECT ... WHERE "SourceQueue" = {SourceQueue} AND "RawBody" LIKE '%{MessageKey}%'
        Assert.Equal(QueueNames.SendEmail, alert.Property("SourceQueue"));
        Assert.Equal(QueueNames.SendEmail, recordedQueue);
        Assert.Contains(messageKey, recordedBody!, StringComparison.Ordinal);

        // ...and the tie-breaker when a key repeats or is a sentinel: the fingerprint identifies the row
        // byte-exactly without reproducing a byte.
        Assert.Equal(MessageKeys.HashCode(recordedBody!), fingerprint);

        // The key is the domain handle a re-issue needs: WHICH user and WHICH purpose.
        Assert.Contains(UserId, messageKey, StringComparison.Ordinal);
        Assert.Contains("reset", messageKey, StringComparison.Ordinal);
        Assert.Equal(TenantId, alert.Property("TenantId"));
    }

    [Fact]
    public async Task The_Durable_Row_Still_Receives_The_Body_Verbatim()
    {
        var body = PoisonedResetEmailBody();
        string? recordedBody = null;

        var store = new Mock<IDeadLetterStore>();
        store.Setup(s => s.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((_, raw, _, _) => recordedBody = raw)
            .Returns(Task.CompletedTask);

        await new SendEmailPoisonHandler(store.Object, new CapturingLogger<SendEmailPoisonHandler>())
            .HandleAsync(body, CancellationToken.None);

        // The redaction is on the ALERT only. Blunting the recovery source instead would be the other
        // failure mode, and this is the assertion that reddens if someone "fixes" the leak there.
        Assert.Equal(body, recordedBody);
        Assert.Contains(RawResetToken, recordedBody!, StringComparison.Ordinal);
    }

    // the persist-failed branch: ruled, not inherited

    [Fact]
    public async Task Persist_Failure_Still_Alerts_At_Error_And_Still_Acks()
    {
        var logger = new CapturingLogger<GenerateReceiptPoisonHandler>();
        var outage = new InvalidOperationException("Npgsql: connection refused");

        // ACK = return without throwing. A throw here re-poisons into <queue>-poison-poison forever.
        var thrown = await Record.ExceptionAsync(() =>
            new GenerateReceiptPoisonHandler(ThrowingStore(outage), logger)
                .HandleAsync("{\"messageKey\":\"receipt:ORDER-9\"}", CancellationToken.None));

        Assert.Null(thrown);
        var alert = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, alert.Level);
        Assert.Same(outage, alert.Exception);
    }

    /// <summary>
    /// The ruling: the persist-failed alert is redacted on the SAME terms as the happy path. The log was
    /// never D3's recovery source (D3 names the <c>DeadLetter</c> row), and the branch's own worst case is
    /// a Postgres outage - during which every poisoned message takes it at once, so a body-carrying alert
    /// here is a burst of live reset tokens into Sentry, not one.
    /// </summary>
    [Fact]
    public async Task Persist_Failure_Does_Not_Fall_Back_To_Logging_The_Raw_Body()
    {
        var body = PoisonedResetEmailBody();
        Assert.Contains(RawResetToken, body, StringComparison.Ordinal); // positive control

        var logger = new CapturingLogger<SendEmailPoisonHandler>();

        await new SendEmailPoisonHandler(ThrowingStore(new InvalidOperationException("db down")), logger)
            .HandleAsync(body, CancellationToken.None);

        var alert = Assert.Single(logger.Entries);
        Assert.DoesNotContain(RawResetToken, alert.EverythingSentrySees, StringComparison.Ordinal);
        Assert.DoesNotContain(RecipientEmail, alert.EverythingSentrySees, StringComparison.Ordinal);

        // Still actionable: the operator learns which message, and that no row exists to go and read.
        Assert.Contains(UserId, Assert.IsType<string>(alert.Property("MessageKey")), StringComparison.Ordinal);
        Assert.Contains("NO DURABLE ROW", alert.Formatted, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reason the ruling costs D3 nothing where D3 is strictest. The two queues whose durable row D3
    /// calls MANDATORY carry no credential and no PII, and their entire domain subject is already inside
    /// the <c>MessageKey</c> - so on the fiscal path the redaction subtracts only <c>LanguageCode</c>,
    /// which finds no row and recovers no money.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Fiscal_Alerts_Retain_Their_Whole_Domain_Subject_Under_Redaction(bool persistSucceeds)
    {
        var receipt = new GenerateReceiptMessage("ORDER-4471", "cs");
        var receiptBody = JsonSerializer.Serialize(
            new QueueEnvelope<GenerateReceiptMessage>(MessageKeys.Receipt(receipt.OrderId), TenantId, receipt), WireOptions);

        var invoice = new GenerateInvoiceMessage("EMP-32", "PP-2026-08", "sk");
        var invoiceBody = JsonSerializer.Serialize(
            new QueueEnvelope<GenerateInvoiceMessage>(MessageKeys.Invoice(invoice.PayPeriodId, invoice.EmployeeId), TenantId, invoice),
            WireOptions);

        var receiptLogger = new CapturingLogger<GenerateReceiptPoisonHandler>();
        var invoiceLogger = new CapturingLogger<GenerateInvoicePoisonHandler>();
        var store = persistSucceeds
            ? new Mock<IDeadLetterStore>().Object
            : ThrowingStore(new InvalidOperationException("db down"));

        await new GenerateReceiptPoisonHandler(store, receiptLogger).HandleAsync(receiptBody, CancellationToken.None);
        await new GenerateInvoicePoisonHandler(store, invoiceLogger).HandleAsync(invoiceBody, CancellationToken.None);

        var receiptAlert = Assert.Single(receiptLogger.Entries).EverythingSentrySees;
        Assert.Contains(receipt.OrderId, receiptAlert, StringComparison.Ordinal);

        var invoiceAlert = Assert.Single(invoiceLogger.Entries).EverythingSentrySees;
        Assert.Contains(invoice.PayPeriodId, invoiceAlert, StringComparison.Ordinal);
        Assert.Contains(invoice.EmployeeId, invoiceAlert, StringComparison.Ordinal);
    }

    // the descriptor itself

    [Fact]
    public void A_Bare_Pre_Envelope_Payload_Yields_A_Sentinel_And_Never_Its_Fields()
    {
        // The D2.1a dual-read shape: a payload with no envelope around it. It still carries the token, so
        // "no messageKey" must not degrade into "log what you can find".
        var bare = JsonSerializer.Serialize(
            new SendEmailMessage(EmailType.ConfirmationEmail, RecipientEmail, RecipientName, RawResetToken, "en", UserId, TenantId),
            WireOptions);
        Assert.Contains(RawResetToken, bare, StringComparison.Ordinal); // positive control

        var descriptor = PoisonAlert.Describe(bare);

        Assert.Equal(PoisonAlert.Absent, descriptor.MessageKey);
        Assert.Equal(TenantId, descriptor.TenantId);
        Assert.DoesNotContain(RawResetToken, Flatten(descriptor), StringComparison.Ordinal);
        Assert.DoesNotContain(RecipientEmail, Flatten(descriptor), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{\"messageKey\":")]
    [InlineData("[1,2,3]")]
    [InlineData("\"a bare string\"")]
    public void An_Unreadable_Body_Still_Produces_An_Actionable_Descriptor(string body)
    {
        var descriptor = PoisonAlert.Describe(body);

        Assert.Equal(PoisonAlert.Unparseable, descriptor.MessageKey);
        Assert.Null(descriptor.TenantId);
        // The fingerprint is what keeps even this case actionable: it matches exactly one stored row.
        Assert.Equal(MessageKeys.HashCode(body), descriptor.Fingerprint);
    }

    [Fact]
    public void A_Non_String_MessageKey_Is_Refused_Rather_Than_Rendered()
    {
        // A hostile or corrupt body could put an object where the key belongs; rendering it would copy
        // whatever it contains into the alert.
        var descriptor = PoisonAlert.Describe("{\"messageKey\":{\"code\":\"" + RawResetToken + "\"},\"tenantId\":42}");

        Assert.Equal(PoisonAlert.Absent, descriptor.MessageKey);
        Assert.Null(descriptor.TenantId);
        Assert.DoesNotContain(RawResetToken, Flatten(descriptor), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every member of this record reaches Sentry. Adding one must be a deliberate act with a reviewer on
    /// it, not a refactor - so the shape is frozen here by name.
    /// </summary>
    [Fact]
    public void The_Alert_Descriptor_Exposes_Exactly_These_Four_Members()
    {
        var members = typeof(PoisonAlertDescriptor)
            .GetProperties()
            .Select(p => p.Name)
            .Where(name => name != "EqualityContract")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Bytes", "Fingerprint", "MessageKey", "TenantId"], members);
    }

    /// <summary>
    /// A second, independent mutation detector at the source level: the defect was literally the
    /// <c>{Body}</c> placeholder, and reinstating it on either branch reddens here even if someone also
    /// reshapes the behavioural fixtures above.
    /// </summary>
    [Fact]
    public void The_Poison_Handler_Source_Passes_No_Body_Placeholder_To_A_Log_Call()
    {
        var source = File.ReadAllText(RepoPath("src", "Cleansia.Functions.Core", "Handlers", "PoisonHandlerBase.cs"));

        // Non-vacuity: we are reading the right file.
        Assert.Contains("PoisonAlert.Describe(body)", source, StringComparison.Ordinal);
        Assert.Contains("deadLetterStore.RecordAsync(SourceQueue, body", source, StringComparison.Ordinal);

        Assert.DoesNotContain("{Body}", source, StringComparison.Ordinal);
    }

    // helpers

    private static string Flatten(PoisonAlertDescriptor descriptor) =>
        $"{descriptor.MessageKey}|{descriptor.TenantId}|{descriptor.Bytes}|{descriptor.Fingerprint}";

    private static IDeadLetterStore ThrowingStore(Exception failure)
    {
        var store = new Mock<IDeadLetterStore>();
        store.Setup(s => s.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        return store.Object;
    }

    // Mirrors FunctionsWorkerErrorTelemetryTests - walk up to the *.sln, then out of src/ to the root.
    private static string RepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");

        var path = Path.GetFullPath(Path.Combine([directory!.FullName, "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected source file not found: {path}");
        return path;
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Formatted,
        Exception? Exception,
        IReadOnlyList<KeyValuePair<string, object?>> Properties)
    {
        /// <summary>
        /// Everything Sentry's logging integration reads out of one call: the formatted message, the
        /// exception, the template (which arrives as the <c>{OriginalFormat}</c> property) and every
        /// structured value - each string-valued one becoming an indexed tag.
        /// </summary>
        public string EverythingSentrySees =>
            string.Join(
                " |#| ",
                new[] { Formatted, Exception?.ToString() ?? string.Empty }
                    .Concat(Properties.Select(p => $"{p.Key}={p.Value}")));

        public object? Property(string name) =>
            Properties.FirstOrDefault(p => p.Key == name).Value;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(
                logLevel,
                formatter(state, exception),
                exception,
                (state as IReadOnlyList<KeyValuePair<string, object?>>)?.ToList() ?? []));
    }
}
