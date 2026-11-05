using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentSys.Models.Ledger;

namespace PaymentSys.Data.Configurations
{
    public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
    {
        public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
        {
            builder.HasKey(l => l.Id);

            builder.HasOne(l => l.JournalEntry)
                .WithMany(e => e.Lines)
                .HasForeignKey(l => l.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(l => l.Account)
                .WithMany(a => a.JournalEntryLines)
                .HasForeignKey(l => l.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(l => l.JournalEntryId);

            builder.HasIndex(l => l.AccountId);

            builder.ToTable(t =>
            {
                t.HasTrigger("TR_JournalEntryLines_Immutable");

                t.HasCheckConstraint(
                    "CK_JournalEntryLines_SingleSide",
                    "[DebitPesewas] >= 0 AND [CreditPesewas] >= 0 AND NOT ([DebitPesewas] > 0 AND [CreditPesewas] > 0) AND ([DebitPesewas] > 0 OR [CreditPesewas] > 0)");
            });
        }
    }
}
