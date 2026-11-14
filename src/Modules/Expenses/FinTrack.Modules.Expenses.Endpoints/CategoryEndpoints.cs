using FinTrack.Modules.Expenses.Application.Categories;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.Expenses.Endpoints;

internal static class CategoryEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("", async (
                bool? includeArchived,
                IQueryHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new GetCategoriesQuery(includeArchived ?? false),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("ListCategories")
            .WithSummary("Kullanıcının kategorilerini listeler.");

        group.MapPost("", async (
                CreateCategoryRequest request,
                ICommandHandler<CreateCategoryCommand, Result<CategoryDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CreateCategoryCommand(request.Name, request.IconKey),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"{ExpensesModule.CategoriesRoutePrefix}/{result.Value.Id}", result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("CreateCategory")
            .WithSummary("Kategori oluşturur.");

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateCategoryRequest request,
                ICommandHandler<UpdateCategoryCommand, Result<CategoryDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new UpdateCategoryCommand(id, request.Name, request.IconKey, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("UpdateCategory")
            .WithSummary("Kategori adını/ikonunu günceller.");

        group.MapPost("/{id:guid}/archive", async (
                Guid id,
                ArchiveRequest request,
                ICommandHandler<ArchiveCategoryCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new ArchiveCategoryCommand(id, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("ArchiveCategory")
            .WithSummary("Kategoriyi arşivler; kayıt silinmez.");
    }
}
