using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public Guid? TransferToAccountId { get; set; }
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Account? Account { get; set; }
    public Account? TransferToAccount { get; set; }
    public Category? Category { get; set; }
}
