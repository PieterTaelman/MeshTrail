using System.Net.Mail;
using Meshtrail.Core.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Email;

/// <summary>Settings from Email (and the MailPit connection string Aspire injects locally).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string From { get; set; } = "Meshtrail <no-reply@meshtrail.local>";

    /// <summary>SMTP server; empty = no mail server (mails are written to the log instead).</summary>
    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 25;

    public bool SmtpEnableSsl { get; set; }

    /// <summary>Reads "Endpoint=smtp://host:port" (Aspire's MailPit connection string) into SmtpHost/SmtpPort.</summary>
    public void UseConnectionString(string? connectionString)
    {
        var endpoint = connectionString?.Split(';').Select(part => part.Trim())
            .FirstOrDefault(part => part.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))?["Endpoint=".Length..];
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            SmtpHost = uri.Host;
            SmtpPort = uri.Port;
        }
    }
}

/// <summary>Sends mail through an SMTP server (MailPit locally).</summary>
public sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.SmtpEnableSsl };
        using var mail = new MailMessage(new MailAddress(settings.From).Address, message.To, message.Subject, message.Body);
        mail.From = new MailAddress(settings.From);
        await client.SendMailAsync(mail, cancellationToken);
        LogSent(message.Subject, message.To);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Mail \"{Subject}\" sent to {To}")]
    private partial void LogSent(string subject, string to);
}

/// <summary>
/// No mail server configured (e.g. running without Docker): the mail, including its link, is written to the log so
/// the flows can still be tried. Never use this in production.
/// </summary>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        LogMail(message.To, message.Subject, message.Body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No mail server configured. Mail to {To}: {Subject}\n{Body}")]
    private partial void LogMail(string to, string subject, string body);
}
