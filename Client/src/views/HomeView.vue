<script setup lang="ts">
import { useAuthStore } from '@/stores/auth';
import { useOrganizationStore } from '@/stores/organization';

const auth = useAuthStore();
const orgStore = useOrganizationStore();
</script>

<template>
  <section class="max-w-3xl">
    <h1 class="text-2xl font-bold text-ink-900">
      Welcome{{ auth.user ? `, ${auth.user.firstName}` : '' }}
    </h1>

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
          <dd>{{ orgStore.currentOrg?.userRole ?? '—' }}</dd>
          <dt class="text-ink-500">Members</dt>
          <dd>{{ orgStore.currentOrg?.memberCount ?? '—' }}</dd>
        </dl>
      </div>
    </div>
  </section>
</template>
