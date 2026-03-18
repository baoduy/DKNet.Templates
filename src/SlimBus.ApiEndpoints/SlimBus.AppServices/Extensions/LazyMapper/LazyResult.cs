namespace SlimBus.AppServices.Extensions.LazyMapper;

internal class LazyResult<TResult>(object? originalValue, IMapper mapper)
    : LazyMap<TResult>(originalValue, mapper), IResult<TResult>
{
    #region Properties

    public bool IsFailed => Reasons.OfType<IError>().Any();

    public bool IsSuccess => !IsFailed;

    public IReadOnlyList<IError> Errors => [.. Reasons.OfType<IError>()];

    public IReadOnlyList<ISuccess> Successes => [.. Reasons.OfType<ISuccess>()];

    public List<IReason> Reasons { get; init; } = [];

    #endregion
}