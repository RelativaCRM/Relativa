namespace Relativa.Persistence.Contracts;

public static class DomainPayloadTypes
{
    public const string WorkspaceLifecycleV1 = "relativa.domain.workspace.lifecycle.v1";
    public const string WorkspaceSettingsUpdatedV1 = "relativa.domain.workspace.settings_updated.v1";
    public const string OrganizationSettingsUpdatedV1 = "relativa.domain.organization.settings_updated.v1";
    public const string EntityAnalysisRefreshV1 = "relativa.domain.entity.analysis_refresh.v1";
    public const string MlRecalculateEnqueuedV1 = "relativa.domain.ml.recalculate_enqueued.v1";
    public const string MlRecalculateProgressV1 = "relativa.domain.ml.recalculate_progress.v1";
    public const string MlRecalculateCompletedV1 = "relativa.domain.ml.recalculate_completed.v1";
}
