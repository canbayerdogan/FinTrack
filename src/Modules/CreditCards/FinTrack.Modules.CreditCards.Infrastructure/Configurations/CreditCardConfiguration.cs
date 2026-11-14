using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.CreditCards.Infrastructure.Configurations;

internal sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("Cards");
        builder.HasKey(card => card.Id);

        builder.Property(card => card.UserId).IsRequired();

        builder.Property(card => card.BankName)
            .IsRequired()
            .HasMaxLength(CreditCard.BankNameMaxLength);

        builder.Property(card => card.DisplayName)
            .IsRequired()
            .HasMaxLength(CreditCard.DisplayNameMaxLength);

        builder.Property(card => card.LogoKey)
            .HasMaxLength(CreditCard.LogoKeyMaxLength);

        builder.Property(card => card.CreditLimit)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale);

        builder.Property(card => card.StatementDay);
        builder.Property(card => card.PaymentDueDay);
        builder.Property(card => card.IsArchived).IsRequired();
        builder.Property(card => card.ArchivedAtUtc);
        builder.Property(card => card.CreatedAtUtc).IsRequired();
        builder.Property(card => card.RowVersion).IsRowVersion();

        builder.HasIndex(card => new { card.UserId, card.IsArchived });
    }
}
