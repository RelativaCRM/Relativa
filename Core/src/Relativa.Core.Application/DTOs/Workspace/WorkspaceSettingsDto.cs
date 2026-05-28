namespace Relativa.Core.Application.DTOs.Workspace;

public sealed record WorkspaceSettingsDto(
    int WorkspaceId,
    string? Description,
    decimal HighRiskThreshold,
    decimal MediumRiskThreshold,
    bool RiskScoringEnabled);
