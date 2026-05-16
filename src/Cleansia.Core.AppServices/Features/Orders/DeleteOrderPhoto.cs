using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class DeleteOrderPhoto
{
    public record Command(string PhotoId) : ICommand<Response>;

    public record Response(bool Success);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderPhotoRepository photoRepository)
        {
            RuleFor(x => x.PhotoId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(photoRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound);
        }
    }

    public class Handler(
        IOrderPhotoRepository photoRepository,
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService,
        IBlobContainerClientFactory blobClientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var photo = await photoRepository.GetByIdAsync(command.PhotoId, cancellationToken);
            if (photo == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.PhotoId), BusinessErrorMessage.NotFound));
            }

            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.PhotoId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == photo.OrderId, cancellationToken);

            if (order == null || !order.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.PhotoId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            try
            {
                var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
                var blobName = ExtractBlobNameFromUrl(photo.BlobUrl);
                await blobClient.DeleteAsync(blobName, cancellationToken);
            }
            catch
            {
            }

            photoRepository.Remove(photo);

            return BusinessResult.Success(new Response(Success: true));
        }

        private static string ExtractBlobNameFromUrl(string blobUrl)
        {
            var uri = new Uri(blobUrl);
            var segments = uri.Segments.Skip(2);
            return string.Join("", segments);
        }
    }
}
