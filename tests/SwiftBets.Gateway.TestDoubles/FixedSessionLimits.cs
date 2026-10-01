using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.TestDoubles;

/// <summary>Session limits set by a test instead of read from compliance's topic.</summary>
public sealed class FixedSessionLimits(SessionLimits limits) : ISessionLimits
{
    public static FixedSessionLimits None { get; } = new(SessionLimits.None);

    public SessionLimits For(string userId) => limits;
}
