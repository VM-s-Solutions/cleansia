using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.EmployeeDocuments;

public class GetMyDocumentsHandlerTests
{
    private const string UserEmail = "cleaner@cleansia.cz";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmployeeDocumentRepository> _documentRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    [Fact]
    public async Task Returns_Every_Active_Document_Even_When_Two_Share_A_File_Name()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        user.Id = "user-1";
        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";

        var identityCard = EmployeeDocument.Create(
            employee.Id, "image.jpg", "path/id.jpg", "image/jpeg", 2048, DocumentType.IdentityCard, null, user.Id);
        var workPermit = EmployeeDocument.Create(
            employee.Id, "image.jpg", "path/permit.jpg", "image/jpeg", 2048, DocumentType.WorkPermit, null, user.Id);

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _documentRepository
            .Setup(r => r.GetByEmployeeIdAsync(employee.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([identityCard, workPermit]);

        var handler = new GetMyDocuments.Handler(
            _employeeRepository.Object,
            _documentRepository.Object,
            _userRepository.Object,
            _session.Object);

        var result = await handler.Handle(new GetMyDocuments.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { identityCard.Id, workPermit.Id }.Order(),
            result.Value.Documents.Select(d => d.DocumentId).Order());
        Assert.Equal(
            [DocumentType.IdentityCard, DocumentType.WorkPermit],
            result.Value.Documents.Select(d => d.DocumentType).Order());
    }
}
