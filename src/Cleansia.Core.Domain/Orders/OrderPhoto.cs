using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Orders;

public class OrderPhoto : Auditable, ITenantEntity
{
    [Required]
    public string OrderId { get; private set; }
    public Order Order { get; private set; }

    [Required]
    public PhotoType PhotoType { get; private set; }

    [Required]
    [MaxLength(500)]
    public string BlobUrl { get; private set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; private set; }

    [MaxLength(255)]
    public string? OriginalFileName { get; private set; }

    public long FileSizeBytes { get; private set; }

    [MaxLength(50)]
    public string ContentType { get; private set; }

    public DateTime CapturedAt { get; private set; }

    [Required]
    public string CapturedByEmployeeId { get; private set; }
    public Employee CapturedBy { get; private set; }

    public int? Width { get; private set; }
    public int? Height { get; private set; }

    [MaxLength(500)]
    public string? Notes { get; private set; }

    public static OrderPhoto Create(
        string orderId,
        PhotoType photoType,
        string blobUrl,
        string fileName,
        string originalFileName,
        long fileSizeBytes,
        string contentType,
        string capturedByEmployeeId,
        string? notes = null,
        int? width = null,
        int? height = null)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Order ID is required", nameof(orderId));

        if (string.IsNullOrWhiteSpace(blobUrl))
            throw new ArgumentException("Blob URL is required", nameof(blobUrl));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required", nameof(fileName));

        if (string.IsNullOrWhiteSpace(capturedByEmployeeId))
            throw new ArgumentException("Captured by employee ID is required", nameof(capturedByEmployeeId));

        if (fileSizeBytes <= 0)
            throw new ArgumentException("File size must be positive", nameof(fileSizeBytes));

        return new OrderPhoto
        {
            OrderId = orderId,
            PhotoType = photoType,
            BlobUrl = blobUrl,
            FileName = fileName,
            OriginalFileName = originalFileName,
            FileSizeBytes = fileSizeBytes,
            ContentType = contentType,
            CapturedAt = DateTime.UtcNow,
            CapturedByEmployeeId = capturedByEmployeeId,
            Notes = notes,
            Width = width,
            Height = height
        };
    }

    /// <summary>
    /// The free text an erasure has to reach on a row it KEEPS. The order aggregate is retained and
    /// anonymized rather than deleted, and its own walk already blanks the review, note and issue text; the
    /// photo row was simply not in that walk, so the uploader's own file name — which routinely carries the
    /// person's name — and the free-text note survived alongside a blob the erasure had already deleted.
    /// <see cref="BlobUrl"/> and <see cref="FileName"/> stay: they are the platform's own storage keys, not
    /// the subject's text, and blanking them would leave the deleted blob unnameable.
    /// </summary>
    public OrderPhoto Anonymize()
    {
        OriginalFileName = AnonymizationMarker.Value;
        Notes = null;
        return this;
    }
}
