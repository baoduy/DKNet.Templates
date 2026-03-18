namespace Minimal.AppServices.Share;

public record RequestBase
{
    #region Properties

    [JsonIgnore] public string? ByUser { get; set; }

    #endregion
}