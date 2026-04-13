namespace Relativa.Core.Application.DTOs.Workspace;

public sealed record CreateWorkspaceRequest(string Name, int? OrganizationId = null);
