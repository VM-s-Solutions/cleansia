using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// Two half-crewed orders over real Postgres, differing only in whether the work is finished, because
/// the browse gate now reads <c>OrderAvailability</c> (Q-BROWSE-01 (b)) and that is the ONE column the
/// two disagree about.
///
/// <para><b>The live one</b> — <c>Confirmed</c> + <c>Paid</c>, two required seats, one filled — is the
/// population the redaction exists for: cleaner B is browsing the board and may take seat two. B gets the
/// shape of the work and nothing about the household; A, who is on the job, reads the same row in full
/// through the same route, which is what proves the fixture's values reached the mapper rather than never
/// having been written.</para>
///
/// <para><b>The finished one</b> is the exposure the owner's answer closes as a side effect. Nothing on a
/// terminal transition fills or frees a seat, so a two-seat job one cleaner completed alone kept seat two
/// open forever and the gate kept admitting strangers to it months later. It is no longer browsable at
/// all — while A, who did it, still reads it, because the browse branch is only reached past the strict
/// gate.</para>
/// </summary>
[Collection("PostgresCollection")]
public class OrderDetailBrowsingCleanerRedactionTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-detred";
    private const string CountryId = "country-cz-detred";
    private const string LanguageId = "language-en-detred";

    private const string LiveOrderId = "order-half-crewed-live";
    private const string FinishedOrderId = "order-half-crewed-finished";
    private const string EmployeeAId = "employee-a-detred";
    private const string EmployeeBId = "employee-b-detred";
    private const string UserAId = "user-a-detred";
    private const string UserBId = "user-b-detred";
    private const string EmployeeAEmail = "employee-a-detred@cleansia.test";
    private const string EmployeeBEmail = "employee-b-detred@cleansia.test";

    private const string CustomerName = "Jana Novakova";
    private const string CustomerEmail = "jana.novakova@cleansia.test";
    private const string CustomerPhone = "+420777123456";
    private const string Street = "Vinohradska 12";
    private const string City = "Praha";
    private const string ZipCode = "12000";
    private const string ApproximateAddress = "Praha · 120";
    private const double Latitude = 50.073658;
    private const double Longitude = 14.418540;
    private const string AccessInstructions = "Code 1234 at the gate, second door on the left.";
    private const string SpecialInstructions = "Use the eco products under the sink.";
    private const string CustomerNotes = "Cat is friendly.";
    private const string CompletionNotes = "Balcony door was jammed.";
    private const string ConfirmationCode = "H-SECRET-4242";
    private const string LiveReceiptNumber = "CZ-2026-000123";
    private const string FinishedReceiptNumber = "CZ-2026-000124";
    private const string NoteContent = "Second bathroom needed a re-do.";
    private const string IssueDescription = "Broken tile behind the washing machine.";
    private const string ReviewComment = "Jana was great, the flat is spotless.";
    private const string CleanerAFirstName = "Petra";
    private const string CleanerALastName = "Svobodova";
    private const string CleanerAPhone = "+420602987654";

    [Fact]
    public async Task A_Cleaner_Who_Never_Took_The_Job_Reads_The_Work_And_None_Of_The_Household()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserBId, EmployeeBEmail, EmployeeBId),
            arrange: SeedBothHalfCrewedOrders,
            act: provider => FetchDetailAsync(provider, LiveOrderId),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsSuccess);
                var detail = result.Value!;

                // The scenario itself: an order B could still take, which is exactly why B may open it.
                // Lose either half and the rest of this test proves less.
                Assert.Equal(LiveOrderId, detail.Id);
                Assert.Equal((int)OrderStatus.Confirmed, detail.OrderStatus.Value);

                Assert.Equal(string.Empty, detail.CustomerName);
                Assert.Equal(string.Empty, detail.CustomerEmail);
                Assert.Equal(string.Empty, detail.CustomerPhone);
                Assert.Null(detail.Address);
                Assert.Equal(string.Empty, detail.ConfirmationCode);
                Assert.Null(detail.AccessInstructions);
                Assert.Null(detail.SpecialInstructions);
                Assert.Null(detail.Notes);
                Assert.Null(detail.ReceiptNumber);
                Assert.Empty(detail.OrderNotes);
                Assert.Empty(detail.OrderIssues);

                var crewMember = Assert.Single(detail.AssignedEmployees);
                Assert.Equal(CleanerAFirstName, crewMember.FullName);
                Assert.Null(crewMember.PhoneNumber);

                // The job itself survives, or the browse branch would be closed by accident — and the
                // coarse zone with it, which the board row B tapped through was already showing.
                Assert.Equal(ApproximateAddress, detail.CustomerAddressApproximate);
                Assert.Equal(3, detail.Rooms);
                Assert.Equal(2, detail.Bathrooms);
                Assert.Equal(180, detail.EstimatedTime);
                Assert.Equal(1500m, detail.TotalPrice);
                Assert.False(detail.IsAssignedToCurrentUser);

                // The seat state that is the whole reason B can reach this job: the crew is two, one
                // cleaner is on it, seat two is open. B's detail says so — the board row already did,
                // and the screen B taps Take on used to not.
                Assert.Equal(2, detail.RequiredEmployees);
                Assert.Equal(2, detail.MaxEmployees);
                Assert.Equal(1, detail.AssignedEmployeesCount);
                Assert.Equal(1, detail.AvailableSpots);
                Assert.True(detail.HasAvailableSpots);

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task The_Cleaner_Who_Did_The_Job_Still_Reads_Every_Redacted_Field()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId),
            arrange: SeedBothHalfCrewedOrders,
            act: provider => FetchDetailAsync(provider, FinishedOrderId),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsSuccess);
                var detail = result.Value!;

                Assert.Equal(CustomerName, detail.CustomerName);
                Assert.Equal(CustomerEmail, detail.CustomerEmail);
                Assert.Equal(CustomerPhone, detail.CustomerPhone);
                Assert.Equal(Street, detail.Address!.Street);
                Assert.Equal(City, detail.Address.City);
                Assert.Equal(ZipCode, detail.Address.ZipCode);
                Assert.Equal(Latitude, detail.Address.Latitude);
                Assert.Equal(Longitude, detail.Address.Longitude);
                Assert.Equal(ApproximateAddress, detail.CustomerAddressApproximate);
                Assert.Equal(ConfirmationCode, detail.ConfirmationCode);
                Assert.Equal(AccessInstructions, detail.AccessInstructions);
                Assert.Equal(SpecialInstructions, detail.SpecialInstructions);
                Assert.Equal(CustomerNotes, detail.Notes);
                Assert.Equal(CompletionNotes, detail.CompletionNotes);
                Assert.Equal(FinishedReceiptNumber, detail.ReceiptNumber);
                Assert.Single(detail.OrderNotes, n => n.Content == NoteContent);
                Assert.Single(detail.OrderIssues, i => i.Description == IssueDescription);
                Assert.Equal(ReviewComment, detail.Review!.Comment);

                var crewMember = Assert.Single(detail.AssignedEmployees);
                Assert.Equal($"{CleanerAFirstName} {CleanerALastName}", crewMember.FullName);
                Assert.Equal(CleanerAPhone, crewMember.PhoneNumber);
                Assert.True(detail.IsAssignedToCurrentUser);

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Q-BROWSE-01 (b). The order still has an open seat and always will, so every term the gate used to
    /// consult still admits B — only offerability refuses. Answered as <c>OrderNotFound</c>, the same
    /// refusal a missing order returns, so the response discloses nothing about a household B has no
    /// relationship with.
    /// </summary>
    [Fact]
    public async Task A_Finished_Job_With_A_Seat_Nobody_Will_Ever_Fill_Is_No_Longer_Browsable()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserBId, EmployeeBEmail, EmployeeBId),
            arrange: SeedBothHalfCrewedOrders,
            act: provider => FetchDetailAsync(provider, FinishedOrderId),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Cleaner_Who_Never_Took_The_Job_Gets_No_Photo_Urls()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserBId, EmployeeBEmail, EmployeeBId),
            arrange: SeedBothHalfCrewedOrders,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetOrderPhotos.Query(LiveOrderId)),
            assert: (CleansiaDbContext _, BusinessResult<GetOrderPhotos.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task The_Cleaner_Who_Did_The_Job_Still_Gets_The_Photos_Route()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId),
            arrange: SeedBothHalfCrewedOrders,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetOrderPhotos.Query(FinishedOrderId)),
            assert: (CleansiaDbContext _, BusinessResult<GetOrderPhotos.Response> result) =>
            {
                Assert.True(result.IsSuccess);
                return Task.CompletedTask;
            });
    }

    private static async Task<BusinessResult<OrderItem>> FetchDetailAsync(
        IServiceProvider provider, string orderId) =>
        await provider.GetRequiredService<IMediator>().Send(new GetOrderDetails.Query(orderId));

    private static Task ReplaceWithEmployeeSession(
        IServiceCollection services, string userId, string email, string employeeId)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            userId,
            email,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, employeeId),
            ])));
        return Task.CompletedTask;
    }

    private static async Task SeedBothHalfCrewedOrders(CleansiaDbContext context)
    {
        var language = Language.Create("en", "English");
        language.Id = LanguageId;
        context.Languages.Add(language);

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var employeeA = CreateApprovedEmployee(
            UserAId, EmployeeAId, EmployeeAEmail, CleanerAFirstName, CleanerALastName, CleanerAPhone);
        var employeeB = CreateApprovedEmployee(
            UserBId, EmployeeBId, EmployeeBEmail, "Bohdan", "Bilek", "+420603111222");
        context.Add(employeeA);
        context.Add(employeeB);

        // The live one: everything a Confirmed + Paid card order can carry, receipt included — the
        // webhook generates one at settlement, so it is populated here rather than hand-set on a state
        // that could not produce it.
        var live = NewHalfCrewedOrder(LiveOrderId, employeeA, DateTime.UtcNow.AddDays(3));
        live.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, live));
        live.AddNote(OrderNote.Create(LiveOrderId, EmployeeAId, NoteContent));
        live.AddIssue(OrderIssue.Create(LiveOrderId, EmployeeAId, IssueDescription));
        context.Add(live);
        context.Add(OrderReceipt.Create(
            LiveOrderId, LiveReceiptNumber, "receipt.pdf", "receipts/receipt.pdf", LanguageId));
        context.Add(NewAfterPhoto(LiveOrderId));

        var finished = NewHalfCrewedOrder(FinishedOrderId, employeeA, DateTime.UtcNow.AddDays(-30));
        finished.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, finished));
        finished.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, finished));
        finished.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, finished));
        finished.CompleteOrder(175, CompletionNotes);
        finished.AddNote(OrderNote.Create(FinishedOrderId, EmployeeAId, NoteContent));
        finished.AddIssue(OrderIssue.Create(FinishedOrderId, EmployeeAId, IssueDescription));
        finished.AddReview(OrderReview.Create(FinishedOrderId, "customer-user-detred", 5, ReviewComment));
        context.Add(finished);
        context.Add(OrderReceipt.Create(
            FinishedOrderId, FinishedReceiptNumber, "receipt.pdf", "receipts/receipt.pdf", LanguageId));
        context.Add(NewAfterPhoto(FinishedOrderId));

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewHalfCrewedOrder(string orderId, Employee cleanerA, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: CustomerName,
            customerEmail: CustomerEmail,
            customerPhone: CustomerPhone,
            customerAddress: Address.Create(
                Street, City, ZipCode, CountryId, latitude: Latitude, longitude: Longitude),
            rooms: 3,
            bathrooms: 2,
            extras: new Dictionary<string, bool> { ["insideOven"] = true },
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            specialInstructions: SpecialInstructions,
            accessInstructions: AccessInstructions);
        order.Id = orderId;
        order.Created(TestConstants.TestUserSession.TestUserName, DateTime.UtcNow.AddDays(-33));
        order.UpdateEstimatedTime(180);

        // Two required seats, no spare — the cap that leaves seat two open after A takes it alone.
        order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);

        typeof(Order).GetProperty(nameof(Order.ConfirmationCode))!.SetValue(order, ConfirmationCode);
        typeof(Order).GetProperty(nameof(Order.Notes))!.SetValue(order, CustomerNotes);

        order.AddAssignedEmployee(OrderEmployee.Create(order, cleanerA));
        return order;
    }

    private static OrderPhoto NewAfterPhoto(string orderId) => OrderPhoto.Create(
        orderId: orderId,
        photoType: PhotoType.After,
        blobUrl: "https://account.blob.core.windows.net/order-photos/2026/order/after.jpg",
        fileName: "after.jpg",
        originalFileName: "after.jpg",
        fileSizeBytes: 2048,
        contentType: "image/jpeg",
        capturedByEmployeeId: EmployeeAId);

    private static Employee CreateApprovedEmployee(
        string userId, string employeeId, string email, string first, string last, string phone)
    {
        var user = User.CreateWithPassword(
            email, TestConstants.TestUserSession.TestUserPassword, first, last, UserProfile.Employee);
        user.Id = userId;
        user.ConfirmEmail();
        user.UpdatePhoneNumber(phone);
        user.Created(TestConstants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Approve(approvedByUserId: "admin-detred");
        employee.Created(TestConstants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }
}
