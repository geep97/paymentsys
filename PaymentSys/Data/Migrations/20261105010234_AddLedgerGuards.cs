using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSys.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_JournalEntries_Immutable]
                ON [dbo].[JournalEntries]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                    BEGIN
                        THROW 51002, 'Journal entries are immutable. Post a reversing entry instead.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted AS i
                        INNER JOIN deleted AS d ON d.Id = i.Id
                        WHERE d.PostedAt IS NOT NULL
                           OR i.PostedAt IS NULL
                           OR i.Reference <> d.Reference
                           OR i.Narration <> d.Narration
                           OR i.CurrencyCode <> d.CurrencyCode
                           OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.PaymentTransactionId, -1) <> ISNULL(d.PaymentTransactionId, -1)
                           OR ISNULL(i.ReversalOfId, -1) <> ISNULL(d.ReversalOfId, -1)
                    )
                    BEGIN
                        THROW 51002, 'Journal entries are immutable. Post a reversing entry instead.', 1;
                    END;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_JournalEntryLines_Immutable]
                ON [dbo].[JournalEntryLines]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    THROW 51003, 'Journal entry lines are immutable. Post a reversing entry instead.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_JournalEntries_EnforceBalance]
                ON [dbo].[JournalEntries]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted AS i
                        INNER JOIN deleted AS d ON d.Id = i.Id
                        WHERE d.PostedAt IS NULL
                          AND i.PostedAt IS NOT NULL
                          AND (
                              (SELECT COUNT(*) FROM [dbo].[JournalEntryLines] AS l WHERE l.JournalEntryId = i.Id) < 2
                              OR EXISTS (
                                  SELECT 1
                                  FROM [dbo].[JournalEntryLines] AS l
                                  WHERE l.JournalEntryId = i.Id
                                  GROUP BY l.JournalEntryId
                                  HAVING SUM(l.DebitPesewas) <> SUM(l.CreditPesewas)
                              )
                          )
                    )
                    BEGIN
                        THROW 51001, 'Journal entry is unbalanced: total debits must equal total credits.', 1;
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_JournalEntries_EnforceBalance];");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_JournalEntryLines_Immutable];");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_JournalEntries_Immutable];");
        }
    }
}
