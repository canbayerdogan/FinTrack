using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.Debts.Infrastructure.Configurations;

internal sealed class PersonalDebtRepaymentConfiguration : IEntityTypeConfiguration<PersonalDebtRepayment>
{
    public void Configure(EntityTypeBuilder<PersonalDebtRepayment> builder)
    {
        builder.ToTable("PersonalDebtRepayments");
        builder.HasKey(repayment => repayment.Id);

        builder.Property(repayment => repayment.UserId).IsRequired();
        builder.Property(repayment => repayment.PersonalDebtId).IsRequired();
        builder.Property(repayment => repayment.Amount)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();
        builder.Property(repayment => repayment.Currency).IsRequired().HasMaxLength(3);
        builder.Property(repayment => repayment.PaidOn).IsRequired();
        builder.Property(repayment => repayment.Note)
            .HasMaxLength(PersonalDebtRepayment.NoteMaxLength);
        builder.Property(repayment => repayment.Status).IsRequired();
        builder.Property(repayment => repayment.RecordedAtUtc).IsRequired();
        builder.Property(repayment => repayment.CancelledAtUtc);
        builder.Property(repayment => repayment.CancellationReason)
            .HasMaxLength(PersonalDebtRepayment.CancellationReasonMaxLength);
        builder.Property(repayment => repayment.RowVersion).IsRowVersion();

        builder.HasIndex(repayment => new { repayment.UserId, repayment.PaidOn });
        builder.HasIndex(repayment => new { repayment.PersonalDebtId, repayment.Status });
    }
}
