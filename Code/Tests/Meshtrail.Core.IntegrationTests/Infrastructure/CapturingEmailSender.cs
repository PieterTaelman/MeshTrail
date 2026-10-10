using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Meshtrail.Core.Application.Abstractions;

namespace Meshtrail.Core.IntegrationTests.Infrastructure;

/// <summary>Keeps the mails the API sends, so tests can follow the links in them.</summary>
internal sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> To(string address) => [.. _sent.Where(message => message.To == address)];

    /// <summary>The user id and token of the newest link to <paramref name="path"/> (e.g. "/account/confirm") mailed to this address.</summary>
    public (Guid UserId, string Token) LinkFor(string address, string path)
    {
        var match = To(address)
            .Select(message => LinkPattern().Match(message.Body))
            .LastOrDefault(found => found.Success && found.Groups["path"].Value == path)
            ?? throw new InvalidOperationException($"No {path} link was mailed to {address}.");
        return (Guid.Parse(match.Groups["user"].Value), Uri.UnescapeDataString(match.Groups["token"].Value));
    }

    [GeneratedRegex(@"https://client\.test(?<path>/account/[a-z-]+)\?user=(?<user>[0-9a-f-]+)&token=(?<token>[^\s]+)")]
    private static partial Regex LinkPattern();
}
