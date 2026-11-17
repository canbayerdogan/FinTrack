using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.Debts.Infrastructure.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    private const string EmptyGuidFilter = "[LinkedCashInflowId] <> '00000000-0000-0000-0000-000000000000'";

    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans");
        builder.HasKey(loan => loan.Id);

        builder.Property(loan => loan.UserId).IsRequired();
        builder.Property(loan => loan.Principal)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();
        builder.Property(loan => loan.MonthlyPayment)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();
        builder.Property(loan => loan.InstallmentCount).IsRequired();
        builder.Property(loan => loan.FirstDueDate).IsRequired();
        builder.Property(loan => loan.BankName).HasMaxLength(Loan.BankNameMaxLength);
        builder.Property(loan => loan.Description).HasMaxLength(Loan.DescriptionMaxLength);
        builder.Property(loan => loan.Currency).IsRequired().HasMaxLength(3);
        builder.Property(loan => loan.Status).IsRequired();
        builder.Property(loan => loan.RecordedAtUtc).IsRequired();
        builder.Property(loan => loan.CancelledAtUtc);
        builder.Property(loan => loan.CancellationReason)
            .HasMaxLength(Loan.CancellationReasonMaxLength);
        builder.Property(loan => loan.LinkedCashInflowId).IsRequired();
        builder.Property(loan => loan.RowVersion).IsRowVersion();

        builder.Ignore(loan => loan.TotalRepayment);
        builder.Ignore(loan => loan.RemainingAmount);

        builder.HasIndex(loan => new { loan.UserId, loan.Status });
        builder.HasIndex(loan => new { loan.UserId, loan.FirstDueDate });
        builder.HasIndex(loan => loan.LinkedCashInflowId)
            .IsUnique()
            .HasFilter(EmptyGuidFilter);

        builder.HasMany(loan => loan.Installments)
            .WithOne()
            .HasForeignKey(installment => installment.LoanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
