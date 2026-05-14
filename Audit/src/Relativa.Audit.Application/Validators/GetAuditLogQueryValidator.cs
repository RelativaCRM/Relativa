using FluentValidation;

namespace Relativa.Audit.Application.Validators;

public sealed class GetAuditLogQueryValidator : AbstractValidator<GetAuditLogQuery>
{
    private static readonly HashSet<string> AllowedCategories =
        ["entity", "workspace", "organization", "user"];

    public GetAuditLogQueryValidator()
    {
        RuleFor(q => q.EntityTypeCategory)
            .NotEmpty()
            .Must(c => AllowedCategories.Contains(c.Trim().ToLowerInvariant()))
            .WithMessage("entity_type must be one of: entity, workspace, organization, user.");

        RuleFor(q => q.Index)
            .GreaterThanOrEqualTo(1);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(q => q)
            .Must(q => !q.DateFrom.HasValue || !q.DateTo.HasValue || q.DateFrom <= q.DateTo)
            .WithName("date_from")
            .WithMessage("date_from must be less than or equal to date_to.");

        When(q => Normalize(q.EntityTypeCategory) is "entity" or "workspace", () =>
        {
            RuleFor(q => q.WorkspaceId)
                .NotNull()
                .WithMessage("workspace_id is required for entity and workspace audit.");
        });

        When(q => Normalize(q.EntityTypeCategory) == "organization", () =>
        {
            RuleFor(q => q.OrganizationId)
                .NotNull()
                .WithMessage("organization_id is required for organization audit.");
        });
    }

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();
}
