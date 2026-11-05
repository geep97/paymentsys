namespace PaymentSys.Options
{
    public class LedgerOptions
    {
        public const string SectionName = "Ledger";

        public string SettlementAccountNumber { get; set; } = "1000";

        public string RevenueAccountNumber { get; set; } = "4000";
    }
}
