namespace Relativa.Core.Application;

/// <summary>
/// Numeric <c>organization_roles.priority</c>: lower value = stronger authority (0 is strongest).
/// Aligns with migration SQL and seeds.
/// </summary>
public static class OrganizationRolePriorityTiers
{
    public const int Owner = 0;
    public const int Admin = 1;
    public const int Member = 6;

    /// <summary>Default for custom org roles when backfilling unset rows (weaker than <see cref="Member"/>).</summary>
    public const int CustomRoleDefaultBackfill = 10;

    /// <summary>Minimum allowed priority for custom (non-system) org roles — must stay strictly weaker than <see cref="Owner"/>.</summary>
    public const int CustomRoleMinimum = 1;
}
