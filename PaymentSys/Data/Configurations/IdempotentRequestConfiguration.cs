using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentSys.Models.Idempotency;

namespace PaymentSys.Data.Configurations
{
    public class IdempotentRequestConfiguration : IEntityTypeConfiguration<IdempotentRequest>
    {
        public void Configure(EntityTypeBuilder<IdempotentRequest> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.IdempotencyKey)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(r => r.Endpoint)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(r => r.RequestHash)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(r => r.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(r => r.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.Property(r => r.RowVersion)
                .IsRowVersion();

            builder.HasIndex(r => new { r.Endpoint, r.IdempotencyKey })
                .IsUnique();

            builder.HasIndex(r => r.ExpiresAt);

            builder.HasIndex(r => r.Status);
        }
    }
}
