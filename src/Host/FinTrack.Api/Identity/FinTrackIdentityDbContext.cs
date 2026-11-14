using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Api.Identity;

/// <summary>
/// Identity tablolarının sahibi. Şema: <c>identity</c>. Finansal modül tabloları burada yoktur.
/// </summary>
public sealed class FinTrackIdentityDbContext(DbContextOptions<FinTrackIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("identity");

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.PreferredTimeZone)
                .HasMaxLength(100)
                .IsRequired();

            user.Property(u => u.CreatedAtUtc)
                .IsRequired();

            user.Property(u => u.RowVersion)
                .IsRowVersion();
        });

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("RefreshTokens");
            token.HasKey(t => t.Id);

            token.Property(t => t.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            token.Property(t => t.ReplacedByTokenHash)
                .HasMaxLength(64);

            token.Property(t => t.RowVersion)
                .IsRowVersion();

            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => t.UserId);
            token.HasIndex(t => t.ExpiresAtUtc);

            token.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
