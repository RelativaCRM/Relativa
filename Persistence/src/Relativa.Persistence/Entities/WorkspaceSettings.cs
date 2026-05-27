namespace Relativa.Persistence.Entities;

public class WorkspaceSettings
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public decimal HighRiskThreshold { get; set; }
    public decimal MediumRiskThreshold { get; set; }
    public Workspace Workspace { get; set; } = null!;
}
