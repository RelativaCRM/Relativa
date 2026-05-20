namespace Relativa.Graph.Dashboard.Dto;

public record MemberActivityDto(
    int UserId,
    string FullName,
    string RoleName,
    int DealsOwned,
    int TasksOwned,
    int TasksDone
);
