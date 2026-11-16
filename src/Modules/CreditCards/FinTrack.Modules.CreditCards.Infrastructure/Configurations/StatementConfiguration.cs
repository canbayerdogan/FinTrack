using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.CreditCards.Infrastructure.Configurations;

internal sealed class StatementConfiguration : IEntityTypeConfiguration<Statement>
{
    public void Configure(EntityTypeBuilder<Statement> builder)
    {
        builder.ToTable("Statements");
        builder.HasKey(statement => statement.Id);

        builder.Property(statement => statement.UserId).IsRequired();
        builder.Property(statement => statement.CardId).IsRequired();

        builder.Property(statement => statement.PeriodStart).IsRequired();
        builder.Property(statement => statement.PeriodEnd).IsRequired();
        builder.Property(statement => statement.DueDate).IsRequired();

        builder.Property(statement => statement.StatementAmount)
            .HasPrecision(Money.StorageScale + 15, Money.StorageScale)
            .IsRequired();

        builder.Property(statement => statement.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(statement => statement.RecordedAtUtc).IsRequired();
        builder.Property(statement => statement.CancelledAtUtc);
        builder.Property(statement => statement.CancellationReason)
            .HasMaxLength(Statement.CancellationReasonMaxLength);
        builder.Property(statement => statement.RowVersion).IsRowVersion();

        builder.HasIndex(statement => new { statement.UserId, statement.CardId, statement.PeriodEnd });
    }
}
