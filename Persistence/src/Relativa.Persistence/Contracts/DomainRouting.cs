namespace Relativa.Persistence.Contracts;

public static class DomainRouting
{
    public const string ExchangeName = "relativa.domain";

    public const string CoreWorkspaceVerbCreated = "created";
    public const string CoreWorkspaceVerbUpdated = "updated";
    public const string CoreWorkspaceVerbArchived = "archived";

    public static string RoutingKeyCoreWorkspace(string verb) =>
        verb is null ? throw new ArgumentNullException(nameof(verb))
        : $"{BoundedContext.Core}.workspace.{verb}";

    private static class BoundedContext
    {
        public const string Core = "core";
    }
}
