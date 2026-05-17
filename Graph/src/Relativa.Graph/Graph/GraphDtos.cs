namespace Relativa.Graph.Graph;

public record GraphNodeDto(
    string Id,
    string Type,
    string Label,
    string? Subtitle,
    string? EntityTypeName,
    int ResourceId,
    string ResourceType,
    int? WorkspaceId,
    IReadOnlyList<string> Permissions,
    string? HighlightTag = null
);

public record GraphEdgeDto(
    string Id,
    string From,
    string To,
    string Type,
    string? Label
);

public record GraphResponseDto(
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges
);
