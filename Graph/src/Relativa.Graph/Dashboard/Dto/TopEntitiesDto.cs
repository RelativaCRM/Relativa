namespace Relativa.Graph.Dashboard.Dto;

public record TopEntitiesDto(
    List<TopDealDto> TopDeals,
    List<TopClientDto> TopClients
);

public record TopDealDto(
    int EntityId,
    string Title,
    decimal Value,
    string? Stage,
    double? ClosureScore,
    string? ClientName,
    string? Priority
);

public record TopClientDto(
    int EntityId,
    string Name,
    string? Industry,
    decimal LifetimeValue,
    bool IsExpectedLtv,
    int ActiveDeals,
    double? AvgClosureScore
);
