using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentSys.Models.Ledger;

namespace PaymentSys.Data.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(a => a.AccountNumber)
                .HasMaxLength(20)
                .IsRequired();

            builder.HasIndex(a => a.AccountNumber)
                .IsUnique();

            builder.Property(a => a.Type)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(a => a.CurrencyCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .IsRequired();

            builder.Property(a => a.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.Property(a => a.RowVersion)
                .IsRowVersion();

            builder.ToTable(t => t.HasCheckConstraint(
                "CK_Accounts_CurrencyCode",
                "[CurrencyCode] = 'GHS'"));
        }
    }
}
