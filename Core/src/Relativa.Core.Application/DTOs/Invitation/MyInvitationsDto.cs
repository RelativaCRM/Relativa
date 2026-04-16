using Relativa.Core.Application.DTOs.OrgInvitation;

namespace Relativa.Core.Application.DTOs.Invitation;

public sealed record MyInvitationsDto(List<InvitationDto> WorkspaceInvitations, List<OrgInvitationDto> OrganizationInvitations);
