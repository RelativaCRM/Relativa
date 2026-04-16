using Relativa.Core.Application.DTOs.OrgInvitation;

namespace Relativa.Core.Application.Interfaces;

public interface IOrgInvitationService
{
    Task<OrgInvitationDto> InviteAsync(int organizationId, int callerUserId, InviteToOrgRequest request, CancellationToken ct = default);
    Task<List<OrgInvitationDto>> GetByOrganizationAsync(int organizationId, int callerUserId, CancellationToken ct = default);
    Task CancelAsync(int organizationId, int invitationId, int callerUserId, CancellationToken ct = default);
    Task AcceptAsync(int userId, string userEmail, string token, CancellationToken ct = default);
}
