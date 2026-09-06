using System.Text;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Infrastructure.Pdf;

// THROWAWAY PROBE - delete before finishing.
public class ZzPdfDeterminismProbeTests
{
    private const string Out = @"C:\Users\cmisa\AppData\Local\Temp\claude\pdfprobe";

    private static readonly QuestPdfService Pdf = new(
        new LayoutBuilderFactory([], [new DefaultInvoiceLayoutBuilder(), new CzechInvoiceLayoutBuilder()]),
        NullLogger<QuestPdfService>.Instance);

    [Fact]
    public void Zz_Probe_Dump_Raw()
    {
        var data = MapFixture(PayrollMockFactory.TestVariableSymbol);
        var raw = Pdf.GenerateInvoicePdf(data, context: null, countryCode: null);
        Directory.CreateDirectory(Out);
        File.WriteAllBytes(Path.Combine(Out, "one.pdf"), raw);

        var s = Encoding.Latin1.GetString(raw);
        var sb = new StringBuilder();
        sb.AppendLine($"len={raw.Length}");
        foreach (Match m in Regex.Matches(s, @"D:\d{4}[^\s\)\]/>]{0,24}"))
        {
            sb.AppendLine($"D-date @{m.Index}: {m.Value}");
        }
        foreach (Match m in Regex.Matches(s, @"\d{4}-\d{2}-\d{2}T[\d:+\-Z']{5,20}"))
        {
            sb.AppendLine($"ISO-date @{m.Index}: {m.Value}");
        }
        foreach (Match m in Regex.Matches(s, @"/ID\s*\[[^\]]{0,200}\]"))
        {
            sb.AppendLine($"ID @{m.Index}: {m.Value}");
        }
        foreach (Match m in Regex.Matches(s, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"))
        {
            sb.AppendLine($"UUID @{m.Index}: {m.Value}");
        }
        // dump the tail (trailer) as latin1
        sb.AppendLine("--- TAIL 1200 ---");
        sb.AppendLine(Sanitize(s[Math.Max(0, s.Length - 1200)..]));
        File.WriteAllText(Path.Combine(Out, "dump.txt"), sb.ToString());
    }

    [Fact]
    public void Zz_Probe_Loop()
    {
        var data = MapFixture(PayrollMockFactory.TestVariableSymbol);
        var baseline = Render(data);
        var rawBaseline = Pdf.GenerateInvoicePdf(data, context: null, countryCode: null);
        var report = new StringBuilder();
        var normDiffs = 0;
        var rawDiffs = 0;

        for (var i = 0; i < 400; i++)
        {
            var raw = Pdf.GenerateInvoicePdf(data, context: null, countryCode: null);
            var norm = Normalize(raw);

            if (!raw.AsSpan().SequenceEqual(rawBaseline))
            {
                rawDiffs++;
            }

            if (!norm.AsSpan().SequenceEqual(baseline))
            {
                normDiffs++;
                if (normDiffs <= 5)
                {
                    report.AppendLine(Describe(i, baseline, norm));
                }
            }
        }

        Directory.CreateDirectory(Out);
        var header = $"iterations=400 rawDiffs={rawDiffs} normalizedDiffs={normDiffs} len={baseline.Length}\n";
        File.WriteAllText(Path.Combine(Out, "loop.txt"), header + report);
        Assert.True(normDiffs == 0, header + report);
    }

    [Fact]
    public void Zz_Probe_Loop_Parallel()
    {
        var data = MapFixture(PayrollMockFactory.TestVariableSymbol);
        var baseline = Render(data);
        var report = new StringBuilder();
        var normDiffs = 0;

        Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var norm = Render(data);
            if (!norm.AsSpan().SequenceEqual(baseline))
            {
                lock (report)
                {
                    normDiffs++;
                    if (normDiffs <= 5)
                    {
                        report.AppendLine(Describe(i, baseline, norm));
                        Directory.CreateDirectory(Out);
                        File.WriteAllBytes(Path.Combine(Out, $"A_{normDiffs}.pdf"), baseline);
                        File.WriteAllBytes(Path.Combine(Out, $"B_{normDiffs}.pdf"), norm);
                    }
                }
            }
        });

        Directory.CreateDirectory(Out);
        var header = $"PARALLEL iterations=400 normalizedDiffs={normDiffs} len={baseline.Length}\n";
        File.WriteAllText(Path.Combine(Out, "loop_parallel.txt"), header + report);
        Assert.True(normDiffs == 0, header + report);
    }

    private static string Describe(int iteration, byte[] a, byte[] b)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== iteration {iteration}: lenA={a.Length} lenB={b.Length}");
        var min = Math.Min(a.Length, b.Length);
        var first = -1;
        for (var i = 0; i < min; i++)
        {
            if (a[i] != b[i])
            {
                first = i;
                break;
            }
        }

        if (first < 0)
        {
            sb.AppendLine("common prefix identical; lengths differ");
            first = min;
        }

        sb.AppendLine($"first differing offset = {first}");
        var from = Math.Max(0, first - 60);
        sb.AppendLine("A: " + Sanitize(Encoding.Latin1.GetString(a, from, Math.Min(160, a.Length - from))));
        sb.AppendLine("B: " + Sanitize(Encoding.Latin1.GetString(b, from, Math.Min(160, b.Length - from))));

        // how many bytes differ in total
        var count = 0;
        for (var i = 0; i < min; i++)
        {
            if (a[i] != b[i])
            {
                count++;
            }
        }

        sb.AppendLine($"total differing bytes in common region = {count}");
        return sb.ToString();
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(c is >= ' ' and < (char)127 ? c : '.');
        }

        return sb.ToString();
    }

    private static byte[] Render(InvoicePdfData data) =>
        Normalize(Pdf.GenerateInvoicePdf(data, context: null, countryCode: null));

    private static byte[] Normalize(byte[] bytes)
    {
        var normalized = Regex.Replace(
            Encoding.Latin1.GetString(bytes), @"(?<=D:)\d{14}", new string('0', 14));
        return Encoding.Latin1.GetBytes(normalized);
    }

    private static InvoicePdfData MapFixture(string? variableSymbol)
    {
        var invoice = PayrollMockFactory.Invoice(
            payPeriod: PayrollMockFactory.OpenPeriod(),
            variableSymbol: variableSymbol ?? PayrollMockFactory.TestVariableSymbol);

        var user = User.CreateWithPassword("cleaner@cleansia.test", "12345678Test!", "Jan", "Novák");
        var employee = Employee.CreateWithUser(user);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);

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
                countryId: "cz",
                vatNumber: "CZ87654321"),
            payoutDetails: null);
    }
}
