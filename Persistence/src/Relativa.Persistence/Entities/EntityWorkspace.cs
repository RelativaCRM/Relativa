namespace Relativa.Persistence.Entities;

public class EntityWorkspace
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public int WorkspaceId { get; set; }
    public Entity Entity { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
}
