using System.Diagnostics;
using Mediator;
using Meshtrail.Core.Application.Telemetry;
using Microsoft.Extensions.Logging;

namespace Meshtrail.Core.Application.Behaviors;

/// <summary>
/// Wraps every message in a trace span and logs how long it took, so slow or failing use cases are easy to spot.
/// </summary>
public sealed partial class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageName = typeof(TMessage).Name;
        using var activity = ApplicationTelemetry.ActivitySource.StartActivity(messageName);
        activity?.SetTag("meshtrail.message", messageName);

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var response = await next(message, cancellationToken);
            LogHandled(messageName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            // Log and rethrow: the global exception handler decides which HTTP status the caller gets.
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            LogFailed(messageName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, exception.GetType().Name);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {MessageName} in {ElapsedMs:0.0} ms")]
    private partial void LogHandled(string messageName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{MessageName} failed after {ElapsedMs:0.0} ms with {ExceptionType}")]
    private partial void LogFailed(string messageName, double elapsedMs, string exceptionType);
}
