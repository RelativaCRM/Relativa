namespace Relativa.Core.Application.DTOs.OrgInvitation;

/// <summary>
/// Invite a new member to an organization.
/// </summary>
/// <param name="Email">Email address of the invitee (case-insensitive).</param>
/// <param name="OrgRoleId">
/// Optional system/org-scoped role id to assign on acceptance. When null the server assigns
/// the <c>org_member</c> system role. Non-default roles require the caller to have the
/// <c>assign_org_roles</c> permission in the organization.
/// </param>
public sealed record InviteToOrgRequest(string Email, int? OrgRoleId = null);
