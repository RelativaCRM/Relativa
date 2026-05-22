<script setup lang="ts">
import { ref, computed, watch, reactive } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Button from 'primevue/button';
import Message from 'primevue/message';
import InputText from 'primevue/inputtext';
import InputNumber from 'primevue/inputnumber';
import Select from 'primevue/select';
import DatePicker from 'primevue/datepicker';
import ToggleSwitch from 'primevue/toggleswitch';
import ConfirmDialog from 'primevue/confirmdialog';
import Dialog from 'primevue/dialog';
import Skeleton from 'primevue/skeleton';
import { useConfirm } from 'primevue/useconfirm';
import { useToast } from 'primevue/usetoast';
import { normalizeError, firstFieldError, type FieldErrors } from '@/api/errors';
import { useWorkspaceStore } from '@/stores/workspace';
import { useEntityStore } from '@/stores/entity';
import { hasWorkspacePermission } from '@/utils/workspacePermissions';
import type {
  EntityDetailDto,
  EntityTypeDto,
  EntityTypePropertyDto,
  EntityPropertyValueDto,
  EntityListItemDto,
} from '@/api/entities';
import { entityApi } from '@/api/entities';
import { isEntityTypeUiLocked } from '@/utils/entityTypes';
import { mlApi, type DealScoreDto } from '@/api/ml';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';

const props = defineProps<{
  workspaceId: number;
  entityId: number;
}>();

const emit = defineEmits<{
  close: [];
  updated: [];
}>();

const route = useRoute();
const router = useRouter();
const wsStore = useWorkspaceStore();
const entityStore = useEntityStore();
const confirm = useConfirm();
const toast = useToast();

const loading = ref(true);
const saving = ref(false);
const errorMessage = ref<string | null>(null);
const detail = ref<EntityDetailDto | null>(null);
const editMode = ref(false);
const fieldErrors = ref<FieldErrors>({});

const score = ref<DealScoreDto | null>(null);
const scoreLoading = ref(false);
const scoreError = ref<string | null>(null);

type FieldValue = string | number | boolean | Date | null;
const editValues = ref<Record<number, FieldValue>>({});

const canEdit = computed(() =>
  hasWorkspacePermission(wsStore.currentWorkspace, 'edit_entities'),
);
const canEditArchived = computed(() =>
  hasWorkspacePermission(wsStore.currentWorkspace, 'edit_archived_entities'),
);
const canEditCurrentEntity = computed(() => {
  if (!detail.value) return false;
  return detail.value.isArchived ? canEditArchived.value : canEdit.value;
});
const canDelete = computed(() =>
  hasWorkspacePermission(wsStore.currentWorkspace, 'delete_entities'),
);

const writableProps = computed(() => {
  const d = detail.value;
  if (!d) return [];
  return d.propertyValues.filter((p) => !p.isReadonly);
});

const currentEntityAllReadonly = computed(() => {
  const d = detail.value;
  return !!d && d.propertyValues.length > 0 && writableProps.value.length === 0;
});

const typeSchema = computed(() => {
  const d = detail.value;
  if (!d) return null;
  return entityStore.types.find((t) => t.id === d.entityTypeId) ?? null;
});

function allowedValuesFor(propertyId: number): string[] {
  return typeSchema.value?.properties.find((p) => p.propertyId === propertyId)?.allowedValues ?? [];
}

// ── Per-tab relationship metadata helpers ────────────────────────────────────

function tabRelSchema(tab: EdgeRelTab) {
  return tab.direction === 'out'
    ? typeSchema.value?.outgoingRelationships.find((r) => r.relationshipTypeId === tab.relationshipTypeId)
    : typeSchema.value?.incomingRelationships.find((r) => r.relationshipTypeId === tab.relationshipTypeId);
}

function tabIsRequired(tab: EdgeRelTab): boolean {
  return tabRelSchema(tab)?.isRequired ?? false;
}

function relatedTypeAllReadonly(typeName: string): boolean {
  const t = entityStore.types.find((et) => et.name === typeName);
  return !!t && t.properties.length > 0 && t.properties.every((p) => p.isReadonly);
}

function tabCardinality(tab: EdgeRelTab): string | null {
  return tabRelSchema(tab)?.relationshipCardinality ?? null;
}

/** Is the CURRENT entity limited to at most one link for this tab? */
function tabCurrentEntityLimited(tab: EdgeRelTab): boolean {
  const c = tabCardinality(tab);
  if (!c) return false;
  return tab.direction === 'out'
    ? c === 'many_to_one' || c === 'one_to_one'
    : c === 'one_to_many' || c === 'one_to_one';
}

/** Total number of existing links for this tab. */
function tabLinkCount(tab: EdgeRelTab): number {
  return tab.direction === 'out'
    ? outboundLinksForTab(tab).length
    : inboundLinksFor(tab.relationshipTypeId).length;
}

/** Should we show "Reassign" instead of "Link"? */
function tabIsReassign(tab: EdgeRelTab): boolean {
  return tabCurrentEntityLimited(tab) && tabLinkCount(tab) > 0;
}

/** Do CANDIDATES need to be filtered (they may already have a conflicting link)? */
function tabCandidateLimited(tab: EdgeRelTab): boolean {
  const c = tabCardinality(tab);
  if (!c) return false;
  return tab.direction === 'in'
    ? c === 'many_to_one' || c === 'one_to_one'
    : c === 'one_to_one';
}

/** Does this tab allow unlimited links from the current entity? → show "+" create+link button. */
function tabAllowsMultiple(tab: EdgeRelTab): boolean {
  const c = tabCardinality(tab);
  if (!c) return true;
  return tab.direction === 'out'
    ? c === 'one_to_many' || c === 'many_to_many'
    : c === 'many_to_one' || c === 'many_to_many';
}

// ── Link / Unlink modal state ────────────────────────────────────────────────
const linkModalOpen = ref(false);
const linkModalTab = ref<EdgeRelTab | null>(null);
const linkIsReassign = ref(false);
const linkCandidates = ref<EntityListItemDto[]>([]);
const linkLoading = ref(false);
const linkError = ref<string | null>(null);

async function openLinkModal(tab: EdgeRelTab) {
  linkModalTab.value = tab;
  linkIsReassign.value = tabIsReassign(tab);
  linkModalOpen.value = true;
  linkError.value = null;
  linkCandidates.value = [];
  linkLoading.value = true;
  try {
    const targetTypeName = tab.otherEntityTypeName;
    const targetType = entityStore.types.find((t) => t.name === targetTypeName);
    if (!targetType) { linkCandidates.value = []; return; }
    const all = await entityApi.list(props.workspaceId, {
      entityTypeId: targetType.id,
      excludeLinkedSourceRelTypeId:
        tabCandidateLimited(tab) && tab.direction === 'in' ? tab.relationshipTypeId : undefined,
      excludeLinkedTargetRelTypeId:
        tabCandidateLimited(tab) && tab.direction === 'out' ? tab.relationshipTypeId : undefined,
    });
    const d = detail.value;
    const linkedIds = new Set(
      (tab.direction === 'out' ? d?.outboundRelationships : d?.inboundRelationships)
        ?.filter((r) => r.relationshipTypeId === tab.relationshipTypeId)
        .map((r) => r.relatedEntityId) ?? [],
    );
    linkCandidates.value = all.filter((e) => !linkedIds.has(e.id));
  } catch (err) {
    linkError.value = normalizeError(err);
  } finally {
    linkLoading.value = false;
  }
}

function previewLinkLabel(item: EntityListItemDto): string {
  const v = item.propertyValues.find(
    (p) => ['name', 'first_name', 'title', 'email'].includes(p.propertyName.toLowerCase()),
  );
  return v ? String(v.value ?? '') : `#${item.id}`;
}

async function confirmLink(candidate: EntityListItemDto) {
  const tab = linkModalTab.value;
  if (!tab || !detail.value) return;
  try {
    if (linkIsReassign.value) {
      const existing = tab.direction === 'out'
        ? outboundLinksForTab(tab)
        : inboundLinksFor(tab.relationshipTypeId);
      for (const link of existing) {
        await entityApi.deleteRelationship(props.workspaceId, link.relationshipId);
      }
    }
    const sourceId = tab.direction === 'out' ? detail.value.id : candidate.id;
    const targetId = tab.direction === 'out' ? candidate.id : detail.value.id;
    await entityApi.createRelationship(props.workspaceId, {
      sourceEntityId: sourceId,
      targetEntityId: targetId,
      relationshipTypeId: tab.relationshipTypeId,
    });
    linkModalOpen.value = false;
    await loadDetail();
    toast.add({ severity: 'success', summary: linkIsReassign.value ? 'Reassigned' : 'Linked', life: 2500 });
  } catch (err) {
    linkError.value = normalizeError(err);
  }
}

async function unlinkRelationship(relationshipId: number) {
  try {
    await entityApi.deleteRelationship(props.workspaceId, relationshipId);
    await loadDetail();
    toast.add({ severity: 'success', summary: 'Unlinked', life: 2500 });
  } catch (err) {
    toast.add({ severity: 'error', summary: 'Error', detail: normalizeError(err), life: 4000 });
  }
}

// ── Create + Link dialog ─────────────────────────────────────────────────────
const createLinkOpen = ref(false);
const createLinkTab = ref<EdgeRelTab | null>(null);
const createLinkTargetType = ref<EntityTypeDto | null>(null);
const createLinkValues = ref<Record<number, FieldValue>>({});
const createLinkOtherRelPick = reactive<Record<number, number | null>>({});
const createLinkOtherRelCandidates = ref<Record<number, EntityListItemDto[]>>({});
const createLinkSubmitting = ref(false);
const createLinkError = ref<string | null>(null);
const createLinkSubmitAttempted = ref(false);

/** Required outgoing relationships of the new entity that the user must fill in (not pre-filled). */
const createLinkOtherRequired = computed(() => {
  const tab = createLinkTab.value;
  const t = createLinkTargetType.value;
  if (!t) return [];
  return t.outgoingRelationships.filter((r) => {
    if (!r.isRequired) return false;
    // For inbound tabs: we pre-fill the link back to the current entity, so skip it here.
    if (tab?.direction === 'in' && r.relationshipTypeId === tab.relationshipTypeId) return false;
    return true;
  });
});

function tabCanCreateLink(tab: EdgeRelTab): boolean {
  const t = entityStore.types.find((ty) => ty.name === tab.otherEntityTypeName);
  return !!t && !isEntityTypeUiLocked(t);
}

function createLinkRelOptions(relTypeId: number) {
  return (createLinkOtherRelCandidates.value[relTypeId] ?? []).map((e) => ({
    label: previewLinkLabel(e),
    value: e.id,
  }));
}

function isCreateLinkPropEmpty(prop: EntityTypePropertyDto): boolean {
  if (prop.dataType === 'Bool') return false;
  return isEmpty(createLinkValues.value[prop.propertyId] ?? null);
}

function createLinkPropError(prop: EntityTypePropertyDto): string | null {
  if (createLinkSubmitAttempted.value && isCreateLinkPropEmpty(prop)) return 'Required.';
  return null;
}

function serializeCreateValue(prop: EntityTypePropertyDto, raw: FieldValue): string | null {
  if (isEmpty(raw)) return null;
  switch (prop.dataType) {
    case 'Date': return raw instanceof Date ? formatDate(raw) : String(raw);
    case 'Bool': return raw ? 'true' : 'false';
    case 'Decimal':
    case 'Int': return Number(raw).toString();
    default: return String(raw).trim();
  }
}

async function openCreateLinkModal(tab: EdgeRelTab) {
  const targetType = entityStore.types.find((t) => t.name === tab.otherEntityTypeName);
  if (!targetType || isEntityTypeUiLocked(targetType)) return;

  createLinkTab.value = tab;
  createLinkTargetType.value = targetType;
  createLinkSubmitAttempted.value = false;
  createLinkError.value = null;
  createLinkSubmitting.value = false;

  const vals: Record<number, FieldValue> = {};
  for (const p of targetType.properties.filter((p) => !p.isReadonly)) {
    vals[p.propertyId] = p.dataType === 'Bool' ? false : null;
  }
  createLinkValues.value = vals;

  for (const k of Object.keys(createLinkOtherRelPick)) {
    delete createLinkOtherRelPick[Number(k)];
  }
  createLinkOtherRelCandidates.value = {};

  const otherRequired = targetType.outgoingRelationships.filter((r) => {
    if (!r.isRequired) return false;
    if (tab.direction === 'in' && r.relationshipTypeId === tab.relationshipTypeId) return false;
    return true;
  });
  for (const r of otherRequired) {
    createLinkOtherRelPick[r.relationshipTypeId] = null;
    try {
      const items = await entityApi.list(props.workspaceId, { entityTypeId: r.targetEntityTypeId, take: 400 });
      createLinkOtherRelCandidates.value = { ...createLinkOtherRelCandidates.value, [r.relationshipTypeId]: items };
    } catch {
      createLinkOtherRelCandidates.value = { ...createLinkOtherRelCandidates.value, [r.relationshipTypeId]: [] };
    }
  }

  createLinkOpen.value = true;
}

async function submitCreateLink() {
  const tab = createLinkTab.value;
  const targetType = createLinkTargetType.value;
  if (!tab || !targetType || !detail.value) return;

  createLinkSubmitAttempted.value = true;

  const writableProps = targetType.properties.filter((p) => !p.isReadonly);
  const hasEmptyRequired = writableProps.some((p) => isCreateLinkPropEmpty(p));
  const hasEmptyRel = createLinkOtherRequired.value.some(
    (r) => createLinkOtherRelPick[r.relationshipTypeId] == null,
  );
  if (hasEmptyRequired || hasEmptyRel) {
    createLinkError.value = 'Fill in all required fields.';
    return;
  }

  createLinkSubmitting.value = true;
  createLinkError.value = null;
  try {
    const properties = writableProps.map((p) => ({
      propertyId: p.propertyId,
      value: serializeCreateValue(p, createLinkValues.value[p.propertyId] ?? null),
    }));

    const links: { relationshipTypeId: number; targetEntityId: number }[] = [];
    // For inbound tabs: pre-fill the link back to the current entity
    if (tab.direction === 'in') {
      links.push({ relationshipTypeId: tab.relationshipTypeId, targetEntityId: detail.value.id });
    }
    for (const r of createLinkOtherRequired.value) {
      const picked = createLinkOtherRelPick[r.relationshipTypeId];
      if (picked != null) links.push({ relationshipTypeId: r.relationshipTypeId, targetEntityId: picked });
    }

    const newEntity = await entityStore.createViaGraph(props.workspaceId, {
      entityTypeId: targetType.id,
      properties,
      ...(links.length > 0 ? { links } : {}),
    });

    // For outbound tabs: the relationship goes current → new, so create it explicitly
    if (tab.direction === 'out') {
      await entityApi.createRelationship(props.workspaceId, {
        sourceEntityId: detail.value.id,
        targetEntityId: newEntity.id,
        relationshipTypeId: tab.relationshipTypeId,
      });
    }

    createLinkOpen.value = false;
    await loadDetail();
    toast.add({ severity: 'success', summary: `${humanize(targetType.name)} created and linked`, life: 3000 });
  } catch (err) {
    createLinkError.value = normalizeError(err);
  } finally {
    createLinkSubmitting.value = false;
  }
}

// ── Right-panel expand state ─────────────────────────────────────────────────
const expandedKeys = ref(new Set<string>());
const expandedCache = ref(new Map<number, EntityDetailDto>());
const expandedLoading = ref(new Set<number>());

function expandKey(tabKey: string, entityId: number): string {
  return `${tabKey}:${entityId}`;
}

function isExpanded(tabKey: string, entityId: number): boolean {
  return expandedKeys.value.has(expandKey(tabKey, entityId));
}

async function toggleExpand(tabKey: string, entityId: number) {
  const k = expandKey(tabKey, entityId);
  if (expandedKeys.value.has(k)) {
    expandedKeys.value.delete(k);
    return;
  }
  expandedKeys.value.add(k);
  if (!expandedCache.value.has(entityId) && !expandedLoading.value.has(entityId)) {
    expandedLoading.value.add(entityId);
    try {
      const d = await entityStore.fetchDetail(props.workspaceId, entityId);
      expandedCache.value.set(entityId, d);
    } catch {
      // show preview only on fetch failure
    } finally {
      expandedLoading.value.delete(entityId);
    }
  }
}

type EdgeRelTab = {
  direction: 'out' | 'in';
  relationshipTypeId: number;
  name: string;
  /** Outbound: target type name. Inbound: source type name (records pointing at this entity). */
  otherEntityTypeName: string;
};

function relTabKey(tab: Pick<EdgeRelTab, 'direction' | 'relationshipTypeId'>): string {
  return tab.direction === 'out'
    ? `out-${tab.relationshipTypeId}`
    : `in-${tab.relationshipTypeId}`;
}

const outboundRelTabs = computed((): EdgeRelTab[] => {
  const schemaRels = typeSchema.value?.outgoingRelationships;
  if (schemaRels?.length) {
    const currentType = detail.value?.entityTypeName ?? null;
    return [...schemaRels]
      .filter((r) => !currentType || r.targetEntityTypeName !== currentType)
      .map((r) => ({
        direction: 'out' as const,
        relationshipTypeId: r.relationshipTypeId,
        name: r.name,
        otherEntityTypeName: r.targetEntityTypeName,
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  const d = detail.value;
  if (!d?.outboundRelationships.length) return [];

  const byId = new Map<number, EdgeRelTab>();
  for (const r of d.outboundRelationships) {
    if (byId.has(r.relationshipTypeId)) continue;
    if (r.relatedEntityTypeName === d.entityTypeName) continue;
    byId.set(r.relationshipTypeId, {
      direction: 'out',
      relationshipTypeId: r.relationshipTypeId,
      name: r.relationshipName,
      otherEntityTypeName: r.relatedEntityTypeName,
    });
  }

  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
});

/**
 * Hide inbound tabs when the current entity type already has an outgoing relationship
 * type to the same peer type. This removes duplicate pair tabs like `deal_contract`
 * on a `contract` page where `contract_deal` is the canonical direction.
 */
const outboundCoveredTypeNames = computed<Set<string>>(() => {
  const schemaOut = typeSchema.value?.outgoingRelationships;
  if (schemaOut?.length) {
    return new Set(schemaOut.map((r) => r.targetEntityTypeName));
  }
  const d = detail.value;
  if (!d?.outboundRelationships.length) return new Set();
  return new Set(d.outboundRelationships.map((r) => r.relatedEntityTypeName));
});

const inboundRelTabs = computed((): EdgeRelTab[] => {
  const schemaRels = typeSchema.value?.incomingRelationships;
  if (schemaRels?.length) {
    const currentType = detail.value?.entityTypeName ?? null;
    const coveredNames = outboundCoveredTypeNames.value;
    return [...schemaRels]
      .filter((r) => !currentType || r.sourceEntityTypeName !== currentType)
      // Hide inbound duplicates when there's an outbound relationship to the same peer type.
      // Example: on `contract`, hide `deal_contract` if `contract_deal` exists (outbound tab will mirror links).
      .filter((r) => !coveredNames.has(r.sourceEntityTypeName))
      .map((r) => ({
        direction: 'in' as const,
        relationshipTypeId: r.relationshipTypeId,
        name: r.name,
        otherEntityTypeName: r.sourceEntityTypeName,
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  const d = detail.value;
  if (!d?.inboundRelationships.length) return [];

  const coveredNames = outboundCoveredTypeNames.value;
  const byId = new Map<number, EdgeRelTab>();
  for (const r of d.inboundRelationships) {
    if (byId.has(r.relationshipTypeId)) continue;
    if (coveredNames.has(r.relatedEntityTypeName)) continue;
    if (r.relatedEntityTypeName === d.entityTypeName) continue;
    byId.set(r.relationshipTypeId, {
      direction: 'in',
      relationshipTypeId: r.relationshipTypeId,
      name: r.relationshipName,
      otherEntityTypeName: r.relatedEntityTypeName,
    });
  }

  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
});

const hasRelationshipTabs = computed(
  () =>
    outboundRelTabs.value.length > 0 || inboundRelTabs.value.length > 0,
);

function outboundLinksFor(relationshipTypeId: number) {
  return (
    detail.value?.outboundRelationships.filter(
      (r) => r.relationshipTypeId === relationshipTypeId,
    ) ?? []
  );
}

function inboundLinksFor(relationshipTypeId: number) {
  return (
    detail.value?.inboundRelationships.filter(
      (r) => r.relationshipTypeId === relationshipTypeId,
    ) ?? []
  );
}

function inverseRelationshipName(name: string): string | null {
  const parts = name.split('_');
  if (parts.length !== 2) return null;
  const [a, b] = parts;
  if (!a || !b) return null;
  return `${b}_${a}`;
}

function outboundLinksForTab(tab: EdgeRelTab) {
  const d = detail.value;
  if (!d) return [];

  const direct =
    d.outboundRelationships.filter((r) => r.relationshipTypeId === tab.relationshipTypeId) ??
    [];

  // If the DB stores the inverse direction (e.g. `deal_contract`) but the schema exposes
  // the canonical outgoing tab (e.g. `contract_deal`), mirror those inbound links here.
  const inverseName = inverseRelationshipName(tab.name);
  const mirrored =
    inverseName
      ? d.inboundRelationships.filter(
          (r) =>
            r.relationshipName === inverseName &&
            r.relatedEntityTypeName === tab.otherEntityTypeName,
        )
      : [];

  if (mirrored.length === 0) return direct;

  const seen = new Set(direct.map((r) => `${r.relationshipTypeId}:${r.relatedEntityId}`));
  const merged = [...direct];
  for (const r of mirrored) {
    const k = `${r.relationshipTypeId}:${r.relatedEntityId}`;
    if (seen.has(k)) continue;
    merged.push(r);
    seen.add(k);
  }
  return merged;
}

function humanize(name: string): string {
  return name.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function formatDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function parseDetailToEditValues(d: EntityDetailDto) {
  const next: Record<number, FieldValue> = {};
  for (const p of d.propertyValues) {
    if (p.isReadonly) continue;
    switch (p.dataType) {
      case 'Bool':
        next[p.propertyId] = Boolean(p.value);
        break;
      case 'Int':
      case 'Decimal':
        next[p.propertyId] =
          p.value === null || p.value === undefined
            ? null
            : Number(p.value);
        break;
      case 'Date':
        next[p.propertyId] =
          typeof p.value === 'string' && p.value
            ? new Date(`${p.value}T12:00:00`)
            : null;
        break;
      default:
        next[p.propertyId] =
          p.value === null || p.value === undefined ? '' : String(p.value);
    }
  }
  editValues.value = next;
}

function isEmpty(v: FieldValue): boolean {
  if (v === null || v === undefined) return true;
  if (typeof v === 'string' && v.trim() === '') return true;
  return false;
}

function serializeValue(
  prop: EntityPropertyValueDto,
  raw: FieldValue,
): string | null {
  if (isEmpty(raw)) return null;
  switch (prop.dataType) {
    case 'Date':
      return raw instanceof Date ? formatDate(raw) : String(raw);
    case 'Bool':
      return raw ? 'true' : 'false';
    case 'Decimal':
    case 'Int':
      return Number(raw).toString();
    case 'String':
    default:
      return String(raw).trim();
  }
}

function formatDisplayValue(p: EntityPropertyValueDto): string {
  if (p.value === null || p.value === undefined) return '—';
  if (typeof p.value === 'boolean') return p.value ? 'Yes' : 'No';
  return String(p.value);
}

function previewLabel(pv: EntityPropertyValueDto[]): string {
  const first = pv[0];
  if (!first) return '';
  return `${humanize(first.propertyName)}: ${formatDisplayValue(first)}`;
}

function goToEntity(typeName: string, id: number) {
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(props.workspaceId) },
    query: { ...route.query, entityType: typeName, id: String(id) },
  });
}

function exitDetail() {
  const q = { ...route.query } as Record<string, unknown>;
  delete q.id;
  delete q.action;
  router.push({
    name: 'workspace-entities',
    params: { workspaceId: String(props.workspaceId) },
    query: q as Record<string, string>,
  });
  emit('close');
}

const isDeal = computed(() => detail.value?.entityTypeName === 'deal');

async function loadDetail() {
  loading.value = true;
  errorMessage.value = null;
  score.value = null;
  scoreError.value = null;
  scoreLoading.value = false;
  try {
    await entityStore.fetchTypes();
    const d = await entityStore.fetchDetail(props.workspaceId, props.entityId);
    detail.value = d;
    parseDetailToEditValues(d);
    editMode.value = false;
  } catch (err) {
    errorMessage.value = normalizeError(err, 'Failed to load entity.').message;
    detail.value = null;
  } finally {
    loading.value = false;
  }

  // Fire-and-forget score fetch for deals so the page paints first.
  if (detail.value && isDeal.value) {
    void loadScore();
  }
}

async function loadScore() {
  const targetEntityId = props.entityId;
  scoreLoading.value = true;
  scoreError.value = null;
  try {
    const results = await mlApi.scoreBatch([targetEntityId]);
    if (props.entityId !== targetEntityId) return;
    score.value = results[0] ?? null;
  } catch (err) {
    if (props.entityId !== targetEntityId) return;
    score.value = null;
    scoreError.value = normalizeError(err, 'Could not load scores.').message;
  } finally {
    if (props.entityId === targetEntityId) {
      scoreLoading.value = false;
    }
  }
}

async function refreshScore() {
  if (!isDeal.value) return;
  await loadScore();
  if (scoreError.value) {
    toast.add({
      severity: 'error',
      summary: 'Score refresh failed',
      detail: scoreError.value,
      life: 4000,
    });
  } else if (score.value && score.value.unavailable_reason === null) {
    toast.add({ severity: 'success', summary: 'Scores refreshed', life: 2000 });
  }
}

function formatScore(value: number | null): string {
  if (value === null || value === undefined) return '—';
  return `${value.toFixed(1)}%`;
}

function cancelEdit() {
  if (detail.value) parseDetailToEditValues(detail.value);
  editMode.value = false;
  fieldErrors.value = {};
}

async function saveEdit() {
  const d = detail.value;
  if (!d) return;
  saving.value = true;
  errorMessage.value = null;
  fieldErrors.value = {};
  const properties = writableProps.value.map((p) => ({
    propertyId: p.propertyId,
    value: serializeValue(p, editValues.value[p.propertyId] ?? null),
  }));
  try {
    const updated = await entityStore.update(
      props.workspaceId,
      props.entityId,
      { properties },
    );
    detail.value = updated;
    parseDetailToEditValues(updated);
    editMode.value = false;
    toast.add({ severity: 'success', summary: 'Saved', life: 2500 });
    emit('updated');
  } catch (err) {
    const normalized = normalizeError(err, 'Failed to save.');
    fieldErrors.value = normalized.fieldErrors;
    errorMessage.value = normalized.message;
  } finally {
    saving.value = false;
  }
}

function fieldError(prop: EntityPropertyValueDto): string | null {
  return (
    firstFieldError(fieldErrors.value, prop.propertyName) ??
    firstFieldError(fieldErrors.value, `properties[${prop.propertyId}]`) ??
    firstFieldError(fieldErrors.value, `properties.${prop.propertyId}`)
  );
}

function requestDeleteEntity() {
  confirm.require({
    message:
      'Delete this entity? It will be hidden from lists; linked records remain in the workspace.',
    header: 'Delete entity',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: async () => {
      try {
        await entityStore.archive(props.workspaceId, props.entityId);
        toast.add({ severity: 'success', summary: 'Entity deleted', life: 2500 });
        exitDetail();
      } catch (err) {
        toast.add({
          severity: 'error',
          summary: 'Delete failed',
          detail: normalizeError(err, 'Could not delete entity.').message,
          life: 5000,
        });
      }
    },
  });
}

watch(
  () => [props.workspaceId, props.entityId] as const,
  () => loadDetail(),
  { immediate: true },
);
</script>

<template>
  <ConfirmDialog />
  <section class="grid grid-cols-1 lg:grid-cols-[1fr_360px] gap-6 items-start">
    <div class="min-w-0">
    <div class="flex items-start justify-between gap-4 mb-6">
      <div class="min-w-0">
        <Button
          text
          icon="pi pi-arrow-left"
          label="Back to list"
          severity="secondary"
          size="small"
          class="!px-1 !mb-1"
          @click="exitDetail"
        />
        <h1 class="text-2xl font-bold text-ink-900">
          <template v-if="detail">
            {{ humanize(detail.entityTypeName) }} · #{{ detail.id }}
          </template>
          <template v-else>Entity</template>
        </h1>
        <p v-if="detail" class="mt-1 text-sm text-ink-500">
          Workspace record details and relationships.
        </p>
      </div>
      <div v-if="detail && (!detail.isArchived || canEditArchived)" class="flex flex-wrap gap-2 shrink-0">
        <Button
          v-if="canEditCurrentEntity && !editMode"
          label="Edit"
          icon="pi pi-pencil"
          severity="secondary"
          outlined
          @click="editMode = true"
        />
        <template v-if="canEditCurrentEntity && editMode">
          <Button
            label="Cancel"
            severity="secondary"
            text
            :disabled="saving"
            @click="cancelEdit"
          />
          <Button
            label="Save"
            icon="pi pi-check"
            :loading="saving"
            :disabled="saving"
            @click="saveEdit"
          />
        </template>
        <Button
          v-if="canDelete && !editMode && !detail?.isArchived && writableProps.length > 0"
          label="Delete"
          icon="pi pi-trash"
          severity="danger"
          outlined
          @click="requestDeleteEntity"
        />
      </div>
    </div>

    <Message
      v-if="errorMessage"
      severity="error"
      :closable="false"
      class="!my-0 mb-4"
    >
      {{ errorMessage }}
    </Message>

    <LoadingSkeleton v-if="loading" variant="detail" :rows="6" label="Loading entity" />

    <template v-else-if="detail">
      <div
        v-if="isDeal"
        class="rounded-xl border border-line bg-white p-6 mb-6"
      >
        <div class="flex items-start justify-between gap-3 mb-4">
          <div>
            <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide">
              Scores
            </h2>
            <p class="mt-1 text-xs text-ink-500">
              Closure and churn likelihood, computed by the ML service from this deal's analysis.
            </p>
          </div>
          <Button
            icon="pi pi-refresh"
            label="Refresh data"
            severity="secondary"
            outlined
            size="small"
            :loading="scoreLoading"
            :disabled="scoreLoading"
            @click="refreshScore"
          />
        </div>

        <div
          v-if="scoreLoading && !score"
          class="grid gap-4 sm:grid-cols-2"
          role="status"
          aria-label="Loading scores"
        >
          <div
            v-for="i in 2"
            :key="i"
            class="rounded-lg border border-line bg-surface/40 p-4 flex flex-col gap-2"
          >
            <Skeleton width="6rem" height="0.75rem" />
            <Skeleton width="4rem" height="1.75rem" />
          </div>
        </div>

        <Message
          v-else-if="scoreError"
          severity="warn"
          :closable="false"
          class="!my-0"
        >
          {{ scoreError }}
        </Message>

        <Message
          v-else-if="score && score.unavailable_reason"
          severity="info"
          :closable="false"
          class="!my-0"
        >
          {{ score.unavailable_reason }}
        </Message>

        <div v-else-if="score" class="grid gap-4 sm:grid-cols-2">
          <div class="rounded-lg border border-line bg-surface/40 p-4">
            <div class="text-xs font-medium text-ink-500 uppercase tracking-wide">
              Closure score
            </div>
            <div class="mt-1 text-2xl font-bold text-brand-700">
              {{ formatScore(score.closure_score) }}
            </div>
          </div>
          <div class="rounded-lg border border-line bg-surface/40 p-4">
            <div class="text-xs font-medium text-ink-500 uppercase tracking-wide">
              Churn score
            </div>
            <div class="mt-1 text-2xl font-bold text-brand-700">
              {{ formatScore(score.churn_score) }}
            </div>
          </div>
        </div>

        <p v-else class="text-sm text-ink-500">
          Scores have not been requested yet.
        </p>
      </div>

      <div class="rounded-xl border border-line bg-white p-6 mb-6">
        <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide mb-4">
          Properties
        </h2>
        <dl class="grid gap-4 sm:grid-cols-2">
          <template v-for="p in detail.propertyValues" :key="p.propertyId">
            <div class="sm:col-span-2 border-b border-line pb-4 last:border-0 last:pb-0">
              <dt class="text-xs font-medium text-ink-500 uppercase tracking-wide">
                {{ humanize(p.propertyName) }}
                <span
                  v-if="p.isReadonly"
                  class="ml-2 normal-case text-ink-400 font-normal"
                >(read-only)</span>
              </dt>
              <dd class="mt-1 text-sm text-ink-900">
                <template v-if="editMode && !p.isReadonly">
                  <Select
                    v-if="p.dataType === 'String' && allowedValuesFor(p.propertyId).length > 0"
                    v-model="editValues[p.propertyId] as string"
                    :options="allowedValuesFor(p.propertyId)"
                    class="w-full"
                    :invalid="!!fieldError(p)"
                  />
                  <InputText
                    v-else-if="p.dataType === 'String'"
                    v-model="editValues[p.propertyId] as string"
                    class="w-full !h-10"
                    :invalid="!!fieldError(p)"
                  />
                  <InputNumber
                    v-else-if="p.dataType === 'Int'"
                    v-model="editValues[p.propertyId] as number"
                    class="w-full"
                    :min-fraction-digits="0"
                    :max-fraction-digits="0"
                    :invalid="!!fieldError(p)"
                  />
                  <InputNumber
                    v-else-if="p.dataType === 'Decimal'"
                    v-model="editValues[p.propertyId] as number"
                    class="w-full"
                    :min-fraction-digits="0"
                    :max-fraction-digits="4"
                    :invalid="!!fieldError(p)"
                  />
                  <DatePicker
                    v-else-if="p.dataType === 'Date'"
                    v-model="editValues[p.propertyId] as Date | null"
                    date-format="yy-mm-dd"
                    show-icon
                    class="w-full"
                    :invalid="!!fieldError(p)"
                  />
                  <ToggleSwitch
                    v-else-if="p.dataType === 'Bool'"
                    v-model="editValues[p.propertyId] as boolean"
                  />
                  <small v-if="fieldError(p)" class="text-xs text-danger block mt-1">
                    {{ fieldError(p) }}
                  </small>
                </template>
                <template v-else>
                  {{ formatDisplayValue(p) }}
                </template>
              </dd>
            </div>
          </template>
        </dl>
      </div>

    </template>
    </div>

    <aside v-if="detail && !loading" class="rounded-xl border border-line bg-white p-5 lg:sticky lg:top-6">
      <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide mb-4">Connections</h2>

      <p v-if="!hasRelationshipTabs" class="text-sm text-ink-500">No connections defined.</p>

      <div
        v-for="tab in [...outboundRelTabs, ...inboundRelTabs]"
        :key="relTabKey(tab)"
        class="mb-5 last:mb-0"
      >
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
            {{ humanize(tab.otherEntityTypeName) }}
          </span>
          <div v-if="canEditCurrentEntity && !currentEntityAllReadonly && !relatedTypeAllReadonly(tab.otherEntityTypeName)" class="flex items-center gap-0.5">
            <Button
              v-if="tabIsReassign(tab)"
              icon="pi pi-sync"
              severity="secondary"
              text
              size="small"
              title="Reassign"
              @click="openLinkModal(tab)"
            />
            <Button
              v-else
              icon="pi pi-link"
              severity="secondary"
              text
              size="small"
              title="Link existing"
              @click="openLinkModal(tab)"
            />
            <Button
              v-if="tabAllowsMultiple(tab) && tabCanCreateLink(tab)"
              icon="pi pi-plus"
              severity="secondary"
              text
              size="small"
              title="Create and link new"
              @click="openCreateLinkModal(tab)"
            />
          </div>
        </div>

        <ul class="space-y-1.5 text-sm">
          <li
            v-for="r in (tab.direction === 'out' ? outboundLinksForTab(tab) : inboundLinksFor(tab.relationshipTypeId))"
            :key="`${relTabKey(tab)}-${r.relatedEntityId}`"
            class="rounded-lg border border-line overflow-hidden"
          >
            <div class="flex items-center gap-1">
              <button
                type="button"
                class="flex-1 text-left px-3 py-2 hover:bg-surface/60 transition-colors"
                @click="toggleExpand(relTabKey(tab), r.relatedEntityId)"
              >
                <span class="flex items-center gap-1.5">
                  <i
                    class="pi text-xs text-ink-400"
                    :class="isExpanded(relTabKey(tab), r.relatedEntityId) ? 'pi-chevron-down' : 'pi-chevron-right'"
                  />
                  <span class="text-brand-700 font-medium">{{ humanize(r.relatedEntityTypeName) }}</span>
                  <span class="font-mono text-xs text-ink-500">#{{ r.relatedEntityId }}</span>
                </span>
                <span v-if="r.previewPropertyValues.length" class="block text-xs text-ink-500 mt-0.5 pl-5">
                  {{ previewLabel(r.previewPropertyValues) }}
                </span>
              </button>
              <Button
                icon="pi pi-arrow-right"
                severity="secondary"
                text
                size="small"
                title="Go to entity"
                @click.stop="goToEntity(r.relatedEntityTypeName, r.relatedEntityId)"
              />
              <Button
                v-if="canEditCurrentEntity && !currentEntityAllReadonly && !(tabIsRequired(tab) && tabLinkCount(tab) <= 1) && !relatedTypeAllReadonly(r.relatedEntityTypeName)"
                icon="pi pi-times"
                severity="danger"
                text
                size="small"
                title="Unlink"
                @click.stop="unlinkRelationship(r.relationshipId)"
              />
            </div>

            <div
              v-if="isExpanded(relTabKey(tab), r.relatedEntityId)"
              class="border-t border-line px-3 py-2 bg-surface/30"
            >
              <div
                v-if="expandedLoading.has(r.relatedEntityId)"
                class="flex items-center gap-2 text-xs text-ink-500 py-1"
              >
                <i class="pi pi-spin pi-spinner text-xs" />
                <span>Loading…</span>
              </div>
              <dl v-else-if="expandedCache.get(r.relatedEntityId)" class="space-y-1.5">
                <div
                  v-for="p in expandedCache.get(r.relatedEntityId)!.propertyValues"
                  :key="p.propertyId"
                  class="flex gap-2 text-xs"
                >
                  <dt class="text-ink-500 min-w-[80px] shrink-0">{{ humanize(p.propertyName) }}</dt>
                  <dd class="text-ink-800 break-all">{{ formatDisplayValue(p) }}</dd>
                </div>
              </dl>
              <p v-else class="text-xs text-ink-500 py-1">Could not load properties.</p>
            </div>
          </li>
        </ul>

        <template v-if="(tab.direction === 'out' ? outboundLinksForTab(tab) : inboundLinksFor(tab.relationshipTypeId)).length === 0">
          <button
            v-if="canEditCurrentEntity && !currentEntityAllReadonly && tabAllowsMultiple(tab) && tabCanCreateLink(tab) && !relatedTypeAllReadonly(tab.otherEntityTypeName)"
            type="button"
            class="mt-1 flex items-center gap-1.5 text-xs text-brand-600 hover:text-brand-800 transition-colors"
            @click="openCreateLinkModal(tab)"
          >
            <i class="pi pi-plus text-xs" />
            <span>Add {{ humanize(tab.otherEntityTypeName) }}</span>
          </button>
          <p v-else class="text-xs text-ink-500 mt-1">
            No {{ humanize(tab.otherEntityTypeName) }} for this {{ humanize(detail.entityTypeName) }}
          </p>
        </template>
      </div>
    </aside>
  </section>

  <Dialog
    v-model:visible="linkModalOpen"
    :header="linkModalTab ? `${linkIsReassign ? 'Reassign' : 'Link'} ${humanize(linkModalTab.otherEntityTypeName)}` : 'Link'"
    modal
    class="w-full max-w-lg"
  >
    <div v-if="linkLoading" class="flex items-center gap-2 py-4 text-sm text-ink-500">
      <i class="pi pi-spin pi-spinner" />
      <span>Loading…</span>
    </div>
    <Message v-else-if="linkError" severity="error" :closable="false" class="!my-0">
      {{ linkError }}
    </Message>
    <p v-else-if="linkCandidates.length === 0" class="text-sm text-ink-500 py-4">
      No linkable records found.
    </p>
    <ul v-else class="space-y-2 py-2 max-h-80 overflow-y-auto text-sm">
      <li v-for="item in linkCandidates" :key="item.id">
        <button
          type="button"
          class="w-full text-left rounded-lg border border-line px-3 py-2 hover:bg-surface/80 transition-colors"
          @click="confirmLink(item)"
        >
          <span class="text-brand-700">{{ humanize(item.entityTypeName) }}</span>
          <span class="font-mono text-xs text-ink-600"> #{{ item.id }}</span>
          <span class="block text-xs text-ink-500 mt-0.5">{{ previewLinkLabel(item) }}</span>
        </button>
      </li>
    </ul>
  </Dialog>

  <Dialog
    v-model:visible="createLinkOpen"
    :header="createLinkTargetType ? `New ${humanize(createLinkTargetType.name)}` : 'Create & link'"
    modal
    class="w-full max-w-lg"
  >
    <div v-if="createLinkTargetType" class="flex flex-col gap-4 py-2">
      <template v-for="r in createLinkOtherRequired" :key="r.relationshipTypeId">
        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-medium text-ink-600">
            {{ humanize(r.name) }} <span class="text-danger">*</span>
          </label>
          <Select
            v-model="createLinkOtherRelPick[r.relationshipTypeId]"
            :options="createLinkRelOptions(r.relationshipTypeId)"
            option-label="label"
            option-value="value"
            :placeholder="`Choose ${humanize(r.targetEntityTypeName)}`"
            class="w-full"
            filter
          />
        </div>
      </template>

      <template v-for="p in createLinkTargetType.properties.filter(p => !p.isReadonly)" :key="p.propertyId">
        <div class="flex flex-col gap-1.5">
          <label :for="`cl-${p.propertyId}`" class="text-xs font-medium text-ink-600">
            {{ humanize(p.name) }}
            <span v-if="p.dataType !== 'Bool'" class="text-danger">*</span>
          </label>
          <Select
            v-if="p.dataType === 'String' && p.allowedValues?.length > 0"
            :id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as string"
            :options="p.allowedValues"
            placeholder="Select..."
            class="w-full"
            :invalid="!!createLinkPropError(p)"
          />
          <InputText
            v-else-if="p.dataType === 'String'"
            :id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as string"
            class="w-full !h-10"
            :invalid="!!createLinkPropError(p)"
          />
          <InputNumber
            v-else-if="p.dataType === 'Int'"
            :input-id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as number"
            :max-fraction-digits="0"
            class="w-full"
            :invalid="!!createLinkPropError(p)"
          />
          <InputNumber
            v-else-if="p.dataType === 'Decimal'"
            :input-id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as number"
            :min-fraction-digits="0"
            :max-fraction-digits="4"
            class="w-full"
            :invalid="!!createLinkPropError(p)"
          />
          <DatePicker
            v-else-if="p.dataType === 'Date'"
            :input-id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as Date | null"
            date-format="yy-mm-dd"
            show-icon
            class="w-full"
            :invalid="!!createLinkPropError(p)"
          />
          <ToggleSwitch
            v-else-if="p.dataType === 'Bool'"
            :input-id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as boolean"
          />
          <small v-if="createLinkPropError(p)" class="text-xs text-danger">{{ createLinkPropError(p) }}</small>
        </div>
      </template>

      <Message v-if="createLinkError" severity="error" :closable="false" class="!my-0">
        {{ createLinkError }}
      </Message>

      <div class="flex justify-end gap-2 pt-2">
        <Button label="Cancel" severity="secondary" text type="button" @click="createLinkOpen = false" />
        <Button
          label="Create & link"
          icon="pi pi-check"
          :loading="createLinkSubmitting"
          :disabled="createLinkSubmitting"
          @click="submitCreateLink"
        />
      </div>
    </div>
  </Dialog>
</template>
