using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Gateway.Application.Sessions;

public sealed class BrowserSessionOptions
{
    public const string SectionName = "Gateway:Sessions";

    /// <summary>Matches identity's refresh-token lifetime; a session unused this long is gone.</summary>
    [Range(1, 90)]
    public int LifetimeDays { get; set; } = 7;

    [Range(5, 300)]
    public int RefreshAheadSeconds { get; set; } = 30;

    [Range(5, 300)]
    public int RotationGraceSeconds { get; set; } = 30;

    [Range(1, 3600)]
    public int LastSeenResolutionSeconds { get; set; } = 300;
}
