using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.Debts.Infrastructure.Configurations;

internal sealed class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> builder)
    {
        builder.ToTable("Installments");
        builder.HasKey(installment => installment.Id);

        builder.Property(installment => installment.UserId).IsRequired();
        builder.Property(installment => installment.LoanId).IsRequired();
        builder.Property(installment => installment.Number).IsRequired();
        builder.Property(installment => installment.DueDate).IsRequired();
        builder.Property(installment => installment.Amount)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();
        builder.Property(installment => installment.Status).IsRequired();

        builder.Ignore(installment => installment.ActivePayment);

        builder.HasIndex(installment => new { installment.LoanId, installment.Number }).IsUnique();
        builder.HasIndex(installment => new { installment.UserId, installment.Status });
        builder.HasIndex(installment => installment.DueDate);

        builder.HasMany(installment => installment.Payments)
            .WithOne()
            .HasForeignKey(payment => payment.InstallmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
