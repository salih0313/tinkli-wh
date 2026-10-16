using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Wros.Api.Shared.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IProblemDetailsService problems)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Exception messages can contain connection details or sensitive input.
        if (exception is not ApiProblemException)
            logger.LogError("Unhandled request failure of type {ExceptionType}", exception.GetType().Name);
        var status = exception switch
        {
            ApiProblemException expected => expected.Status,
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => 409,
            Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 or 1205 } } => 409,
            Microsoft.Data.SqlClient.SqlException { Number: 1205 } => 409,
            _ => 500
        };
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = exception is ApiProblemException problem ? problem.Message : status == 409 ? "This record changed. Refresh and retry." : "An unexpected error occurred.",
                Detail = "Contact support with the correlation ID."
            }
        });
    }
}
