namespace Relativa.Core.Application.DTOs.Workspace;

public sealed record UpdateWorkspaceSettingsRequest(
    string Name,
    string? Description,
    decimal HighRiskThreshold,
    decimal MediumRiskThreshold,
    bool RiskScoringEnabled);
