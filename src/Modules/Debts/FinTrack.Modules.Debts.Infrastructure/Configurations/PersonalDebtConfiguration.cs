using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.Debts.Infrastructure.Configurations;

internal sealed class PersonalDebtConfiguration : IEntityTypeConfiguration<PersonalDebt>
{
    private const string EmptyGuidFilter = "[LinkedCashInflowId] <> '00000000-0000-0000-0000-000000000000'";

    public void Configure(EntityTypeBuilder<PersonalDebt> builder)
    {
        builder.ToTable("PersonalDebts");
        builder.HasKey(debt => debt.Id);

        builder.Property(debt => debt.UserId).IsRequired();
        builder.Property(debt => debt.CounterpartyName)
            .IsRequired()
            .HasMaxLength(PersonalDebt.CounterpartyNameMaxLength);
        builder.Property(debt => debt.Principal)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();
        builder.Property(debt => debt.Currency).IsRequired().HasMaxLength(3);
        builder.Property(debt => debt.BorrowedOn).IsRequired();
        builder.Property(debt => debt.DueDate);
        builder.Property(debt => debt.Description).HasMaxLength(PersonalDebt.DescriptionMaxLength);
        builder.Property(debt => debt.Status).IsRequired();
        builder.Property(debt => debt.RecordedAtUtc).IsRequired();
        builder.Property(debt => debt.CancelledAtUtc);
        builder.Property(debt => debt.CancellationReason)
            .HasMaxLength(PersonalDebt.CancellationReasonMaxLength);
        builder.Property(debt => debt.LinkedCashInflowId).IsRequired();
        builder.Property(debt => debt.RowVersion).IsRowVersion();

        builder.Ignore(debt => debt.RemainingAmount);

        builder.HasIndex(debt => new { debt.UserId, debt.Status });
        builder.HasIndex(debt => new { debt.UserId, debt.BorrowedOn });
        builder.HasIndex(debt => debt.LinkedCashInflowId)
            .IsUnique()
            .HasFilter(EmptyGuidFilter);

        builder.HasMany(debt => debt.Repayments)
            .WithOne()
            .HasForeignKey(repayment => repayment.PersonalDebtId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
