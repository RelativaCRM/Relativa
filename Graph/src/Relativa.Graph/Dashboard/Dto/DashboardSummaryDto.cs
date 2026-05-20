namespace Relativa.Graph.Dashboard.Dto;

public record DashboardSummaryDto(
    int TotalDeals,
    int OpenDeals,
    decimal TotalDealValue,
    int WonDeals,
    int LostDeals,
    double WinRate,
    decimal AvgDealSize,
    int TotalClients,
    int ActiveClients,
    int DealsClosingThisMonth,
    int TasksOverdue,
    int TotalWorkspaces,
    int ActiveWorkspaces,
    string AccessLevel
);
