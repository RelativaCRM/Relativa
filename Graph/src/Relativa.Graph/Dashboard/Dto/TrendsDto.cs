namespace Relativa.Graph.Dashboard.Dto;

public record TrendsDto(List<TrendsMonthDto> Months);

public record TrendsMonthDto(
    string Label,
    int NewDeals,
    int ClosedWon,
    int ClosedLost,
    decimal WonRevenue,
    decimal ActiveValue
);
