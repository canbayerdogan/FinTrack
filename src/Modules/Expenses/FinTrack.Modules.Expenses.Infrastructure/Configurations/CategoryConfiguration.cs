using FinTrack.Modules.Expenses.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Modules.Expenses.Infrastructure.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);

        builder.Property(category => category.UserId).IsRequired();

        builder.Property(category => category.Name)
            .IsRequired()
            .HasMaxLength(Category.NameMaxLength);

        builder.Property(category => category.NormalizedName)
            .IsRequired()
            .HasMaxLength(Category.NameMaxLength);

        builder.Property(category => category.IconKey)
            .HasMaxLength(Category.IconKeyMaxLength);

        builder.Property(category => category.CreatedAtUtc).IsRequired();

        builder.Property(category => category.RowVersion).IsRowVersion();

        // Arşivli kategoriler de benzersizliğe dahildir (docs/05 §3.6).
        builder.HasIndex(category => new { category.UserId, category.NormalizedName })
            .IsUnique()
            .HasDatabaseName("UX_Categories_UserId_NormalizedName");
    }
}
