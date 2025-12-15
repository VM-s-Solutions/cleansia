using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetOrderPhotos
{
    public record Query(string OrderId) : IQuery<Response>;

    public record Response(
        IEnumerable<OrderPhotoDto> Photos,
        int BeforePhotoCount,
        int AfterPhotoCount);

    public record OrderPhotoDto(
        string Id,
        PhotoType PhotoType,
        string BlobUrl,
        string FileName,
        string? OriginalFileName,
        long FileSizeBytes,
        string ContentType,
        DateTime CapturedAt,
        string CapturedByEmployeeId,
        string? CapturedByEmployeeName,
        int? Width,
        int? Height,
        string? Notes);

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }
    }

    public class Handler(IOrderPhotoRepository photoRepository) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var photos = await photoRepository.GetPhotosByOrderIdAsync(query.OrderId, cancellationToken);

            var photoDtos = photos.Select(p => new OrderPhotoDto(
                Id: p.Id,
                PhotoType: p.PhotoType,
                BlobUrl: p.BlobUrl,
                FileName: p.FileName,
                OriginalFileName: p.OriginalFileName,
                FileSizeBytes: p.FileSizeBytes,
                ContentType: p.ContentType,
                CapturedAt: p.CapturedAt,
                CapturedByEmployeeId: p.CapturedByEmployeeId,
                CapturedByEmployeeName: p.CapturedBy != null
                    ? $"{p.CapturedBy.User?.FirstName} {p.CapturedBy.User?.LastName}".Trim()
                    : null,
                Width: p.Width,
                Height: p.Height,
                Notes: p.Notes
            )).ToList();

            var beforeCount = await photoRepository.GetPhotoCountByOrderIdAndTypeAsync(
                query.OrderId,
                PhotoType.Before,
                cancellationToken);

            var afterCount = await photoRepository.GetPhotoCountByOrderIdAndTypeAsync(
                query.OrderId,
                PhotoType.After,
                cancellationToken);

            return BusinessResult.Success(new Response(
                Photos: photoDtos,
                BeforePhotoCount: beforeCount,
                AfterPhotoCount: afterCount));
        }
    }
}
