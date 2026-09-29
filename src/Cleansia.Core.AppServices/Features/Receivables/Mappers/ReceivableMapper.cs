using Cleansia.Core.AppServices.Features.Receivables.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.AppServices.Features.Receivables.Mappers;

public static class ReceivableMapper
{
    public static ReceivableListItem MapToListItem(this Receivable receivable) =>
        new(
            Id: receivable.Id,
            OrderId: receivable.OrderId,
            DisplayOrderNumber: receivable.Order!.DisplayOrderNumber,
            UserId: receivable.UserId,
            Kind: receivable.Kind.MapToCode(),
            Status: receivable.Status.MapToCode(),
            Amount: receivable.Amount,
            CurrencyCode: receivable.Currency!.Code,
            Attempts: receivable.Attempts,
            CreatedOn: receivable.CreatedOn,
            WrittenOffOn: receivable.WrittenOffOn,
            WriteOffNote: receivable.WriteOffNote);

    public static MyReceivableDto MapToMyDto(this Receivable receivable) =>
        new(
            Id: receivable.Id,
            OrderId: receivable.OrderId,
            DisplayOrderNumber: receivable.Order!.DisplayOrderNumber,
            Kind: receivable.Kind.MapToCode(),
            Amount: receivable.Amount,
            CurrencyCode: receivable.Currency!.Code,
            CreatedOn: receivable.CreatedOn);
}
