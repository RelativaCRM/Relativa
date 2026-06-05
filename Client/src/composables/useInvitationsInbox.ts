import { ref, computed } from 'vue';
import {
  orgApi,
  type OrgInvitationDto,
  type JoinRequestDto,
} from '@/api/organizations';
import { normalizeError } from '@/api/errors';
import { useOrganizationStore } from '@/stores/organization';
import { i18n } from '@/i18n';

const t = (key: string, params?: Record<string, unknown>): string =>
  params ? i18n.global.t(key, params) : i18n.global.t(key);

const orgInvitations = ref<OrgInvitationDto[]>([]);
const pendingJoinRequests = ref<JoinRequestDto[]>([]);
const inboxLoading = ref(true);
const inboxError = ref<string | null>(null);
const inboxFilter = ref('');
const busyToken = ref<string | null>(null);
const busyRequestId = ref<number | null>(null);
let loadedOnce = false;

const inboxCount = computed(
  () => orgInvitations.value.length + pendingJoinRequests.value.length,
);
const hasInbox = computed(() => inboxCount.value > 0);

const filteredInvitations = computed(() => {
  const q = inboxFilter.value.trim().toLowerCase();
  if (!q) return orgInvitations.value;
  return orgInvitations.value.filter((i) => i.organizationName.toLowerCase().includes(q));
});
const filteredRequests = computed(() => {
  const q = inboxFilter.value.trim().toLowerCase();
  if (!q) return pendingJoinRequests.value;
  return pendingJoinRequests.value.filter((r) => r.organizationName.toLowerCase().includes(q));
});

async function loadInbox() {
  inboxLoading.value = true;
  try {
    const [invitations, joinReqs] = await Promise.all([
      orgApi.myOrganizationInvitations(),
      orgApi.myJoinRequests(),
    ]);
    orgInvitations.value = invitations;
    pendingJoinRequests.value = joinReqs.filter((r) => r.status === 'Pending');
  } catch {
    orgInvitations.value = [];
    pendingJoinRequests.value = [];
  } finally {
    inboxLoading.value = false;
  }
}

async function ensureLoaded() {
  if (loadedOnce) return;
  loadedOnce = true;
  await loadInbox();
}

function reset() {
  loadedOnce = false;
  orgInvitations.value = [];
  pendingJoinRequests.value = [];
  inboxFilter.value = '';
  inboxError.value = null;
  inboxLoading.value = true;
}

async function acceptInvite(invitation: OrgInvitationDto): Promise<boolean> {
  busyToken.value = invitation.token;
  inboxError.value = null;
  try {
    await orgApi.acceptOrgInvitation(invitation.token);
    await useOrganizationStore().fetchOrganizations();
    orgInvitations.value = orgInvitations.value.filter((i) => i.id !== invitation.id);
    return true;
  } catch (err) {
    inboxError.value = normalizeError(err, t('onboarding.acceptFailed')).message;
    return false;
  } finally {
    busyToken.value = null;
  }
}

async function declineInvite(invitation: OrgInvitationDto) {
  busyToken.value = invitation.token;
  inboxError.value = null;
  try {
    await orgApi.declineOrgInvitation(invitation.token);
    orgInvitations.value = orgInvitations.value.filter((i) => i.id !== invitation.id);
  } catch (err) {
    inboxError.value = normalizeError(err, t('onboarding.declineFailed')).message;
  } finally {
    busyToken.value = null;
  }
}

async function cancelRequest(request: JoinRequestDto) {
  busyRequestId.value = request.id;
  inboxError.value = null;
  try {
    await orgApi.cancelMyJoinRequest(request.id);
    pendingJoinRequests.value = pendingJoinRequests.value.filter((r) => r.id !== request.id);
  } catch (err) {
    inboxError.value = normalizeError(err, t('onboarding.cancelFailed')).message;
  } finally {
    busyRequestId.value = null;
  }
}

async function clearAll() {
  inboxError.value = null;
  const tokens = orgInvitations.value.map((i) => i.token);
  const ids = pendingJoinRequests.value.map((r) => r.id);
  try {
    await Promise.all([
      ...tokens.map((tk) => orgApi.declineOrgInvitation(tk)),
      ...ids.map((id) => orgApi.cancelMyJoinRequest(id)),
    ]);
    orgInvitations.value = [];
    pendingJoinRequests.value = [];
  } catch (err) {
    inboxError.value = normalizeError(err, t('onboarding.declineFailed')).message;
    await loadInbox();
  }
}

export function useInvitationsInbox() {
  return {
    orgInvitations,
    pendingJoinRequests,
    inboxLoading,
    inboxError,
    inboxFilter,
    busyToken,
    busyRequestId,
    inboxCount,
    hasInbox,
    filteredInvitations,
    filteredRequests,
    loadInbox,
    ensureLoaded,
    reset,
    acceptInvite,
    declineInvite,
    cancelRequest,
    clearAll,
  };
}
