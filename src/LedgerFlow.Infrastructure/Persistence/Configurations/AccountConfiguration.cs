using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LedgerFlow.Domain.Entities;

namespace LedgerFlow.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.InitialBalance).HasPrecision(18, 2);
        builder.Property(x => x.CurrentBalance).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
