using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.Expenses.Infrastructure.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");
        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.UserId).IsRequired();

        builder.Property(expense => expense.Amount)
            .IsRequired()
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale);

        builder.Property(expense => expense.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(expense => expense.OccurredOn)
            .IsRequired();

        builder.Property(expense => expense.CategoryId).IsRequired();

        builder.Property(expense => expense.Description)
            .HasMaxLength(Expense.DescriptionMaxLength);

        builder.Property(expense => expense.PaymentMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(expense => expense.RecordedAtUtc).IsRequired();

        builder.Property(expense => expense.CancellationReason)
            .HasMaxLength(Expense.CancellationReasonMaxLength);

        builder.Property(expense => expense.RowVersion).IsRowVersion();

        builder.HasMany(expense => expense.Refunds)
            .WithOne()
            .HasForeignKey(refund => refund.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(expense => expense.Refunds)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(expense => new { expense.UserId, expense.OccurredOn });
        builder.HasIndex(expense => new { expense.CategoryId, expense.UserId });
        builder.HasIndex(expense => new { expense.UserId, expense.CardId });

        // Kategori aynı modüldedir; modüller arası FK yasaktır (yalnızca CardId).
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
