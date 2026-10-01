using Microsoft.Extensions.Logging;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Identity;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.Infrastructure.Messaging;

/// <summary>
/// Ends a customer's live browser sessions when identity revokes all of them, so a suspended or self-excluded account
/// is signed out everywhere at once instead of when its access token next expires.
/// </summary>
public sealed partial class SessionRevokedConsumer(BrowserSessions sessions, ILogger<SessionRevokedConsumer> logger) : IEventHandler<SessionRevokedV1>
{
    public async Task HandleAsync(ConsumedEvent<SessionRevokedV1> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = message.Envelope.Payload;
        if (payload.SessionId is not null)
        {
            // A single device's sign-out already went through the gateway, which removed that session itself.
            return;
        }

        var dropped = await sessions.DropAllAsync(payload.UserId.ToString());
        LogDropped(dropped, payload.UserId, payload.Reason);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ended {Count} browser sessions of {UserId}: {Reason}")]
    private partial void LogDropped(int count, Guid userId, string reason);
}
