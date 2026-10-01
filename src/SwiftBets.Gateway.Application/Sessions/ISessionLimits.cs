namespace SwiftBets.Gateway.Application.Sessions;

/// <summary>A customer's session-time limit and reality-check interval, from compliance; nulls mean none set.</summary>
public sealed record SessionLimits(int? SessionLimitMinutes, int? RealityCheckMinutes)
{
    public static SessionLimits None { get; } = new(null, null);

    /// <summary>True once a browser session that began at <paramref name="startedAt"/> has used up the customer's limit.</summary>
    public bool Exhausted(DateTimeOffset startedAt, DateTimeOffset now) =>
        SessionLimitMinutes is { } minutes && now - startedAt >= TimeSpan.FromMinutes(minutes);
}

public interface ISessionLimits
{
    SessionLimits For(string userId);
}
