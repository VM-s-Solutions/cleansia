using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.EmployeeDocuments;

/// <summary>
/// Characterization of the employee-documents paged list across the A5/A2
/// canonicalization (hand-built new PagedData + public Handler -> internal Handler
/// + items.MapToDto(total, request) via the GetPagedSort spec path). Pins the row
/// projection, page metadata, and that the filter inputs (active default true,
/// employeeId, status) reach the spec.
/// </summary>
public class GetEmployeeDocumentsHandlerTests
{
    private const string EmployeeId = "emp-1";

    private readonly Mock<IEmployeeDocumentRepository> _repository = new();

    private Task<PagedData<EmployeeDocumentItem>> Handle(GetEmployeeDocuments.Request request)
    {
        var handlerType = typeof(GetEmployeeDocuments).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<EmployeeDocumentItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static EmployeeDocument Document()
    {
        var document = EmployeeDocument.Create(
            employeeId: EmployeeId,
            fileName: "passport.pdf",
            filePath: "docs/passport.pdf",
            contentType: "application/pdf",
            fileSizeBytes: 1024,
            documentType: DocumentType.Passport,
            description: "ID doc",
            createdBy: "system");
        document.Id = "doc-1";
        return document;
    }

    [Fact]
    public async Task Projects_Row_And_PageMetadata()
    {
        var document = Document();
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<EmployeeDocument, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(9);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.EmployeeDocumentSort>(
                10, 5, It.IsAny<Expression<Func<EmployeeDocument, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { document }.AsQueryable().BuildMock());

        var result = await Handle(new GetEmployeeDocuments.Request { Offset = 10, Limit = 5 });

        Assert.Equal(9, result.Total);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(5, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("doc-1", row.Id);
        Assert.Equal("passport.pdf", row.FileName);
        Assert.Equal(EmployeeId, row.EmployeeId);
        Assert.Equal(DocumentType.Passport, row.DocumentType);
    }

    [Fact]
    public async Task Filter_Inputs_Reach_Specification_With_Active_Default_True()
    {
        Expression<Func<EmployeeDocument, bool>>? captured = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<EmployeeDocument, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<EmployeeDocument, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.EmployeeDocumentSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<EmployeeDocument, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<EmployeeDocument>().AsQueryable().BuildMock());

        await Handle(new GetEmployeeDocuments.Request
        {
            Filter = new EmployeeDocumentFilter { EmployeeId = EmployeeId, Status = DocumentStatus.Pending }
        });

        Assert.NotNull(captured);
        var predicate = captured!.Compile();

        // EmployeeDocument.Create defaults Status to Pending — matches the filter.
        var match = Document();
        Assert.True(predicate(match));

        var wrongEmployee = EmployeeDocument.Create(
            "other-emp", "x.pdf", "p", "application/pdf", 1, DocumentType.Passport, null, "system");
        Assert.False(predicate(wrongEmployee));

        var inactive = Document();
        inactive.IsActive = false;
        Assert.False(predicate(inactive));
    }
}
