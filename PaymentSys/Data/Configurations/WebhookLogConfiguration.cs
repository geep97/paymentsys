using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentSys.Models.Payments;

namespace PaymentSys.Data.Configurations
{
    public class WebhookLogConfiguration : IEntityTypeConfiguration<WebhookLog>
    {
        public void Configure(EntityTypeBuilder<WebhookLog> builder)
        {
            builder.HasKey(w => w.Id);

            builder.Property(w => w.EventId)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(w => w.EventType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(w => w.Provider)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(w => w.Payload)
                .IsRequired();

            builder.Property(w => w.ErrorMessage)
                .HasMaxLength(1000);

            builder.Property(w => w.ReceivedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.HasIndex(w => new { w.Provider, w.EventId })
                .IsUnique();

            builder.HasIndex(w => w.IsProcessed);
        }
    }
}
