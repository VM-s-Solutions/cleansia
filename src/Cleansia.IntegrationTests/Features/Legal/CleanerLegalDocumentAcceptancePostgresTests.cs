using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Legal;

/// <summary>
/// Owner ruling 2026-09-28: a cleaner accepts their contract documents in the app, recorded like the
/// customer's legal acts — on their consent row, with the version, the document, the IP and the device —
/// through the real pipeline on real Postgres, and reads them back as accepted.
/// </summary>
[Collection("PostgresCollection")]
public class CleanerLegalDocumentAcceptancePostgresTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Ip = "203.0.113.20";
    private const string DeviceLabel = "Android/Pixel";

    private static User NewCleanerUser()
    {
        var user = User.CreateWithPassword("cleaner-legal@cleansia.test", "Password1!@abc", "Cle", "Aner", UserProfile.Employee);
        user.Created("seed", DateTime.UtcNow);
        return user;
    }

    private static Employee NewEmployee(User user)
    {
        var employee = Employee.CreateWithUser(user);
        employee.Created("seed", DateTime.UtcNow);
        return employee;
    }

    private static LegalDocument FrameworkContract(int effectiveDaysAgo = 1)
    {
        var document = LegalDocument.Create(
            LegalDocumentAudience.Employee,
            LegalDocumentType.CleanerFrameworkContract,
            countryId: null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-effectiveDaysAgo));
        document.AddText("en", "Framework contract", "## Terms\n\nThe cleaner works as a subcontractor.");
        document.AddText("cs", "Rámcová smlouva", "## Podmínky\n\nUklízeč pracuje jako subdodavatel.");
        return document;
    }

    private static Task SessionOf(IServiceCollection services, User user)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            user.Id, user.Email, [new Claim(ClaimTypes.Role, UserProfile.Employee.ToString())])));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(Ip, DeviceLabel)));
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(JwtAudiences.Partner));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Accepting_The_Framework_Contract_Records_The_Version_The_Document_The_Ip_And_The_Device()
    {
        var cleaner = NewCleanerUser();
        var document = FrameworkContract();

        await TestMethod(
            setup: services => SessionOf(services, cleaner),
            arrange: context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                context.Users.Add(cleaner);
                context.Employees.Add(NewEmployee(cleaner));
                context.LegalDocuments.Add(document);
                return Task.CompletedTask;
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var accepted = await mediator.Send(new AcceptLegalDocument.Command(document.TextFor("cs")!.Id));
                var read = await mediator.Send(new GetMyLegalDocuments.Query("cs"));
                return (Accepted: accepted, Read: read);
            },
            assert: async (context, outcome) =>
            {
                Assert.True(outcome.Accepted.IsSuccess, outcome.Accepted.Error?.Message);
                Assert.Equal(document.Version, outcome.Accepted.Value!.Version);

                var row = await context.UserConsents.IgnoreQueryFilters()
                    .SingleAsync(c => c.UserId == cleaner.Id && c.ConsentType == ConsentType.CleanerFrameworkContract);
                Assert.True(row.IsGranted);
                Assert.Equal(document.Version, row.DocumentVersion);
                Assert.Equal(document.Id, row.LegalDocumentId);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(DeviceLabel, row.UserAgent);

                Assert.True(outcome.Read.IsSuccess, outcome.Read.Error?.Message);
                var listed = Assert.Single(outcome.Read.Value!);
                Assert.Equal("cs", listed.Language);
                Assert.True(listed.IsAccepted);
                Assert.Equal(document.Version, listed.AcceptedVersion);
            },
            transactional: false);
    }

    /// <summary>
    /// The consent row is moved in place by the next version; the acceptance rows are the history. After
    /// the cleaner accepts the second version, which contract they accepted first, when, from where and
    /// on which device is still on record — the evidence the self-billed invoices of that time rest on.
    /// </summary>
    [Fact]
    public async Task Accepting_The_Next_Version_Keeps_The_Earlier_Acceptance_Provable()
    {
        var cleaner = NewCleanerUser();
        var employee = NewEmployee(cleaner);
        var first = FrameworkContract(effectiveDaysAgo: 3);
        var second = FrameworkContract(effectiveDaysAgo: 1);

        await TestMethod(
            setup: services => SessionOf(services, cleaner),
            arrange: context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                context.Users.Add(cleaner);
                context.Employees.Add(employee);
                context.LegalDocuments.Add(first);
                return Task.CompletedTask;
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var acceptedFirst = await mediator.Send(new AcceptLegalDocument.Command(first.TextFor("en")!.Id));
                var acceptedAgain = await mediator.Send(new AcceptLegalDocument.Command(first.TextFor("en")!.Id));

                var context = provider.GetRequiredService<Cleansia.Infra.Database.CleansiaDbContext>();
                context.LegalDocuments.Add(second);
                await context.CommitAsync(CancellationToken.None);

                var acceptedSecond = await mediator.Send(new AcceptLegalDocument.Command(second.TextFor("cs")!.Id));
                return (acceptedFirst, acceptedAgain, acceptedSecond);
            },
            assert: async (context, outcome) =>
            {
                Assert.True(outcome.acceptedFirst.IsSuccess, outcome.acceptedFirst.Error?.Message);
                Assert.True(outcome.acceptedAgain.IsSuccess, outcome.acceptedAgain.Error?.Message);
                Assert.True(outcome.acceptedSecond.IsSuccess, outcome.acceptedSecond.Error?.Message);

                var history = await context.CleanerLegalDocumentAcceptances.IgnoreQueryFilters()
                    .Where(a => a.EmployeeId == employee.Id)
                    .OrderBy(a => a.AcceptedOn)
                    .ToListAsync();
                Assert.Equal(2, history.Count);
                Assert.Equal(first.TextFor("en")!.Id, history[0].LegalDocumentTextId);
                Assert.Equal(first.Version, history[0].DocumentVersion);
                Assert.Equal(second.TextFor("cs")!.Id, history[1].LegalDocumentTextId);
                Assert.Equal(second.Version, history[1].DocumentVersion);
                Assert.All(history, a =>
                {
                    Assert.Equal(Ip, a.IpAddress);
                    Assert.Equal(DeviceLabel, a.DeviceLabel);
                    Assert.Equal(JwtAudiences.Partner, a.ClientAudience);
                    Assert.Equal(TestTenants.Default, a.TenantId);
                });

                var consent = await context.UserConsents.IgnoreQueryFilters()
                    .SingleAsync(c => c.UserId == cleaner.Id && c.ConsentType == ConsentType.CleanerFrameworkContract);
                Assert.Equal(second.Id, consent.LegalDocumentId);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Cleaners_Data_Export_Carries_The_Acceptance_With_Its_Ip_And_Device()
    {
        var cleaner = NewCleanerUser();
        var document = FrameworkContract();

        await TestMethod(
            setup: services => SessionOf(services, cleaner),
            arrange: context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                context.Users.Add(cleaner);
                context.Employees.Add(NewEmployee(cleaner));
                context.LegalDocuments.Add(document);
                return Task.CompletedTask;
            },
            act: async provider =>
            {
                var accepted = await provider.GetRequiredService<IMediator>()
                    .Send(new AcceptLegalDocument.Command(document.TextFor("cs")!.Id));
                var export = await provider.GetRequiredService<IGdprExportService>()
                    .BuildAsync(cleaner.Id, cleaner.Email, CancellationToken.None);
                return (Accepted: accepted, Export: export);
            },
            assert: (_, outcome) =>
            {
                Assert.True(outcome.Accepted.IsSuccess, outcome.Accepted.Error?.Message);

                var exported = Assert.Single(outcome.Export.CleanerLegalDocumentAcceptances);
                Assert.Equal(LegalDocumentType.CleanerFrameworkContract, exported.DocumentType);
                Assert.Equal(document.TextFor("cs")!.Id, exported.LegalDocumentTextId);
                Assert.Equal(document.Version, exported.DocumentVersion);
                Assert.Equal("cs", exported.Language);
                Assert.Equal(JwtAudiences.Partner, exported.ClientAudience);
                Assert.Equal(Ip, exported.IpAddress);
                Assert.Equal(DeviceLabel, exported.DeviceLabel);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Text_That_Is_Not_A_Cleaner_Document_In_Force_Is_Refused_And_Writes_Nothing()
    {
        var cleaner = NewCleanerUser();
        var customerTerms = LegalDocument.Create(
            LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));
        customerTerms.AddText("en", "Terms", "## Terms");

        await TestMethod(
            setup: services => SessionOf(services, cleaner),
            arrange: context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                context.Users.Add(cleaner);
                context.Employees.Add(NewEmployee(cleaner));
                context.LegalDocuments.Add(customerTerms);
                return Task.CompletedTask;
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new AcceptLegalDocument.Command(customerTerms.TextFor("en")!.Id)),
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(
                    Core.AppServices.Common.BusinessErrorMessage.LegalDocumentNotInForce,
                    Assert.IsAssignableFrom<IValidationResult>(result).Errors.Single().Message);
                Assert.Empty(await context.UserConsents.IgnoreQueryFilters().Where(c => c.UserId == cleaner.Id).ToListAsync());
                Assert.Empty(await context.CleanerLegalDocumentAcceptances.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }
}
