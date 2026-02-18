namespace LedgeLink.Participant.UI.Domain.Models;

/// <summary>
/// Domain model: the identity and branding of the current participant instance.
/// Populated from environment variables — the only thing that differs between
/// the Schroders and Hargreaves deployments of the same binary.
/// </summary>
public sealed record ParticipantContext
{
    public string Name        { get; init; } = "Participant";
    public string Color       { get; init; } = "#374151";
    public string Role        { get; init; } = "Observer";
    public string LogoInitial { get; init; } = "P";
}
