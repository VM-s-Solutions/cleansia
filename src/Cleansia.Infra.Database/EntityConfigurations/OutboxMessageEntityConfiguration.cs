using Cleansia.Core.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OutboxMessageEntityConfiguration : AuditableEntityConfiguration<OutboxMessage, string>
{
    public override void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        base.Configure(builder);

        builder.ToTable("OutboxMessages");

        builder.Property(e => e.QueueName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.MessageKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Body)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.AttemptCount)
            .IsRequired();

        builder.Property(e => e.ClaimedBy)
            .HasMaxLength(128);

        builder.Property(e => e.LastError)
            .HasColumnType("text");

        // (QueueName, MessageKey) is unique WITHOUT TenantId: the key already embeds a globally-unique
        // id per logical effect, so collapsing a double-enqueue to one row is correct, and adding the
        // tenant would weaken dedup for null-tenant rows (the IX_OrderReceipts_OrderId exception).
        builder.HasIndex(e => new { e.QueueName, e.MessageKey })
            .IsUnique()
            .HasDatabaseName("IX_OutboxMessages_QueueName_MessageKey");

        // The drainer's claim query scans only due, undispatched rows.
        builder.HasIndex(e => e.NextAttemptAt)
            .HasDatabaseName("IX_OutboxMessages_NextAttemptAt_Pending")
            .HasFilter($"\"Status\" = {(int)OutboxMessageStatus.Pending}");
    }
}
