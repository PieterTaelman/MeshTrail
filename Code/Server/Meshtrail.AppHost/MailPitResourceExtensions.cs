using System.Net.Mail;

namespace Meshtrail.AppHost;

internal static class MailPitResourceExtensions
{
    /// <summary>Adds a dashboard button that sends one test mail, to check the mail catcher works.</summary>
    public static IResourceBuilder<MailPitContainerResource> WithSendTestMailCommand(this IResourceBuilder<MailPitContainerResource> mailpit)
    {
        mailpit.WithCommand(
            name: "send-test-mail",
            displayName: "Send test mail",
            executeCommand: async context =>
            {
                var smtp = mailpit.Resource.PrimaryEndpoint;
                using var client = new SmtpClient(smtp.Host, smtp.Port);
                using var message = new MailMessage(
                    "meshtrail@localhost",
                    "developer@localhost",
                    "Meshtrail test mail",
                    $"Sent from the Aspire dashboard at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}.");

                await client.SendMailAsync(message, context.CancellationToken);
                return CommandResults.Success();
            },
            commandOptions: new CommandOptions
            {
                Description = "Sends a test mail through MailPit's SMTP port. Open the MailPit UI to see it.",
                IconName = "Mail",
            });

        return mailpit;
    }
}
