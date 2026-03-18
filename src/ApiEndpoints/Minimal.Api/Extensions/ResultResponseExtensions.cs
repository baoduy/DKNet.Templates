namespace Minimal.Api.Extensions;

internal static class ResultResponseExtensions
{
    public static IResult Response<TObject>(this IResult<TObject> result, bool isCreated = false)
    {
        return !result.IsSuccess
            ? TypedResults.Problem(result.ToProblemDetails()!)
            : isCreated
                ? TypedResults.Created(new Uri("/"), result.Value)
                : result.ValueOrDefault is null
                    ? TypedResults.Ok()
                    : TypedResults.Json(result.Value);
    }

    public static IResult Response(this IResultBase result, bool isCreated = false)
    {
        return result.IsSuccess
            ? isCreated ? TypedResults.Created() : TypedResults.Ok()
            : TypedResults.Problem(result.ToProblemDetails()!);
    }
}