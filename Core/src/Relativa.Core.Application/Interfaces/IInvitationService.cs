using Relativa.Core.Application.DTOs.Invitation;

namespace Relativa.Core.Application.Interfaces;

public interface IInvitationService
{
    Task<InvitationDto> InviteAsync(int workspaceId, int callerUserId, InviteMemberRequest request, CancellationToken ct = default);
    Task<List<InvitationDto>> GetPendingAsync(int workspaceId, int callerUserId, CancellationToken ct = default);
    Task CancelAsync(int workspaceId, int invitationId, int callerUserId, CancellationToken ct = default);
    Task AcceptAsync(int userId, string userEmail, AcceptInvitationRequest request, CancellationToken ct = default);
}
