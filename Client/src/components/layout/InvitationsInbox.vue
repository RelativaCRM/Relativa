<script setup lang="ts">
import { onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import FormError from '@/components/feedback/FormError.vue';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';
import { currentLocale } from '@/i18n';
import { useInvitationsInbox } from '@/composables/useInvitationsInbox';
import type { OrgInvitationDto } from '@/api/organizations';

const emit = defineEmits<{ accepted: [] }>();

const { t } = useI18n();
const {
  inboxLoading,
  inboxError,
  inboxFilter,
  busyToken,
  busyRequestId,
  hasInbox,
  filteredInvitations,
  filteredRequests,
  ensureLoaded,
  acceptInvite,
  declineInvite,
  cancelRequest,
  clearAll,
} = useInvitationsInbox();

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString(currentLocale());
}

async function onAccept(invitation: OrgInvitationDto) {
  if (await acceptInvite(invitation)) emit('accepted');
}

onMounted(() => {
  ensureLoaded();
});
</script>

<template>
  <div>
    <div class="mb-2 flex items-center justify-between">
      <span class="text-xs font-semibold uppercase tracking-wide text-ink-500">
        {{ t('onboarding.invitations') }}
      </span>
      <button
        v-if="hasInbox"
        type="button"
        class="text-xs font-medium text-brand-600 hover:underline"
        @click="clearAll"
      >
        {{ t('onboarding.clearAll') }}
      </button>
    </div>

    <InputText
      v-if="hasInbox"
      v-model="inboxFilter"
      :placeholder="t('onboarding.filterPlaceholder')"
      class="!h-8 w-full !text-sm"
    />

    <LoadingSkeleton
      v-if="inboxLoading"
      variant="list"
      :rows="2"
      class="mt-3"
      :label="t('onboarding.invitations')"
    />

    <div
      v-else-if="!hasInbox"
      class="flex flex-col items-center gap-3 py-8 text-center text-sm text-ink-500"
    >
      <i class="pi pi-bell-slash text-3xl text-ink-300" />
      <span>{{ t('onboarding.invitationsEmpty') }}</span>
    </div>

    <div v-else class="nav-scroll mt-2 max-h-72 overflow-y-auto">
      <template v-if="filteredInvitations.length">
        <p class="px-0.5 pb-1 pt-1 text-[11px] font-semibold uppercase text-ink-400">
          {{ t('onboarding.incomingHeading') }}
        </p>
        <div
          v-for="inv in filteredInvitations"
          :key="`inv-${inv.id}`"
          class="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0"
        >
          <div class="min-w-0">
            <p class="truncate text-sm font-medium text-ink-900">{{ inv.organizationName }}</p>
            <p class="text-[11px] text-ink-500">
              {{ t('onboarding.expires', { date: formatDate(inv.expiresAt) }) }}
            </p>
          </div>
          <div class="flex shrink-0 items-center gap-1">
            <Button
              size="small"
              severity="secondary"
              text
              :loading="busyToken === inv.token"
              :aria-label="t('onboarding.revoke')"
              @click="declineInvite(inv)"
            >
              <i class="pi pi-times text-xs" />
            </Button>
            <Button
              size="small"
              :label="t('onboarding.accept')"
              :loading="busyToken === inv.token"
              @click="onAccept(inv)"
            />
          </div>
        </div>
      </template>

      <template v-if="filteredRequests.length">
        <p class="px-0.5 pb-1 pt-3 text-[11px] font-semibold uppercase text-ink-400">
          {{ t('onboarding.myRequestsHeading') }}
        </p>
        <div
          v-for="req in filteredRequests"
          :key="`req-${req.id}`"
          class="flex items-center justify-between gap-2 border-b border-line py-2 last:border-0"
        >
          <div class="min-w-0">
            <p class="truncate text-sm font-medium text-ink-900">{{ req.organizationName }}</p>
            <p class="text-[11px] text-ink-500">
              {{ t('onboarding.requested', { date: formatDate(req.createdAt) }) }}
            </p>
          </div>
          <div class="flex shrink-0 items-center gap-1.5">
            <span
              class="inline-flex items-center px-2 py-0.5 text-[10px] font-semibold bg-amber-50 text-amber-700 ring-1 ring-inset ring-amber-200"
            >
              {{ t('onboarding.pendingReview') }}
            </span>
            <Button
              size="small"
              severity="secondary"
              text
              :loading="busyRequestId === req.id"
              :aria-label="t('onboarding.revoke')"
              @click="cancelRequest(req)"
            >
              <i class="pi pi-times text-xs" />
            </Button>
          </div>
        </div>
      </template>
    </div>

    <FormError v-if="inboxError" :message="inboxError" class="mt-2" />
  </div>
</template>
