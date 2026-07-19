using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// T-0436 — the employee-affecting admin actions each record a (User, User.Id) audit row so the
/// employee-detail drill-in (T-0295, filtered by ResourceType='User' + the audited User.Id) has content
/// beyond <c>gdpr.user.delete</c>. Asserted at the producer, mirroring
/// <see cref="AuditSensitiveSnapshotTests"/>: a real <see cref="AuditContext"/> is injected, the handler
/// runs, and the drained snapshot keys on the USER id (never the Employee id — the exact mismatch that
/// left the drill-in empty) and carries state fields + ids ONLY, never subject PII or admin free-text.
/// </summary>
public sealed class EmployeeUserAuditCoverageTests
{
    private const string AdminId = "admin-1";
    private const string AdminEmail = "admin@cleansia.test";
    private const string SubjectUserId = "user-9";
    private const string SubjectEmployeeId = "emp-9";
    private const string SubjectFirstName = "Milada";
    private const string SubjectLastName = "Novotna";
    private const string SubjectEmail = "milada.novotna@example.test";
    private const string SubjectPhone = "+420111222333";
    private const string SubjectIban = "CZ6508000000192000145399";
    private const string SubjectPassport = "P1234567";

    // ── frozen labels (mirrors AdminUserPrivilegeAuditLabelTests) ─────────────

    [Theory]
    [InlineData(typeof(ApproveEmployee.Command), "employee.approve")]
    [InlineData(typeof(RejectEmployee.Command), "employee.reject")]
    [InlineData(typeof(AdminUpdateEmployee.Command), "employee.update")]
    [InlineData(typeof(AdminUpdateEmployeeAvailability.Command), "employee.availability.update")]
    public void Employee_Admin_Commands_Carry_The_Frozen_User_Typed_Label(Type commandType, string expectedLabel)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expectedLabel, descriptor.Action);
        Assert.Equal("User", descriptor.ResourceType);
        Assert.False(descriptor.Sensitive);
        Assert.True(descriptor.Audited);
    }

    [Fact]
    public void The_SelfService_Employee_Edit_Is_Not_Enrolled_In_User_Typed_Coverage()
    {
        var descriptor = AuditActionDescriptor.For(typeof(UpdateEmployee.Command));

        Assert.Null(descriptor.ResourceType);
        Assert.Equal("UpdateEmployee", descriptor.Action);
    }

    // ── ApproveEmployee ───────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveEmployee_Emits_Before_After_ContractStatus_Keyed_On_The_UserId()
    {
        var auditContext = new AuditContext();
        var employee = BuildEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        var handler = new ApproveEmployee.Handler(
            employeeRepository.Object, AdminUserRepository().Object, AdminSession(), auditContext);
        var result = await handler.Handle(
            new ApproveEmployee.Command(SubjectEmployeeId, "country-cz", "fast-track onboarding"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(SubjectUserId, snapshot.ResourceId);
        Assert.Contains($"\"status\":{(int)ContractStatus.Pending}", snapshot.BeforeJson);
        Assert.Contains($"\"status\":{(int)ContractStatus.Approved}", snapshot.AfterJson);
        Assert.Contains("\"workCountryId\":\"country-cz\"", snapshot.AfterJson);
        Assert.Contains($"\"employeeId\":\"{SubjectEmployeeId}\"", snapshot.AfterJson);
        // The admin's free-text notes never enter the snapshot (could carry subject PII).
        Assert.DoesNotContain("fast-track onboarding", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    [Fact]
    public async Task ApproveEmployee_On_Failure_Emits_No_Snapshot()
    {
        var auditContext = new AuditContext();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetQueryable()).Returns(Array.Empty<Employee>().AsQueryable().BuildMock());

        var handler = new ApproveEmployee.Handler(
            employeeRepository.Object, AdminUserRepository().Object, AdminSession(), auditContext);
        var result = await handler.Handle(
            new ApproveEmployee.Command("missing-emp", "country-cz"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(auditContext.DrainSnapshot());
    }

    // ── RejectEmployee ────────────────────────────────────────────────────────

    [Fact]
    public async Task RejectEmployee_Emits_Before_After_ContractStatus_Keyed_On_The_UserId()
    {
        var auditContext = new AuditContext();
        var employee = BuildEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(SubjectEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var handler = new RejectEmployee.Handler(
            employeeRepository.Object, AdminUserRepository().Object, AdminSession(), auditContext);
        var result = await handler.Handle(
            new RejectEmployee.Command(SubjectEmployeeId, "documents look forged"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(SubjectUserId, snapshot.ResourceId);
        Assert.Contains($"\"status\":{(int)ContractStatus.Pending}", snapshot.BeforeJson);
        Assert.Contains($"\"status\":{(int)ContractStatus.Rejected}", snapshot.AfterJson);
        Assert.Contains($"\"employeeId\":\"{SubjectEmployeeId}\"", snapshot.AfterJson);
        Assert.DoesNotContain("documents look forged", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── AdminUpdateEmployee ───────────────────────────────────────────────────

    [Fact]
    public async Task AdminUpdateEmployee_Emits_Ids_Only_Never_The_Edited_Profile_Values()
    {
        var auditContext = new AuditContext();
        var employee = BuildEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(SubjectEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        var geocoder = new Mock<IAddressGeocoder>();

        var handler = new AdminUpdateEmployee.Handler(employeeRepository.Object, geocoder.Object, auditContext);
        var result = await handler.Handle(
            new AdminUpdateEmployee.Command(
                SubjectEmployeeId,
                FirstName: "Renamed",
                LastName: null,
                BirthDate: default,
                Phone: "+420999888777",
                Street: "New Street 5",
                City: "Brno",
                ZipCode: "60200",
                CountryId: "country-cz",
                State: null,
                NationalityId: null,
                PassportId: null,
                EntityType: null,
                RegistrationNumber: null,
                VatNumber: null,
                LegalEntityName: null,
                Iban: null,
                EmergencyName: null,
                EmergencyPhone: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(SubjectUserId, snapshot.ResourceId);
        Assert.Contains($"\"userId\":\"{SubjectUserId}\"", snapshot.AfterJson);
        Assert.Contains($"\"employeeId\":\"{SubjectEmployeeId}\"", snapshot.AfterJson);
        Assert.DoesNotContain("Renamed", snapshot.AfterJson);
        Assert.DoesNotContain("+420999888777", snapshot.AfterJson);
        Assert.DoesNotContain("New Street 5", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── AdminUpdateEmployeeAvailability ───────────────────────────────────────

    [Fact]
    public async Task AdminUpdateEmployeeAvailability_Emits_Ids_Only_Never_The_Schedule_Values()
    {
        var auditContext = new AuditContext();
        var employee = BuildEmployee();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository.Setup(r => r.GetByIdAsync(SubjectEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var handler = new AdminUpdateEmployeeAvailability.Handler(employeeRepository.Object, auditContext);
        var result = await handler.Handle(
            new AdminUpdateEmployeeAvailability.Command(
                SubjectEmployeeId,
                new Dictionary<string, List<AdminUpdateEmployeeAvailability.TimeRangeDto>>
                {
                    ["Monday"] = [new AdminUpdateEmployeeAvailability.TimeRangeDto("08:00", "12:00")]
                }),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(SubjectUserId, snapshot.ResourceId);
        Assert.Contains($"\"userId\":\"{SubjectUserId}\"", snapshot.AfterJson);
        Assert.Contains($"\"employeeId\":\"{SubjectEmployeeId}\"", snapshot.AfterJson);
        Assert.DoesNotContain("08:00", snapshot.AfterJson);
        Assert.DoesNotContain("Monday", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── the admin gate still scopes the coverage (S1) ─────────────────────────

    [Fact]
    public async Task A_Partner_Role_Caller_Produces_No_Audit_Row_For_An_Employee_Command()
    {
        var writer = new Mock<IAuditWriter>();
        var sink = new Mock<IAuditFailureSink>();
        var session = new TestUserSessionProvider(
            "partner-1", "partner@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Employee.ToString())]);
        var behavior = new AuditLogBehavior<RejectEmployee.Command, BusinessResult>(
            session, new AuditContext(), writer.Object, sink.Object, new AuditEntryFactory(session),
            NullLogger<AuditLogBehavior<RejectEmployee.Command, BusinessResult>>.Instance);

        await behavior.Handle(
            new RejectEmployee.Command(SubjectEmployeeId),
            _ => Task.FromResult(BusinessResult.Success()),
            CancellationToken.None);

        writer.Verify(w => w.Add(It.IsAny<AdminActionAudit>()), Times.Never);
        sink.Verify(s => s.RecordFailureAsync(It.IsAny<AdminActionAudit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IUserSessionProvider AdminSession()
    {
        var mock = new Mock<IUserSessionProvider>();
        mock.Setup(s => s.GetUserId()).Returns(AdminId);
        mock.Setup(s => s.GetUserEmail()).Returns(AdminEmail);
        return mock.Object;
    }

    private static Mock<IUserRepository> AdminUserRepository()
    {
        var adminUser = User.CreateWithPassword(AdminEmail, "Passw0rd!", "Ada", "Min", UserProfile.Administrator);
        adminUser.Id = AdminId;
        var mock = new Mock<IUserRepository>();
        mock.Setup(r => r.GetByEmailAsync(AdminEmail, It.IsAny<CancellationToken>())).ReturnsAsync(adminUser);
        return mock;
    }

    private static Employee BuildEmployee()
    {
        var user = User.CreateWithPassword(SubjectEmail, "Passw0rd!", SubjectFirstName, SubjectLastName, UserProfile.Employee);
        user.Id = SubjectUserId;
        user.Update(SubjectFirstName, SubjectLastName, SubjectPhone, new DateOnly(1990, 1, 1));

        var employee = Employee.CreateWithUser(user);
        employee.Id = SubjectEmployeeId;
        employee.UpdateEmployeeDetails(
            EmployeeEntityType.NaturalPerson,
            registrationNumber: "12345678",
            vatNumber: null,
            legalEntityName: null,
            nationalityId: "country-cz",
            passportId: SubjectPassport,
            iban: SubjectIban,
            address: Address.Create("Wenceslas Square 1", "Prague", "11000", "country-cz"),
            availability: new Dictionary<string, List<TimeRange>>(),
            emergencyContactName: null,
            emergencyContactPhone: null);

        return employee;
    }

    private static void AssertNoSubjectPii(AuditSnapshot snapshot)
    {
        foreach (var json in new[] { snapshot.BeforeJson, snapshot.AfterJson })
        {
            if (json is null)
            {
                continue;
            }

            Assert.DoesNotContain(SubjectFirstName, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SubjectLastName, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SubjectEmail, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SubjectPhone, json);
            Assert.DoesNotContain(SubjectIban, json);
            Assert.DoesNotContain(SubjectPassport, json);
            Assert.DoesNotContain("@", json);
        }
    }
}
