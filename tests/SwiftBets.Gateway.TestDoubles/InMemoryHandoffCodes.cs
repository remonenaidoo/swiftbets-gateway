using System.Collections.Concurrent;
using SwiftBets.Gateway.Application.Sessions;

namespace SwiftBets.Gateway.TestDoubles;

public sealed class InMemoryHandoffCodes : IHandoffCodes
{
    private readonly ConcurrentDictionary<string, SessionTokens> _codes = new();

    public Task StoreAsync(string codeHash, SessionTokens browser, TimeSpan lifetime)
    {
        _codes[codeHash] = browser;
        return Task.CompletedTask;
    }

    public Task<SessionTokens?> TakeAsync(string codeHash) => Task.FromResult(_codes.TryRemove(codeHash, out var tokens) ? tokens : null);
}
