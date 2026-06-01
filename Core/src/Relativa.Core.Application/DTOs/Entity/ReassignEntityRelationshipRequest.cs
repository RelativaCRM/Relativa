namespace Relativa.Core.Application.DTOs.Entity;

/// <summary>
/// Exactly one field must be non-null: supply NewTargetEntityId when swapping an outgoing link,
/// or NewSourceEntityId when swapping an inbound link.
/// </summary>
public sealed record ReassignEntityRelationshipRequest(
    int? NewSourceEntityId,
    int? NewTargetEntityId);
