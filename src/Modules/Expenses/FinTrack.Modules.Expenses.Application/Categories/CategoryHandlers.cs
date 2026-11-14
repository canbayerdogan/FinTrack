using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Application.Common;
using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Application.Categories;

public sealed class GetCategoriesHandler(
    ICurrentUser currentUser,
    ICategoryQueries queries)
    : IQueryHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> HandleAsync(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var categories = await queries.ListAsync(userId, query.IncludeArchived, cancellationToken);
        return Result.Success(categories);
    }
}

public sealed class CreateCategoryHandler(
    ICurrentUser currentUser,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> HandleAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var createResult = Category.Create(userId, command.Name, command.IconKey, clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<CategoryDto>(createResult.Error);
        }

        var category = createResult.Value;
        if (await categories.NameExistsAsync(userId, category.NormalizedName, null, cancellationToken))
        {
            return Result.Failure<CategoryDto>(ApplicationErrors.CategoryNameTaken());
        }

        await categories.AddAsync(category, cancellationToken);
        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);

        return outcome switch
        {
            SaveChangesOutcome.DuplicateKey => Result.Failure<CategoryDto>(ApplicationErrors.CategoryNameTaken()),
            SaveChangesOutcome.ConcurrencyConflict => Result.Failure<CategoryDto>(ApplicationErrors.Concurrency()),
            _ => Result.Success(CategoryMapper.ToDto(category)),
        };
    }
}

public sealed class UpdateCategoryHandler(
    ICurrentUser currentUser,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> HandleAsync(
        UpdateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var category = await categories.FindAsync(userId, command.Id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<CategoryDto>(ApplicationErrors.CategoryNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure<CategoryDto>(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(category.RowVersion, requestedRowVersion))
        {
            return Result.Failure<CategoryDto>(ApplicationErrors.Concurrency());
        }

        var renameResult = category.Rename(command.Name, command.IconKey);
        if (renameResult.IsFailure)
        {
            return Result.Failure<CategoryDto>(renameResult.Error);
        }

        if (await categories.NameExistsAsync(userId, category.NormalizedName, category.Id, cancellationToken))
        {
            return Result.Failure<CategoryDto>(ApplicationErrors.CategoryNameTaken());
        }

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);

        return outcome switch
        {
            SaveChangesOutcome.DuplicateKey => Result.Failure<CategoryDto>(ApplicationErrors.CategoryNameTaken()),
            SaveChangesOutcome.ConcurrencyConflict => Result.Failure<CategoryDto>(ApplicationErrors.Concurrency()),
            _ => Result.Success(CategoryMapper.ToDto(category)),
        };
    }
}

public sealed class ArchiveCategoryHandler(
    ICurrentUser currentUser,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<ArchiveCategoryCommand, Result>
{
    public async Task<Result> HandleAsync(ArchiveCategoryCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var category = await categories.FindAsync(userId, command.Id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(ApplicationErrors.CategoryNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(category.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        // Arşivleme idempotenttir; zaten arşivliyse aynı sürümle 204 döner.
        category.Archive(clock.UtcNow);

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        return outcome == SaveChangesOutcome.ConcurrencyConflict
            ? Result.Failure(ApplicationErrors.Concurrency())
            : Result.Success();
    }
}
