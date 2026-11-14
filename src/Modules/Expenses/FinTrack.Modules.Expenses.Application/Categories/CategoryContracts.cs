using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Application.Categories;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? IconKey,
    bool IsArchived,
    string RowVersion);

public sealed record GetCategoriesQuery(bool IncludeArchived);

public sealed record CreateCategoryCommand(string? Name, string? IconKey);

public sealed record UpdateCategoryCommand(Guid Id, string? Name, string? IconKey, string? RowVersion);

public sealed record ArchiveCategoryCommand(Guid Id, string? RowVersion);

/// <summary>Handler'ların ürettiği kategori görünümü; altyapıya bağımlı değildir.</summary>
public static class CategoryMapper
{
    public static CategoryDto ToDto(Category category) => new(
        category.Id,
        category.Name,
        category.IconKey,
        category.IsArchived,
        RowVersionCodec.ToBase64(category.RowVersion));
}
