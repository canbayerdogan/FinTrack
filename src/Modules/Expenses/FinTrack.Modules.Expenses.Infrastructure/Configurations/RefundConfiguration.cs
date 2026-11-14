using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.Expenses.Infrastructure.Configurations;

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");
        builder.HasKey(refund => refund.Id);

        builder.Property(refund => refund.ExpenseId).IsRequired();
        builder.Property(refund => refund.UserId).IsRequired();

        builder.Property(refund => refund.Amount)
            .IsRequired()
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale);

        builder.Property(refund => refund.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(refund => refund.RefundedOn)
            .IsRequired();

        builder.Property(refund => refund.Description)
            .HasMaxLength(Refund.DescriptionMaxLength);

        builder.Property(refund => refund.RecordedAtUtc).IsRequired();

        builder.Property(refund => refund.CancellationReason)
            .HasMaxLength(Expense.CancellationReasonMaxLength);

        builder.Property(refund => refund.RowVersion).IsRowVersion();

        builder.HasIndex(refund => refund.ExpenseId);
        builder.HasIndex(refund => new { refund.UserId, refund.RefundedOn });
    }
}
