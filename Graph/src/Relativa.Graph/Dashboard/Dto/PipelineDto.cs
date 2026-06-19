namespace Relativa.Graph.Dashboard.Dto;

public record PipelineDto(
    List<PipelineStageDto> Stages,
    Dictionary<string, int> StatusBreakdown,
    double ConversionRate,
    double AvgDaysToClose
);

public record PipelineStageDto(
    string Name,
    int Count,
    decimal Value,
    double Percentage
);
