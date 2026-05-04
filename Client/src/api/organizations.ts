import { api } from '@/api/http';
import type { UserProfile } from '@/api/auth';

/* ── DTOs ───────────────────────────────────────────────── */

export interface OrganizationDto {
  id: number;
  name: string;
  memberCount: number;
  userRole: string;
}

export interface OrgMemberDto {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  roleName: string;
  joinedAt: string;
}

export interface OrgRoleDto {
  id: number;
  name: string;
  isSystem: boolean;
  permissions: { id: number; name: string }[];
}

export interface OrgInvitationDto {
  id: number;
  organizationId: number;
  email: string;
  organizationName: string;
  roleName: string;
  status: string;
  token: string;
  expiresAt: string;
}

export interface JoinRequestDto {
  id: number;
  userId: number;
  userName: string;
  userEmail: string;
  message: string;
  status: string;
  createdAt: string;
  reviewedByName: string | null;
  reviewedAt: string | null;
}

export interface MyInvitationsDto {
  organizationInvitations: OrgInvitationDto[];
}

/* ── API ────────────────────────────────────────────────── */

const CORE = '/core/api/v1';

export const orgApi = {
  /* Organizations */
  create(name: string): Promise<OrganizationDto> {
    return api.post<OrganizationDto>(`${CORE}/organizations`, { name });
  },
  list(): Promise<OrganizationDto[]> {
    return api.get<OrganizationDto[]>(`${CORE}/organizations`);
  },
  search(query: string): Promise<OrganizationDto[]> {
    return api.get<OrganizationDto[]>(
      `${CORE}/organizations/search?q=${encodeURIComponent(query)}`,
    );
  },
  get(id: number): Promise<OrganizationDto> {
    return api.get<OrganizationDto>(`${CORE}/organizations/${id}`);
  },

  /* Members */
  listMembers(orgId: number): Promise<OrgMemberDto[]> {
    return api.get<OrgMemberDto[]>(`${CORE}/organizations/${orgId}/members`);
  },
  removeMember(orgId: number, userId: number): Promise<void> {
    return api.del(`${CORE}/organizations/${orgId}/members/${userId}`);
  },
  changeMemberRole(orgId: number, userId: number, roleId: number): Promise<void> {
    return api.put(`${CORE}/organizations/${orgId}/members/${userId}/role`, {
      roleId,
    });
  },
  updateOrgUserProfile(
    orgId: number,
    userId: number,
    payload: { firstName: string; lastName: string },
  ): Promise<UserProfile> {
    return api.patch<UserProfile>(
      `${CORE}/organizations/${orgId}/users/${userId}`,
      { ...payload },
    );
  },

  /* Invitations */
  invite(
    orgId: number,
    email: string,
    orgRoleId?: number,
  ): Promise<OrgInvitationDto> {
    return api.post<OrgInvitationDto>(
      `${CORE}/organizations/${orgId}/invitations`,
      orgRoleId ? { email, orgRoleId } : { email },
    );
  },
  listInvitations(orgId: number): Promise<OrgInvitationDto[]> {
    return api.get<OrgInvitationDto[]>(
      `${CORE}/organizations/${orgId}/invitations`,
    );
  },
  cancelInvitation(orgId: number, invId: number): Promise<void> {
    return api.del(`${CORE}/organizations/${orgId}/invitations/${invId}`);
  },
  resendInvitation(orgId: number, invId: number): Promise<OrgInvitationDto> {
    return api.post<OrgInvitationDto>(
      `${CORE}/organizations/${orgId}/invitations/${invId}/resend`,
    );
  },
  acceptOrgInvitation(token: string): Promise<void> {
    return api.post(`${CORE}/invitations/accept-org`, { token });
  },

  /* Join requests */
  submitJoinRequest(orgId: number, message: string): Promise<JoinRequestDto> {
    return api.post<JoinRequestDto>(
      `${CORE}/organizations/${orgId}/join-requests`,
      { message },
    );
  },
  listJoinRequests(orgId: number): Promise<JoinRequestDto[]> {
    return api.get<JoinRequestDto[]>(
      `${CORE}/organizations/${orgId}/join-requests`,
    );
  },
  reviewJoinRequest(
    orgId: number,
    reqId: number,
    decision: 'Approved' | 'Rejected',
  ): Promise<void> {
    return api.put(`${CORE}/organizations/${orgId}/join-requests/${reqId}`, {
      decision,
    });
  },
  myJoinRequests(): Promise<JoinRequestDto[]> {
    return api.get<JoinRequestDto[]>(`${CORE}/join-requests/mine`);
  },

  /* Roles */
  listRoles(orgId: number): Promise<OrgRoleDto[]> {
    return api.get<OrgRoleDto[]>(`${CORE}/organizations/${orgId}/roles`);
  },

  /* Combined invitations (my inbox) */
  myInvitations(): Promise<MyInvitationsDto> {
    return api.get<MyInvitationsDto>(`${CORE}/invitations/mine`);
  },
  myOrganizationInvitations(): Promise<OrgInvitationDto[]> {
    return api.get<OrgInvitationDto[]>(`${CORE}/invitations/mine/organization`);
  },
};
