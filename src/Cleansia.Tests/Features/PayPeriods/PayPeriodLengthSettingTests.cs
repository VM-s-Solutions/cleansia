using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Services;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// A pay period's length is one setting, PayPeriodClosing:LengthInDays. Both cleaner agreements promise an
/// invoice after each 14-day period, so 14 is the default everywhere, and only the DEV template shortens it so
/// testers see a whole pay cycle within days (owner ruling 2026-11-09).
/// </summary>
public class PayPeriodLengthSettingTests
{
    [Fact]
    public void An_Absent_Section_Binds_The_Fourteen_Days_The_Agreements_Promise()
    {
        Assert.Equal(14, PayPeriod.LengthInDays);
        Assert.Equal(PayPeriod.LengthInDays, PayPeriodTestServices.ClosingConfig().LengthInDays);
    }

    [Fact]
    public void The_Section_Binds_The_Length()
    {
        Assert.Equal(3, PayPeriodTestServices.ClosingConfig(3).LengthInDays);
    }

    [Theory]
    [InlineData(null, 14)]
    [InlineData(3, 3)]
    public async Task Create_Makes_A_Period_Of_The_Configured_Length_From_The_Start_Date(int? setting, int expectedDays)
    {
        var start = new DateOnly(2026, 1, 1);
        var repository = new Mock<IPayPeriodRepository>();
        PayPeriod? added = null;
        repository.Setup(r => r.Add(It.IsAny<PayPeriod>())).Callback<PayPeriod>(p => added = p);

        var result = await new CreatePayPeriod.Handler(repository.Object, PayPeriodTestServices.ClosingConfig(setting))
            .Handle(new CreatePayPeriod.Command(start, Notes: "by hand"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal((start, start.AddDays(expectedDays - 1)), (added!.StartDate, added.EndDate));
        Assert.Equal(expectedDays, added.GetPeriodDays());
        Assert.Equal("by hand", added.Notes);
    }

    [Theory]
    [InlineData(null, 14)]
    [InlineData(3, 3)]
    public void The_Dto_Carries_The_Last_Day_The_Period_May_Run_To(int? setting, int expectedDays)
    {
        var period = PayPeriod.CreateBiWeekly(new DateOnly(2026, 10, 20), expectedDays);

        var dto = period.MapToDto(PayPeriodTestServices.ClosingConfig(setting).LengthInDays);

        Assert.Equal(new DateOnly(2026, 10, 20).AddDays(expectedDays - 1), dto.LatestEndDate);
    }

    [Fact]
    public void The_Domain_Takes_A_Three_Day_Length()
    {
        var period = PayPeriod.CreateBiWeekly(new DateOnly(2026, 1, 1), 3);

        Assert.Equal(new DateOnly(2026, 1, 3), period.EndDate);
        Assert.Equal(new DateOnly(2026, 1, 3), period.Update(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3)).EndDate);
    }

    [Fact]
    public void The_Functions_Appsettings_Leaves_The_Length_To_The_App_Settings()
    {
        // The worker adds appsettings.json after the environment, so a value there would beat the Azure app setting.
        var json = File.ReadAllText(RepoPath("src", "Cleansia.Functions", "appsettings.json"));

        Assert.DoesNotContain("PayPeriodClosing", json);
    }

    [Fact]
    public void Only_The_Dev_Template_Shortens_The_Period_And_The_Api_Hosts_And_The_Worker_Both_Receive_It()
    {
        var bicep = WithoutComments(File.ReadAllText(RepoPath("deploy", "bicep", "main.bicep")));

        Assert.Matches(
            @"var\s+payPeriodSettings\s*=\s*env\s*==\s*'dev'\s*\?\s*\{\s*PayPeriodClosing__LengthInDays\s*:\s*'3'\s*\}\s*:\s*\{\s*\}",
            bicep);
        Assert.Single(Regex.Matches(bicep, "PayPeriodClosing__"));
        Assert.Matches(new Regex(@"var\s+apiBaseSettings\s*=\s*union\(\{.*?\}\s*,[^\r\n]*\bpayPeriodSettings\b", RegexOptions.Singleline), bicep);
        Assert.Matches(@"extraAppSettings:\s*union\([^\r\n]*\bpayPeriodSettings\b", bicep);
    }

    private static string RepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.False(directory is null, "Could not locate the solution directory from the test base directory.");
        var path = Path.GetFullPath(Path.Combine([directory!.FullName, "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected file not found: {path}");
        return path;
    }

    private static string WithoutComments(string template) =>
        Regex.Replace(
            template,
            @"(?<text>'''.*?'''|'(?:\\.|[^'\\\r\n])*')|/\*.*?\*/|//[^\n]*",
            match => match.Groups["text"].Success ? match.Value : string.Empty,
            RegexOptions.Singleline);
}
