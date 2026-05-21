namespace Relativa.Graph.Dashboard.Dto;

public record RiskDistributionDto(
    Dictionary<string, RiskBucketDto> Distribution,
    List<RiskItemDto> Items
);

public record RiskBucketDto(
    int Count,
    decimal TotalValue,
    double Percentage
);

public record RiskItemDto(
    int EntityId,
    string Title,
    double Score,
    decimal Value,
    string RiskBucket,
    string? ClientName
);
