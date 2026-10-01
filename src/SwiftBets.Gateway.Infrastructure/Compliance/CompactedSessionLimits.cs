using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Compliance;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.Infrastructure.Compliance;

/// <summary>Reads compliance's compacted snapshot held in memory; a customer compliance never touched has no limits.</summary>
public sealed class CompactedSessionLimits(ICompactedState<RestrictionsChangedV1> state) : ISessionLimits
{
    public SessionLimits For(string userId) =>
        state.TryGet(userId, out var snapshot) ? new SessionLimits(snapshot.SessionLimitMinutes, snapshot.RealityCheckMinutes) : SessionLimits.None;
}
