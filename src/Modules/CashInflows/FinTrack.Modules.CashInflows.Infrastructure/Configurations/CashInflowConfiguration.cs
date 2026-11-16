using FinTrack.Modules.CashInflows.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.CashInflows.Infrastructure.Configurations;

internal sealed class CashInflowConfiguration : IEntityTypeConfiguration<CashInflow>
{
    public void Configure(EntityTypeBuilder<CashInflow> builder)
    {
        builder.ToTable("CashInflows");
        builder.HasKey(inflow => inflow.Id);

        builder.Property(inflow => inflow.UserId).IsRequired();

        builder.Property(inflow => inflow.Amount)
            .IsRequired()
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale);

        builder.Property(inflow => inflow.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(inflow => inflow.OccurredOn).IsRequired();
        builder.Property(inflow => inflow.Source).IsRequired();
        builder.Property(inflow => inflow.Description).HasMaxLength(CashInflow.DescriptionMaxLength);
        builder.Property(inflow => inflow.DebtLinkId);
        builder.Property(inflow => inflow.RecordedAtUtc).IsRequired();
        builder.Property(inflow => inflow.CancelledAtUtc);
        builder.Property(inflow => inflow.CancellationReason)
            .HasMaxLength(CashInflow.CancellationReasonMaxLength);
        builder.Property(inflow => inflow.RowVersion).IsRowVersion();

        builder.HasIndex(inflow => new { inflow.UserId, inflow.OccurredOn });
    }
}
