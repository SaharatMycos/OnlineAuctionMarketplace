using Marketplace.SharedKernel;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api;

/// <summary>Maps rule violations to RFC 7807 problem details with a stable <c>code</c> extension.</summary>
internal sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, detail) = exception switch
        {
            ProblemException p => (p.Status, p.Code, p.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "unauthorized", "Sign in to do this."),
            BadHttpRequestException b => (b.StatusCode, "bad_request", b.Message),
            _ => (0, "", ""),
        };
        if (status == 0)
            return false;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = code, Detail = detail, Extensions = { ["code"] = code } },
        });
    }
}
