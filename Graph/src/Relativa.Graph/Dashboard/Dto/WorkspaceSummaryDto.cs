namespace Relativa.Graph.Dashboard.Dto;

public record WorkspaceSummaryDto(
    int WorkspaceId,
    string WorkspaceName,
    int TotalDeals,
    int OpenDeals,
    int WonDeals,
    int LostDeals,
    decimal? TotalPipelineValue,
    double? WinRate,
    decimal? AvgDealSize,
    int TotalClients,
    int ActiveClients,
    int? TasksOverdue,
    int? DealsClosingThisMonth,
    int MemberCount,
    string AccessLevel
);
