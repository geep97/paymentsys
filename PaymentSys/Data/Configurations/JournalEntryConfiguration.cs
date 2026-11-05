using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentSys.Models.Ledger;

namespace PaymentSys.Data.Configurations
{
    public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Reference)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(e => e.Reference)
                .IsUnique();

            builder.Property(e => e.Narration)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .IsRequired();

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.HasOne(e => e.PaymentTransaction)
                .WithMany()
                .HasForeignKey(e => e.PaymentTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.ReversalOf)
                .WithMany()
                .HasForeignKey(e => e.ReversalOfId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => e.PaymentTransactionId);

            builder.HasIndex(e => e.ReversalOfId);

            builder.ToTable(t =>
            {
                t.HasTrigger("TR_JournalEntries_Immutable");
                t.HasTrigger("TR_JournalEntries_EnforceBalance");

                t.HasCheckConstraint(
                    "CK_JournalEntries_CurrencyCode",
                    "[CurrencyCode] = 'GHS'");
            });
        }
    }
}
