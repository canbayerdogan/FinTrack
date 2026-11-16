using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.CreditCards.Infrastructure.Configurations;

internal sealed class CardPaymentConfiguration : IEntityTypeConfiguration<CardPayment>
{
    public void Configure(EntityTypeBuilder<CardPayment> builder)
    {
        builder.ToTable("CardPayments");
        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.UserId).IsRequired();
        builder.Property(payment => payment.CardId).IsRequired();

        builder.Property(payment => payment.Amount)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();

        builder.Property(payment => payment.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(payment => payment.PaidOn).IsRequired();
        builder.Property(payment => payment.Note).HasMaxLength(CardPayment.NoteMaxLength);
        builder.Property(payment => payment.RecordedAtUtc).IsRequired();
        builder.Property(payment => payment.CancelledAtUtc);
        builder.Property(payment => payment.CancellationReason)
            .HasMaxLength(CardPayment.CancellationReasonMaxLength);
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        builder.HasIndex(payment => new { payment.UserId, payment.CardId, payment.PaidOn });
    }
}
