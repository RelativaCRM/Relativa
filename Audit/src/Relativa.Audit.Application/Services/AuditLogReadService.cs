using FluentValidation;
using Microsoft.Extensions.Options;
using Relativa.Audit.Application.DTOs;
using Relativa.Audit.Application.Exceptions;
using Relativa.Audit.Application.Interfaces;
using Relativa.Audit.Application.Options;
using Relativa.Audit.Application.Validators;

namespace Relativa.Audit.Application.Services;

public sealed class AuditLogReadService(
    IAuditLogReadRepository repository,
    IValidator<GetAuditLogQuery> queryValidator,
    IOptions<AuditLogReadOptions> options) : IAuditLogReadService
{
    private readonly AuditLogReadOptions _opt = options.Value;

    public async Task<AuditLogListResponse> GetAsync(GetAuditLogQuery q, int callerUserId, CancellationToken ct)
    {
        await queryValidator.ValidateAndThrowAsync(q, ct);
        var category = q.EntityTypeCategory.Trim().ToLowerInvariant();

        var from = q.DateFrom ?? DateTimeOffset.UtcNow.AddDays(-_opt.DefaultDateRangeDays);
        var to = q.DateTo ?? DateTimeOffset.UtcNow;
        var index = q.Index;
        var pageSize = q.PageSize;
        var skip = (index - 1) * pageSize;

        await repository.EnsureResourcesExistAsync(q, category, ct);
        await repository.EnsureRbacAsync(callerUserId, category, q.WorkspaceId, q.OrganizationId, ct);

        var filterContext = await repository.BuildFilterContextAsync(q, category, ct);

        return category switch
        {
            "entity" => await repository.GetEntityScopeAsync(
                from, to, q.Action, q.EntityId, q.DomainEntityType, q.ActorUserId, q.WorkspaceId!.Value,
                skip, pageSize, index, filterContext, ct),
            "workspace" => await repository.GetWorkspaceScopeAsync(
                from, to, q.Action, q.ActorUserId, q.WorkspaceId!.Value, skip, pageSize, index, filterContext, ct),
            "organization" => await repository.GetOrganizationScopeAsync(
                from, to, q.Action, q.ActorUserId, q.OrganizationId!.Value, skip, pageSize, index, filterContext, ct),
            "user" => await repository.GetUserScopeAsync(
                from, to, q.Action, q.ActorUserId, q.TargetUserId, callerUserId, skip, pageSize, index,
                filterContext, ct),
            _ => throw new AppException("invalid_category", 400, "Invalid category.")
        };
    }
}
