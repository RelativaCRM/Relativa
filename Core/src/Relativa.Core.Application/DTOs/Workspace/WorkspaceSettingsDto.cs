namespace Relativa.Core.Application.DTOs.Workspace;

public sealed record WorkspaceSettingsDto(
    int WorkspaceId,
    string Name,
    string? Description,
    decimal HighRiskThreshold,
    decimal MediumRiskThreshold,
    bool RiskScoringEnabled);
