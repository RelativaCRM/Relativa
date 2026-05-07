<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';

const auth = useAuthStore();
const orgStore = useOrganizationStore();

const now = ref(new Date());
let tickHandle: ReturnType<typeof setInterval> | null = null;

onMounted(() => {
  tickHandle = setInterval(() => {
    now.value = new Date();
  }, 60_000);
});

onUnmounted(() => {
  if (tickHandle) clearInterval(tickHandle);
});

const greeting = computed(() => {
  const hour = now.value.getHours();
  if (hour >= 5 && hour < 12) return 'Good morning';
  if (hour >= 12 && hour < 18) return 'Good afternoon';
  return 'Good evening';
});

const firstName = computed(() => auth.user?.firstName?.trim() ?? '');

function displayOrgRole(roleName: string | null | undefined): string {
  if (!roleName) return '—';
  if (roleName === 'org_owner') return 'Owner';
  if (roleName === 'org_admin') return 'Admin';
  if (roleName === 'org_member') return 'Member';
  return roleName;
}
</script>

<template>
  <section class="max-w-3xl">
    <div class="relative overflow-hidden rounded-2xl border border-line bg-white px-7 py-8 shadow-sm">
      <div
        class="pointer-events-none absolute -top-12 -right-12 h-48 w-48 rounded-full bg-brand-100/60 blur-3xl"
        aria-hidden="true"
      />
      <div
        class="pointer-events-none absolute -bottom-16 -left-10 h-44 w-44 rounded-full bg-brand-50 blur-3xl"
        aria-hidden="true"
      />

      <div class="relative">
        <p class="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand-600">
          {{ now.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' }) }}
        </p>
        <h1 class="mt-2 text-[28px] font-bold text-ink-900 leading-tight">
          {{ greeting }}<span v-if="firstName">, {{ firstName }}</span>
        </h1>
      </div>
    </div>

    <div class="mt-6 grid gap-4 sm:grid-cols-2">
      <div class="rounded-xl border border-line bg-white p-5">
        <h2 class="text-sm font-semibold text-ink-900">Session</h2>
        <dl class="mt-3 text-sm text-ink-700 grid grid-cols-[auto,1fr] gap-x-6 gap-y-2">
          <dt class="text-ink-500">Email</dt>
          <dd>{{ auth.user?.email ?? '—' }}</dd>
          <dt class="text-ink-500">Token expiry</dt>
          <dd>{{ auth.expiresAt ? new Date(auth.expiresAt).toLocaleString() : '—' }}</dd>
        </dl>
      </div>

      <div class="rounded-xl border border-line bg-white p-5">
        <h2 class="text-sm font-semibold text-ink-900">Organization</h2>
        <dl class="mt-3 text-sm text-ink-700 grid grid-cols-[auto,1fr] gap-x-6 gap-y-2">
          <dt class="text-ink-500">Name</dt>
          <dd>{{ orgStore.currentOrg?.name ?? '—' }}</dd>
          <dt class="text-ink-500">Role</dt>
          <dd>{{ displayOrgRole(orgStore.currentOrg?.userRole) }}</dd>
          <dt class="text-ink-500">Members</dt>
          <dd>{{ orgStore.currentOrg?.memberCount ?? '—' }}</dd>
        </dl>
      </div>
    </div>
  </section>
</template>
