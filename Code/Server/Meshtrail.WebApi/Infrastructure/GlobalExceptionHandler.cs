using FluentValidation;
using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Infrastructure;

/// <summary>
/// Turns known exceptions into ProblemDetails responses, so controllers never need try/catch.
/// Unknown exceptions fall through to the default 500 handler.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => ToValidationProblem(validation),
            KeyNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = exception.Message,
            },
            ConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Changed by someone else",
                Detail = exception.Message,
            },
            DomainException => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Business rule violated",
                Detail = exception.Message,
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        LogHandled(exception.GetType().Name, problem.Status ?? 0);
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ValidationProblemDetails ToValidationProblem(ValidationException exception)
    {
        // Group failures per property so the client can show each message next to its field.
        var errors = exception.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Mapped {ExceptionType} to HTTP {StatusCode}")]
    private partial void LogHandled(string exceptionType, int statusCode);
}
