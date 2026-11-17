using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.CreditCards.Infrastructure.Configurations;

internal sealed class CashAdvanceConfiguration : IEntityTypeConfiguration<CashAdvance>
{
    public void Configure(EntityTypeBuilder<CashAdvance> builder)
    {
        builder.ToTable("CashAdvances");
        builder.HasKey(advance => advance.Id);

        builder.Property(advance => advance.UserId).IsRequired();
        builder.Property(advance => advance.CardId).IsRequired();

        builder.Property(advance => advance.Amount)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();

        builder.Property(advance => advance.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(advance => advance.TakenOn).IsRequired();
        builder.Property(advance => advance.Description).HasMaxLength(CashAdvance.DescriptionMaxLength);
        builder.Property(advance => advance.LinkedCashInflowId);
        builder.Property(advance => advance.RecordedAtUtc).IsRequired();
        builder.Property(advance => advance.CancelledAtUtc);
        builder.Property(advance => advance.CancellationReason)
            .HasMaxLength(CashAdvance.CancellationReasonMaxLength);
        builder.Property(advance => advance.RowVersion).IsRowVersion();

        builder.HasIndex(advance => new { advance.UserId, advance.CardId, advance.TakenOn });
        builder.HasIndex(advance => advance.LinkedCashInflowId).IsUnique().HasFilter("[LinkedCashInflowId] IS NOT NULL");
    }
}
