using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// One render at a time, process-wide. → T-0679
///
/// <para><b>What this is protecting.</b> QuestPDF 2024.12.1's native Skia is not thread-safe when it
/// builds a subsetted font's <c>/ToUnicode</c> CMap: with two <c>GeneratePdf</c> calls in flight,
/// ~1-3% of renders emit a CMap mapping every glyph to U+0000. The document looks perfect and its
/// text layer is dead — copy, search and extraction return nothing usable — and both artefacts are
/// uploaded to blob storage AND emailed, so one bad render leaves two permanent copies.</para>
///
/// <para><b>Why it asserts concurrency rather than corruption.</b> The corruption is ~1-3% per
/// concurrent render, so a test that renders a few hundred times and checks the bytes is a coin flip
/// that would fail CI on its own schedule — the exact defect this suite just spent a session removing.
/// The lock's OBSERVABLE property is deterministic, so that is what is pinned.</para>
///
/// <para><b>Why it owns its own threads.</b> CI runs with collection parallelism off, so a test that
/// relied on xUnit for concurrency would observe a maximum of one whether the lock existed or not —
/// vacuously green forever. Eight dedicated threads with a rendezvous cannot be neutered that way.</para>
/// </summary>
public class QuestPdfRenderSerialisationTests
{
    private const int Threads = 8;

    /// <summary>
    /// A layout that reports when it is inside the renderer and draws one trivial page. Standing in
    /// for a real layout keeps the test at a few hundred milliseconds; what is under test is the gate,
    /// not the drawing.
    /// </summary>
    private sealed class ConcurrencyProbeLayout : IInvoiceLayoutBuilder
    {
        private int _inside;

        public string CountryCode => "PROBE";

        public IReadOnlyCollection<string> CountryCodes => ["PROBE"];

        public int Calls;

        public int MaxObserved;

        public void Build(IDocumentContainer container, InvoicePdfData data, CountryInvoiceContext? context)
        {
            var now = Interlocked.Increment(ref _inside);
            Interlocked.Increment(ref Calls);

            int seen;
            do
            {
                seen = Volatile.Read(ref MaxObserved);
            }
            while (now > seen
                && Interlocked.CompareExchange(ref MaxObserved, now, seen) != seen);

            // Long enough that two unguarded renders would overlap here rather than merely queueing.
            Thread.Sleep(25);

            container.Page(page => page.Content().Text("probe"));
            Interlocked.Decrement(ref _inside);
        }
    }

    /// <summary>
    /// THE GATE. Removing the <c>lock (RenderGate)</c> from QuestPdfService makes this fail: the
    /// observed maximum becomes 2, which is QuestPDF's own static RenderDocumentSemaphore ceiling
    /// (<c>CurrentCount == 2</c>, verified by reflection) — and 2 is exactly the condition that
    /// corrupts the CMap.
    /// </summary>
    [Fact]
    public void Only_One_Document_Renders_At_A_Time()
    {
        var probe = new ConcurrencyProbeLayout();
        var service = new QuestPdfService(
            new LayoutBuilderFactory([], [probe]),
            NullLogger<QuestPdfService>.Instance);

        var data = InvoiceFixture();
        using var startLine = new Barrier(Threads);
        var threads = new Thread[Threads];

        for (var i = 0; i < Threads; i++)
        {
            threads[i] = new Thread(() =>
            {
                // Every thread waits for the rest, so they contend rather than arriving in sequence.
                startLine.SignalAndWait();
                service.GenerateInvoicePdf(data, context: null, countryCode: "PROBE");
            });
            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "a render thread did not finish");
        }

        // Anti-vacuity: without this the test would pass on a probe that was never invoked at all.
        Assert.Equal(Threads, probe.Calls);
        Assert.Equal(1, probe.MaxObserved);
    }

    /// <summary>
    /// The same shape PayoutInvoiceRenderedSymbolTests maps, built locally rather than shared: this
    /// test cares only that SOMETHING renders, and a helper crossing two files to say so would be
    /// paid by every future reader of both.
    /// </summary>
    private static InvoicePdfData InvoiceFixture()
    {
        var invoice = PayrollMockFactory.Invoice(
            payPeriod: PayrollMockFactory.OpenPeriod(),
            variableSymbol: PayrollMockFactory.TestVariableSymbol);

        var user = User.CreateWithPassword("cleaner@cleansia.test", "12345678Test!", "Jan", "Novák");
        var employee = Employee.CreateWithUser(user);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");

        return invoice.CreatePdfData(
            employee,
            currency,
            [],
            countryContext: null,
            companyInfo: CompanyInfo.Create(
                legalName: "Cleansia s.r.o.",
                tradingName: "Cleansia",
                registrationNumber: "87654321",
                street: "Testovací 1",
                city: "Praha",
                zipCode: "11000",
                countryId: "cz"),
            payoutDetails: null);
    }
}
