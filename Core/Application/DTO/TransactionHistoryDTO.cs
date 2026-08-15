namespace Application.DTO
{
    public class TransactionHistoryDTO
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        
        public decimal PrincipalPaid { get; set; }
        public decimal InterestPaid { get; set; }
        public decimal PenaltyPaid { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
