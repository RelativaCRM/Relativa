using Relativa.Core.Application.DTOs.Member;

namespace Relativa.Core.Application.Interfaces;

public interface IWorkspaceMemberService
{
    Task<List<WorkspaceMemberDto>> GetMembersAsync(int workspaceId, int userId, CancellationToken ct = default);
    Task UpdateRoleAsync(int workspaceId, int targetUserId, int callerUserId, UpdateMemberRoleRequest request, CancellationToken ct = default);
    Task RemoveAsync(int workspaceId, int targetUserId, int callerUserId, CancellationToken ct = default);
}
