using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

/// <summary>
/// The admin queue of deletion requests.
///
/// <para>Defaults to <c>Pending</c> because the queue is a to-do list: an admin opening it wants what
/// is still waiting on them, not a history. Answered requests are reachable by asking for a status,
/// so the record is not hidden — it is just not what the screen opens on.</para>
/// </summary>
public class GetDocumentDeletionRequests
{
    public record Request(DocumentDeletionRequestStatus? Status = DocumentDeletionRequestStatus.Pending)
        : IRequest<IEnumerable<DocumentDeletionRequestDto>>;

    public class Handler(IDocumentDeletionRequestRepository repository)
        : IRequestHandler<Request, IEnumerable<DocumentDeletionRequestDto>>
    {
        public async Task<IEnumerable<DocumentDeletionRequestDto>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            var query = repository.GetQueryable()
                .Include(r => r.Document)
                    .ThenInclude(d => d!.Employee)
                        .ThenInclude(e => e!.User);

            var filtered = request.Status is null
                ? query.Where(_ => true)
                : query.Where(r => r.Status == request.Status);

            var rows = await filtered
                // Oldest first: a queue that surfaces the newest request leaves the person who has
                // been waiting longest at the bottom of it.
                .OrderBy(r => r.CreatedOn)
                .ToListAsync(cancellationToken);

            return rows.Select(r => new DocumentDeletionRequestDto(
                Id: r.Id,
                DocumentId: r.DocumentId,
                EmployeeId: r.EmployeeId,
                EmployeeName: FullName(r),
                DocumentFileName: r.Document?.FileName ?? string.Empty,
                DocumentType: r.Document?.DocumentType ?? DocumentType.Other,
                Reason: r.Reason,
                Status: r.Status,
                ReviewNotes: r.ReviewNotes,
                CreatedOn: r.CreatedOn,
                ReviewedAt: r.ReviewedAt));
        }

        /// <summary>
        /// Best effort, and empty rather than a placeholder when the chain is broken. An admin queue
        /// row reading "Unknown" is worse than one reading nothing: it looks like data.
        /// </summary>
        private static string FullName(Domain.Documents.DocumentDeletionRequest request)
        {
            var user = request.Document?.Employee?.User;
            if (user is null)
            {
                return string.Empty;
            }

            return $"{user.FirstName} {user.LastName}".Trim();
        }
    }
}
