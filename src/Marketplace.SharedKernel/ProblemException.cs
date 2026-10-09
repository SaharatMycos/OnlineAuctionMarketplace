namespace Marketplace.SharedKernel;

/// <summary>
/// A rule violation the caller should see (mapped to RFC 7807 problem details by the api).
/// Use the static helpers so status codes stay consistent across modules.
/// </summary>
public sealed class ProblemException(int status, string code, string detail) : Exception(detail)
{
    public int Status { get; } = status;
    /// <summary>Stable machine-readable code, e.g. <c>bid_too_low</c>.</summary>
    public string Code { get; } = code;

    public static ProblemException BadRequest(string code, string detail) => new(400, code, detail);
    public static ProblemException Forbidden(string code, string detail) => new(403, code, detail);
    public static ProblemException NotFound(string what) => new(404, "not_found", $"{what} was not found.");
    public static ProblemException Conflict(string code, string detail) => new(409, code, detail);
}
