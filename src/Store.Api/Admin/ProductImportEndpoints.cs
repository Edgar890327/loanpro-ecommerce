using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Store.Application;
using Store.Infrastructure.Importing;

namespace Store.Api.Admin;

public static class ProductImportEndpoints
{
    private const long MultipartOverheadBytes = 256 * 1024;

    public static RouteGroupBuilder MapProductImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/products")
            .RequireAuthorization("Admin");
        group.MapPost("/import", ImportAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ProductCsvParser.MaxFileSizeBytes + MultipartOverheadBytes))
            .WithMetadata(new RequestFormLimitsAttribute
            {
                MultipartBodyLengthLimit = ProductCsvParser.MaxFileSizeBytes + MultipartOverheadBytes
            });
        return group;
    }

    private static async Task<IResult> ImportAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        ProductImportService importService,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Multipart form data is required.");
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The upload exceeds the supported size.");
        }

        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["A non-empty CSV file is required."]
            });
        }

        if (file.Length > ProductCsvParser.MaxFileSizeBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: $"CSV files cannot exceed {ProductCsvParser.MaxFileSizeBytes / 1024 / 1024} MB.");
        }

        if (!bool.TryParse(form["dryRun"], out var dryRun))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["dryRun"] = ["The dryRun multipart field must be true or false."]
            });
        }

        var confirmReimport = false;
        if (form.TryGetValue("confirmReimport", out var confirmValue) &&
            !bool.TryParse(confirmValue, out confirmReimport))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["confirmReimport"] = ["The confirmReimport multipart field must be true or false."]
            });
        }

        await using var upload = new MemoryStream((int)file.Length);
        await file.CopyToAsync(upload, cancellationToken);
        try
        {
            var result = await importService.ImportAsync(
                upload.ToArray(),
                dryRun,
                confirmReimport,
                user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.Identity?.Name ?? "admin",
                cancellationToken);
            if (result.ReimportConfirmationRequired)
            {
                return Results.Conflict(new
                {
                    title = "This file was imported before.",
                    detail = "Set confirmReimport=true to add its stock again.",
                    report = result.Report
                });
            }

            return Results.Ok(result.Report);
        }
        catch (ProductCsvLimitException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: exception.Message);
        }
        catch (ProductCsvFormatException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: exception.Message);
        }
    }
}
