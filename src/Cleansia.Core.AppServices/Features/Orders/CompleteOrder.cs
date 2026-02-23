using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CompleteOrder
{
    public record Command(
        string OrderId,
        string EmployeeId,
        int ActualCompletionTimeMinutes,
        string? CompletionNotes = null) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus NewStatus,
        int ActualCompletionTime);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IOrderPhotoRepository _orderPhotoRepository;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            IOrderPhotoRepository orderPhotoRepository)
        {
            _orderRepository = orderRepository;
            _employeeRepository = employeeRepository;
            _orderPhotoRepository = orderPhotoRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(OrderIsInProgressAsync)
                .WithMessage(BusinessErrorMessage.OrderNotInProgress)
                .MustAsync(HasAfterPhotosAsync)
                .WithMessage(BusinessErrorMessage.AfterPhotosRequired);

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound)
                .MustAsync(HasCompletedProfileAsync)
                .WithMessage(BusinessErrorMessage.EmployeeProfileIncomplete)
                .MustAsync(HasUploadedDocumentsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeDocumentsMissing);

            RuleFor(x => x.ActualCompletionTimeMinutes)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.ActualTimeMustBePositive);

            RuleFor(x => x)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);

            When(x => !string.IsNullOrEmpty(x.CompletionNotes), () =>
            {
                RuleFor(x => x.CompletionNotes)
                    .MaximumLength(1000)
                    .WithMessage(BusinessErrorMessage.CompletionNotesTooLong);
            });
        }

        private async Task<bool> OrderIsInProgressAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null) return false;

            var currentStatus = order.OrderStatusHistory
                .OrderByDescending(osh => osh.CreatedOn)
                .FirstOrDefault()?.Status;

            return currentStatus == OrderStatus.InProgress;
        }

        private async Task<bool> EmployeeIsAssignedToOrderAsync(Command command, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.AssignedEmployees.Any(oe => oe.EmployeeId == command.EmployeeId) ?? false;
        }

        private async Task<bool> HasCompletedProfileAsync(string employeeId, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository
                .GetQueryable()
                .Include(e => e.Address)
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

            if (employee == null) return false;

            return !string.IsNullOrWhiteSpace(employee.ICO) &&
                   employee.Address is not null &&
                   employee.Availability.Any();
        }

        private async Task<bool> HasUploadedDocumentsAsync(string employeeId, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);

            if (employee == null) return false;

            return employee.ContractStatus != Domain.Enums.ContractStatus.Pending;
        }

        private async Task<bool> HasAfterPhotosAsync(string orderId, CancellationToken cancellationToken)
        {
            var photoCount = await _orderPhotoRepository
                .GetPhotoCountByOrderIdAndTypeAsync(orderId, Domain.Enums.PhotoType.After, cancellationToken);

            return photoCount > 0;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IReceiptService receiptService,
        IEmailService emailService)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.SelectedServices).ThenInclude(os => os.Service)
                .Include(o => o.SelectedPackages).ThenInclude(op => op.Package)
                .Include(o => o.CustomerAddress).ThenInclude(a => a!.Country)
                .Include(o => o.Currency)
                .Include(o => o.Receipt)
                .Include(o => o.User).ThenInclude(u => u!.PreferredLanguage)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            order.CompleteOrder(command.ActualCompletionTimeMinutes, command.CompletionNotes);

            var completedStatusTrack = OrderStatusTrack.Create(OrderStatus.Completed, order);
            order.AddOrderStatus(completedStatusTrack);

            // Generate and send receipt (skip if one already exists for this order)
            if (order.Receipt != null)
            {
                return BusinessResult.Success(new Response(
                    OrderId: order.Id,
                    NewStatus: OrderStatus.Completed,
                    ActualCompletionTime: command.ActualCompletionTimeMinutes
                ));
            }

            try
            {
                // Use user's preferred language or default to Czech for receipts
                var languageCode = order.User?.PreferredLanguageCode ?? "cs";
                var receipt = await receiptService.GenerateReceiptAsync(order, languageCode, cancellationToken);

                // Send receipt email asynchronously (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var pdfBytes = await receiptService.DownloadReceiptPdfAsync(receipt, cancellationToken);
                        await emailService.SendOrderReceiptEmailAsync(
                            order.CustomerEmail,
                            order,
                            pdfBytes,
                            receipt.FileName,
                            languageCode,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't fail the order completion
                        // TODO: Add proper logging
                        Console.WriteLine($"Failed to send receipt email: {ex.Message}");
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the order completion
                // TODO: Add proper logging
                Console.WriteLine($"Failed to generate receipt: {ex.Message}");
            }

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                NewStatus: OrderStatus.Completed,
                ActualCompletionTime: command.ActualCompletionTimeMinutes
            ));
        }
    }
}
