using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Type).HasConversion<int>();
        builder.HasOne(x => x.Account).WithMany(a => a.Transactions).HasForeignKey(x => x.AccountId);
        builder.HasOne(x => x.TransferToAccount).WithMany().HasForeignKey(x => x.TransferToAccountId);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
    }
}
