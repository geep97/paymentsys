using Microsoft.EntityFrameworkCore;
using PaymentSys.Models.Idempotency;
using PaymentSys.Models.Ledger;
using PaymentSys.Models.Payments;

namespace PaymentSys.Data
{
    public class PaymentDbContext : DbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts => Set<Account>();

        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

        public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

        public DbSet<WebhookLog> WebhookLogs => Set<WebhookLog>();

        public DbSet<IdempotentRequest> IdempotentRequests => Set<IdempotentRequest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
