using System.Reflection;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// What EF does when it loads an order with its receipts: the receipt joins the order's collection, which
/// has no public writer. A sale receipt attached this way is the order's <see cref="Order.Receipt"/>.
/// </summary>
internal static class OrderReceiptAttachment
{
    public static void Attach(Order order, OrderReceipt receipt) =>
        ((ICollection<OrderReceipt>)typeof(Order)
            .GetField("_receipts", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(order)!).Add(receipt);
}
