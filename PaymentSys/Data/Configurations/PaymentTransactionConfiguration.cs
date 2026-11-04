using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentSys.Models.Payments;

namespace PaymentSys.Data.Configurations
{
    public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
    {
        public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Reference)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(p => p.Reference)
                .IsUnique();

            builder.Property(p => p.AccessCode)
                .HasMaxLength(100);

            builder.Property(p => p.Email)
                .HasMaxLength(254)
                .IsRequired();

            builder.Property(p => p.Currency)
                .HasMaxLength(3)
                .IsFixedLength()
                .IsRequired();

            builder.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(p => p.Provider)
                .HasMaxLength(50);

            builder.Property(p => p.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.Property(p => p.UpdatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.Property(p => p.RowVersion)
                .IsRowVersion();

            builder.HasIndex(p => p.Status);

            builder.HasIndex(p => p.CreatedAt);

            builder.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PaymentTransactions_AmountPesewas",
                    "[AmountPesewas] > 0");

                t.HasCheckConstraint(
                    "CK_PaymentTransactions_Currency",
                    "[Currency] = 'GHS'");
            });
        }
    }
}
