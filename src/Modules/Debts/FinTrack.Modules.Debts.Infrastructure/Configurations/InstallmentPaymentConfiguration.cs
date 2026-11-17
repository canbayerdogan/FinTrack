using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.Debts.Infrastructure.Configurations;

internal sealed class InstallmentPaymentConfiguration : IEntityTypeConfiguration<InstallmentPayment>
{
    public void Configure(EntityTypeBuilder<InstallmentPayment> builder)
    {
        builder.ToTable("InstallmentPayments");
        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.UserId).IsRequired();
        builder.Property(payment => payment.InstallmentId).IsRequired();
        builder.Property(payment => payment.Amount)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();
        builder.Property(payment => payment.PaidOn).IsRequired();
        builder.Property(payment => payment.Status).IsRequired();
        builder.Property(payment => payment.RecordedAtUtc).IsRequired();
        builder.Property(payment => payment.CancelledAtUtc);
        builder.Property(payment => payment.CancellationReason)
            .HasMaxLength(InstallmentPayment.CancellationReasonMaxLength);
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        builder.HasIndex(payment => new { payment.UserId, payment.PaidOn });
        builder.HasIndex(payment => payment.InstallmentId)
            .IsUnique()
            .HasFilter("[Status] = 0");
    }
}
