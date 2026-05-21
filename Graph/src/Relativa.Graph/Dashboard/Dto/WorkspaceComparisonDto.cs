namespace Relativa.Graph.Dashboard.Dto;

public record WorkspaceComparisonDto(
    int WorkspaceId,
    string WorkspaceName,
    int DealCount,
    decimal PipelineValue,
    double WinRate,
    int ClientCount,
    int MemberCount,
    string TopStage
);
