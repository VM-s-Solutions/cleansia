using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Credit.Admin;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Credit;

public class IssueCustomerCreditHandlerTests
{
    [Fact]
    public async Task AnErasedSubject_RefusedByTheLockedAccountLookup_ReceivesNoGrantOrSuccessAudit()
    {
        var accounts = new Mock<ICreditAccountRepository>();
        accounts.Setup(r => r.EnsureForUserAsync("erased-user", "currency-czk", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreditAccount?)null);
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns("admin-1");
        var audit = new Mock<IAuditContext>();
        var handler = new IssueCustomerCredit.Handler(accounts.Object, session.Object, audit.Object);

        var result = await handler.Handle(new IssueCustomerCredit.Command(
            "erased-user", 500m, "currency-czk", CreditTransactionReason.Goodwill,
            "A queued grant submitted before erasure completed.", "grant-erased-user"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(IssueCustomerCredit.Command.UserId), result.Error!.Code);
        Assert.Equal(BusinessErrorMessage.UserNotFound, result.Error.Message);
        accounts.Verify(r => r.EnsureForUserAsync("erased-user", "currency-czk", It.IsAny<CancellationToken>()), Times.Once);
        accounts.VerifyNoOtherCalls();
        audit.VerifyNoOtherCalls();
    }
}
