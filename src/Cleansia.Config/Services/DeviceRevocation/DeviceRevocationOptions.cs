namespace Cleansia.Config.Services.DeviceRevocation;

/// <summary>
/// Binds the <c>DeviceRevocation</c> config section (ADR-0026 D8). Both values are security bounds:
/// <see cref="Enabled"/> is the same-day ops kill switch and <see cref="RefreshSeconds"/> is the
/// enforcement-latency ceiling — changing either requires a superseding ADR (raw-file pinned by
/// TC-REVOKE-NOW-7). Defaults are the accepted production values.
/// </summary>
public sealed class DeviceRevocationOptions
{
    public const string SectionName = "DeviceRevocation";

    public bool Enabled { get; set; } = true;

    public int RefreshSeconds { get; set; } = 30;
}
