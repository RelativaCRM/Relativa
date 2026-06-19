<script setup lang="ts">
import { ref, computed, watch, reactive, nextTick, toRef, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';
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
  ReassignEntityRelationshipRequest,
} from '@/api/entities';
import { entityApi } from '@/api/entities';
import { isEntityTypeUiLocked } from '@/utils/entityTypes';
import { getPropertyFormatErrorKey } from '@/utils/propertyValidation';
import { mlApi, type DealScoreDto } from '@/api/ml';
import LoadingSkeleton from '@/components/feedback/LoadingSkeleton.vue';
import { useEntityRelationshipsHub } from '@/composables/useEntityRelationshipsHub';

const props = defineProps<{
  workspaceId: number;
  entityId: number;
  initialEditMode?: boolean;
}>();

const emit = defineEmits<{
  close: [];
  updated: [];
}>();

const { t } = useI18n();
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
const initialEditModeConsumed = ref(false);
const fieldErrors = ref<FieldErrors>({});
const editSubmitAttempted = ref(false);
const touchedEditProps = ref(new Set<number>());

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

const typeSchema = computed(() => {
  const d = detail.value;
  if (!d) return null;
  return entityStore.types.find((t) => t.id === d.entityTypeId) ?? null;
});

const displayProperties = computed<EntityPropertyValueDto[]>(() => {
  const schema = typeSchema.value;
  const stored = detail.value?.propertyValues ?? [];
  if (!schema) return stored;

  const storedByPropertyId = new Map(stored.map((p) => [p.propertyId, p]));

  return schema.properties.map(
    (sp) =>
      storedByPropertyId.get(sp.propertyId) ?? {
        propertyId: sp.propertyId,
        propertyName: sp.name,
        displayName: sp.displayName,
        dataType: sp.dataType,
        value: null,
        isReadonly: sp.isReadonly,
      },
  );
});

const writableProps = computed(() => displayProperties.value.filter((p) => !p.isReadonly));

const currentEntityAllReadonly = computed(
  () => displayProperties.value.length > 0 && writableProps.value.length === 0,
);

function allowedValuesFor(propertyId: number) {
  return typeSchema.value?.properties.find((p) => p.propertyId === propertyId)?.allowedValues ?? [];
}


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

function tabCurrentEntityLimited(tab: EdgeRelTab): boolean {
  const c = tabCardinality(tab);
  if (!c) return false;
  return tab.direction === 'out'
    ? c === 'many_to_one' || c === 'one_to_one'
    : c === 'one_to_many' || c === 'one_to_one';
}

function tabLinkCount(tab: EdgeRelTab): number {
  return tab.direction === 'out'
    ? outboundLinksForTab(tab).length
    : inboundLinksFor(tab.relationshipTypeId).length;
}

function tabIsReassign(tab: EdgeRelTab): boolean {
  return tabCurrentEntityLimited(tab) && tabLinkCount(tab) > 0;
}

function tabCandidateLimited(tab: EdgeRelTab): boolean {
  const c = tabCardinality(tab);
  if (!c) return false;
  return tab.direction === 'in'
    ? c === 'many_to_one' || c === 'one_to_one'
    : c === 'one_to_one';
}

function tabAllowsMultiple(tab: EdgeRelTab): boolean {
  const c = tabCardinality(tab);
  if (!c) return true;
  return tab.direction === 'out'
    ? c === 'one_to_many' || c === 'many_to_many'
    : c === 'many_to_one' || c === 'many_to_many';
}

const linkModalOpen = ref(false);
const linkModalTab = ref<EdgeRelTab | null>(null);
const linkIsReassign = ref(false);
const linkCandidates = ref<EntityListItemDto[]>([]);
const linkLoading = ref(false);
const linkError = ref<string | null>(null);
const linkSearch = ref('');
const linkSearchInputRef = ref<{ $el?: HTMLInputElement } | null>(null);

const SEARCHABLE_PROPS = new Set(['name', 'first_name', 'last_name', 'title', 'email']);

const filteredLinkCandidates = computed<EntityListItemDto[]>(() => {
  const query = linkSearch.value.trim().toLowerCase();
  if (!query) return linkCandidates.value;
  return linkCandidates.value.filter((item) => {
    if (String(item.id).includes(query)) return true;
    return item.propertyValues.some((p) =>
      SEARCHABLE_PROPS.has(p.propertyName.toLowerCase())
      && typeof p.value === 'string'
      && p.value.toLowerCase().includes(query),
    );
  });
});

watch(
  () => [linkModalOpen.value, linkLoading.value, linkCandidates.value.length] as const,
  async ([open, loading, count]) => {
    if (!open || loading || count === 0) return;
    await nextTick();
    linkSearchInputRef.value?.$el?.focus();
  },
);

async function openLinkModal(tab: EdgeRelTab) {
  linkModalTab.value = tab;
  linkIsReassign.value = tabIsReassign(tab);
  linkModalOpen.value = true;
  linkError.value = null;
  linkCandidates.value = [];
  linkSearch.value = '';
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
    linkError.value = normalizeError(err, t('entityRead.loadCandidatesError')).message;
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

function findStringProp(item: EntityListItemDto, name: string): string | null {
  const p = item.propertyValues.find((pv) => pv.propertyName.toLowerCase() === name);
  if (!p || typeof p.value !== 'string') return null;
  const s = p.value.trim();
  return s.length > 0 ? s : null;
}

function candidatePrimary(item: EntityListItemDto): string {
  const first = findStringProp(item, 'first_name');
  const last = findStringProp(item, 'last_name');
  if (first || last) return [first, last].filter(Boolean).join(' ');
  return findStringProp(item, 'name')
    ?? findStringProp(item, 'title')
    ?? `#${item.id}`;
}

function candidateSecondary(item: EntityListItemDto): string | null {
  return findStringProp(item, 'email');
}

async function confirmLink(candidate: EntityListItemDto) {
  const tab = linkModalTab.value;
  if (!tab || !detail.value) return;
  try {
    if (linkIsReassign.value) {
      const existing = tab.direction === 'out'
        ? outboundLinksForTab(tab)
        : inboundLinksFor(tab.relationshipTypeId);
      const link = existing[0];
      if (!link) {
        linkError.value = t('entityRead.noExistingLink');
        return;
      }
      const body: ReassignEntityRelationshipRequest =
        tab.direction === 'out'
          ? { newTargetEntityId: candidate.id }
          : { newSourceEntityId: candidate.id };
      await entityApi.reassignRelationship(props.workspaceId, link.relationshipId, body);
    } else {
      const sourceId = tab.direction === 'out' ? detail.value.id : candidate.id;
      const targetId = tab.direction === 'out' ? candidate.id : detail.value.id;
      await entityApi.createRelationship(props.workspaceId, {
        sourceEntityId: sourceId,
        targetEntityId: targetId,
        relationshipTypeId: tab.relationshipTypeId,
      });
    }
    linkModalOpen.value = false;
    await loadDetail();
    toast.add({ severity: 'success', summary: linkIsReassign.value ? t('entityRead.reassigned') : t('entityRead.linked'), life: 2500 });
  } catch (err) {
    linkError.value = normalizeError(err, t('entityRead.linkError')).message;
  }
}

async function unlinkRelationship(relationshipId: number) {
  try {
    await entityApi.deleteRelationship(props.workspaceId, relationshipId);
    await loadDetail();
    toast.add({ severity: 'success', summary: t('entityRead.unlinked'), life: 2500 });
  } catch (err) {
    toast.add({ severity: 'error', summary: t('settings.errorSummary'), detail: normalizeError(err).message, life: 4000 });
  }
}

const createLinkOpen = ref(false);
const createLinkTab = ref<EdgeRelTab | null>(null);
const createLinkTargetType = ref<EntityTypeDto | null>(null);
const createLinkValues = ref<Record<number, FieldValue>>({});
const createLinkOtherRelPick = reactive<Record<number, number | null>>({});
const createLinkOtherRelCandidates = ref<Record<number, EntityListItemDto[]>>({});
const createLinkSubmitting = ref(false);
const createLinkError = ref<string | null>(null);
const createLinkSubmitAttempted = ref(false);
const touchedCreateLinkProps = ref(new Set<number>());

const createLinkOtherRequired = computed(() => {
  const tab = createLinkTab.value;
  const t = createLinkTargetType.value;
  if (!t) return [];
  return t.outgoingRelationships.filter((r) => {
    if (!r.isRequired) return false;
    if (tab?.direction === 'in' && r.relationshipTypeId === tab.relationshipTypeId) return false;
    if (detail.value && r.targetEntityTypeId === detail.value.entityTypeId) return false;
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
  const shouldValidate = createLinkSubmitAttempted.value || touchedCreateLinkProps.value.has(prop.propertyId);
  if (shouldValidate && prop.isRequired && isCreateLinkPropEmpty(prop)) return t('entityRead.required');
  if (shouldValidate && prop.dataType === 'String') {
    const raw = createLinkValues.value[prop.propertyId];
    const key = getPropertyFormatErrorKey(prop.name, raw != null ? String(raw) : null);
    if (key) return t(key);
  }
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
  touchedCreateLinkProps.value = new Set();

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
    if (detail.value && r.targetEntityTypeId === detail.value.entityTypeId) return false;
    return true;
  });
  for (const r of otherRequired) {
    createLinkOtherRelPick[r.relationshipTypeId] = null;
  }

  const candidateLists = await Promise.all(
    otherRequired.map(r =>
      entityApi.list(props.workspaceId, { entityTypeId: r.targetEntityTypeId, take: 400 }).catch(() => [] as EntityListItemDto[])
    )
  );
  const candidates: Record<number, EntityListItemDto[]> = {};
  for (let i = 0; i < otherRequired.length; i++) {
    candidates[otherRequired[i].relationshipTypeId] = candidateLists[i];
  }
  createLinkOtherRelCandidates.value = candidates;

  createLinkOpen.value = true;
}

async function submitCreateLink() {
  const tab = createLinkTab.value;
  const targetType = createLinkTargetType.value;
  if (!tab || !targetType || !detail.value) return;

  createLinkSubmitAttempted.value = true;

  const writableProps = targetType.properties.filter((p) => !p.isReadonly);
  touchedCreateLinkProps.value = new Set(writableProps.map((p) => p.propertyId));
  const hasEmptyRequired = writableProps.some((p) => p.isRequired && isCreateLinkPropEmpty(p));
  const hasFormatError = writableProps.some((p) => !!createLinkPropError(p));
  const hasEmptyRel = createLinkOtherRequired.value.some(
    (r) => createLinkOtherRelPick[r.relationshipTypeId] == null,
  );
  if (hasEmptyRequired || hasFormatError || hasEmptyRel) {
    createLinkError.value = t('entityForm.fillRequired');
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
    if (tab.direction === 'in') {
      links.push({ relationshipTypeId: tab.relationshipTypeId, targetEntityId: detail.value.id });
    }
    for (const r of (createLinkTargetType.value?.outgoingRelationships ?? []).filter(
      (r) => r.isRequired && r.targetEntityTypeId === detail.value!.entityTypeId && r.relationshipTypeId !== tab.relationshipTypeId,
    )) {
      links.push({ relationshipTypeId: r.relationshipTypeId, targetEntityId: detail.value.id });
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

    if (tab.direction === 'out') {
      await entityApi.createRelationship(props.workspaceId, {
        sourceEntityId: detail.value.id,
        targetEntityId: newEntity.id,
        relationshipTypeId: tab.relationshipTypeId,
      });
    }

    createLinkOpen.value = false;
    await loadDetail();
    toast.add({ severity: 'success', summary: t('entityRead.createdAndLinked', { type: targetType.displayName }), life: 3000 });
  } catch (err) {
    createLinkError.value = normalizeError(err, t('entityRead.createLinkError')).message;
  } finally {
    createLinkSubmitting.value = false;
  }
}

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
    } finally {
      expandedLoading.value.delete(entityId);
    }
  }
}

type EdgeRelTab = {
  direction: 'out' | 'in';
  relationshipTypeId: number;
  name: string;
  displayName: string;
  otherEntityTypeName: string;
  otherEntityTypeDisplayName: string;
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
        displayName: r.displayName,
        otherEntityTypeName: r.targetEntityTypeName,
        otherEntityTypeDisplayName: r.targetEntityTypeDisplayName,
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
      displayName: r.relationshipDisplayName,
      otherEntityTypeName: r.relatedEntityTypeName,
      otherEntityTypeDisplayName: r.relatedEntityTypeDisplayName,
    });
  }

  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
});

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
      .filter((r) => !coveredNames.has(r.sourceEntityTypeName))
      .map((r) => ({
        direction: 'in' as const,
        relationshipTypeId: r.relationshipTypeId,
        name: r.name,
        displayName: r.displayName,
        otherEntityTypeName: r.sourceEntityTypeName,
        otherEntityTypeDisplayName: r.sourceEntityTypeDisplayName,
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
      displayName: r.relationshipDisplayName,
      otherEntityTypeName: r.relatedEntityTypeName,
      otherEntityTypeDisplayName: r.relatedEntityTypeDisplayName,
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

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function formatDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function parseDetailToEditValues(d: EntityDetailDto) {
  const schema = typeSchema.value;
  const next: Record<number, FieldValue> = {};

  const allProps = schema
    ? schema.properties.filter((p) => !p.isReadonly)
    : d.propertyValues.filter((p) => !p.isReadonly);

  const storedByPropertyId = new Map(d.propertyValues.map((p) => [p.propertyId, p]));

  for (const sp of allProps) {
    const value = storedByPropertyId.get(sp.propertyId)?.value ?? null;
    switch (sp.dataType) {
      case 'Bool':
        next[sp.propertyId] = Boolean(value);
        break;
      case 'Int':
      case 'Decimal':
        next[sp.propertyId] = value === null || value === undefined ? null : Number(value);
        break;
      case 'Date':
        next[sp.propertyId] =
          typeof value === 'string' && value ? new Date(`${value}T12:00:00`) : null;
        break;
      default:
        next[sp.propertyId] = value === null || value === undefined ? '' : String(value);
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
  if (typeof p.value === 'boolean') return p.value ? t('entityRead.yes') : t('entityRead.no');
  return String(p.value);
}

function previewLabel(pv: EntityPropertyValueDto[]): string {
  const first = pv[0];
  if (!first) return '';
  return `${first.displayName}: ${formatDisplayValue(first)}`;
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
    const d = await entityStore.fetchDetail(props.workspaceId, props.entityId);
    detail.value = d;
    parseDetailToEditValues(d);
    if (!initialEditModeConsumed.value) {
      editMode.value = props.initialEditMode ?? false;
      initialEditModeConsumed.value = true;
    } else {
      editMode.value = false;
    }
  } catch (err) {
    errorMessage.value = normalizeError(err, t('entityRead.loadError')).message;
    detail.value = null;
  } finally {
    loading.value = false;
  }

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
    scoreError.value = normalizeError(err, t('entityRead.loadScoresError')).message;
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
      summary: t('entityRead.scoreRefreshFailed'),
      detail: scoreError.value,
      life: 4000,
    });
  } else if (score.value && score.value.unavailable_reason === null) {
    toast.add({ severity: 'success', summary: t('entityRead.scoresRefreshed'), life: 2000 });
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
  editSubmitAttempted.value = false;
  touchedEditProps.value = new Set();
}

async function saveEdit() {
  const d = detail.value;
  if (!d) return;
  editSubmitAttempted.value = true;
  touchedEditProps.value = new Set(writableProps.value.map((p) => p.propertyId));
  const hasClientError = writableProps.value.some((p) => !!editFieldError(p));
  if (hasClientError) return;
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
    toast.add({ severity: 'success', summary: t('entityRead.saved'), life: 2500 });
    emit('updated');
  } catch (err) {
    const normalized = normalizeError(err, t('entityRead.saveError'));
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

function editFieldError(prop: EntityPropertyValueDto): string | null {
  const shouldValidate = editSubmitAttempted.value || touchedEditProps.value.has(prop.propertyId);
  if (shouldValidate) {
    const isRequired = typeSchema.value?.properties.find((p) => p.propertyId === prop.propertyId)?.isRequired ?? false;
    if (isRequired && isEmpty(editValues.value[prop.propertyId] ?? null)) {
      return t('entityForm.fieldRequired');
    }
    if (prop.dataType === 'String') {
      const raw = editValues.value[prop.propertyId];
      const key = getPropertyFormatErrorKey(prop.propertyName, raw != null ? String(raw) : null);
      if (key) return t(key);
    }
  }
  return fieldError(prop);
}

function requestDeleteEntity() {
  confirm.require({
    message: t('entityRead.confirmDelete'),
    header: t('entityRead.deleteHeader'),
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: t('common.cancel'), severity: 'secondary', text: true },
    acceptProps: { label: t('entityRead.delete'), severity: 'danger' },
    accept: async () => {
      try {
        await entityStore.archive(props.workspaceId, props.entityId);
        toast.add({ severity: 'success', summary: t('entityRead.entityDeleted'), life: 2500 });
        exitDetail();
      } catch (err) {
        toast.add({
          severity: 'error',
          summary: t('entityRead.deleteFailed'),
          detail: normalizeError(err, t('entityRead.deleteError')).message,
          life: 5000,
        });
      }
    },
  });
}

const { start: startHub, stop: stopHub } = useEntityRelationshipsHub(
  toRef(props, 'workspaceId'),
  toRef(props, 'entityId'),
  () => loadDetail(),
);

watch(
  () => [props.workspaceId, props.entityId] as const,
  async (_: readonly [number, number], old: readonly [number, number] | undefined) => {
    if (old) await stopHub();
    loadDetail();
    startHub();
  },
  { immediate: true },
);

onUnmounted(() => stopHub());
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
          :label="t('entityRead.back')"
          severity="secondary"
          size="small"
          class="!px-1 !mb-1"
          @click="exitDetail"
        />
        <h1 class="text-2xl font-bold text-ink-900">
          <template v-if="detail">
            {{ detail.entityTypeDisplayName }} · #{{ detail.id }}
          </template>
          <template v-else>{{ t('entityRead.entityFallback') }}</template>
        </h1>
        <p v-if="detail" class="mt-1 text-sm text-ink-500">
          {{ t('entityRead.subtitle') }}
        </p>
      </div>
      <div v-if="detail && (!detail.isArchived || canEditArchived)" class="flex flex-wrap gap-2 shrink-0">
        <Button
          v-if="canEditCurrentEntity && !editMode"
          :label="t('entityRead.edit')"
          icon="pi pi-pencil"
          severity="secondary"
          outlined
          @click="editMode = true"
        />
        <template v-if="canEditCurrentEntity && editMode">
          <Button
            :label="t('common.cancel')"
            outlined
            :disabled="saving"
            class="!h-10 !px-4 !bg-white !border !border-brand-600 !text-brand-600 hover:!bg-brand-50"
            @click="cancelEdit"
          />
          <Button
            :label="t('entityRead.save')"
            icon="pi pi-check"
            :loading="saving"
            :disabled="saving"
            class="!h-10 !px-4 !bg-brand-600 !border !border-brand-600 !text-white hover:!bg-brand-700 hover:!border-brand-700"
            @click="saveEdit"
          />
        </template>
        <Button
          v-if="canDelete && !editMode && !detail?.isArchived && writableProps.length > 0"
          :label="t('entityRead.delete')"
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

    <LoadingSkeleton v-if="loading" variant="detail" :rows="6" :label="t('common.loading')" />

    <template v-else-if="detail">
      <div
        v-if="isDeal"
        class="rounded-xl border border-line bg-white p-6 mb-6"
      >
        <div class="flex items-start justify-between gap-3 mb-4">
          <div>
            <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide">
              {{ t('entityRead.scores') }}
            </h2>
            <p class="mt-1 text-xs text-ink-500">
              {{ t('entityRead.scoresHint') }}
            </p>
          </div>
          <Button
            icon="pi pi-refresh"
            :label="t('entityRead.refreshData')"
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
          :aria-label="t('entityRead.loadingScores')"
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
              {{ t('entityRead.closureScore') }}
            </div>
            <div class="mt-1 text-2xl font-bold text-brand-700">
              {{ formatScore(score.closure_score) }}
            </div>
          </div>
          <div class="rounded-lg border border-line bg-surface/40 p-4">
            <div class="text-xs font-medium text-ink-500 uppercase tracking-wide">
              {{ t('entityRead.churnScore') }}
            </div>
            <div class="mt-1 text-2xl font-bold text-brand-700">
              {{ formatScore(score.churn_score) }}
            </div>
          </div>
        </div>

        <p v-else class="text-sm text-ink-500">
          {{ t('entityRead.scoresNotRequested') }}
        </p>
      </div>

      <div class="rounded-xl border border-line bg-white p-6 mb-6">
        <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide mb-4">
          {{ t('entityRead.properties') }}
        </h2>
        <dl class="grid gap-4 sm:grid-cols-2">
          <template v-for="p in displayProperties" :key="p.propertyId">
            <div class="sm:col-span-2 border-b border-line pb-4 last:border-0 last:pb-0">
              <dt class="text-xs font-medium text-ink-500 uppercase tracking-wide">
                {{ p.displayName }}
                <span
                  v-if="p.isReadonly"
                  class="ml-2 normal-case text-ink-400 font-normal"
                >{{ t('entityRead.readOnly') }}</span>
              </dt>
              <dd class="mt-1 text-sm text-ink-900">
                <template v-if="editMode && !p.isReadonly">
                  <Select
                    v-if="p.dataType === 'String' && allowedValuesFor(p.propertyId).length > 0"
                    v-model="editValues[p.propertyId] as string"
                    :options="allowedValuesFor(p.propertyId)"
                    option-label="displayName"
                    option-value="value"
                    class="w-full !h-10"
                    :invalid="!!editFieldError(p)"
                    @blur="touchedEditProps.add(p.propertyId)"
                  />
                  <InputText
                    v-else-if="p.dataType === 'String'"
                    v-model="editValues[p.propertyId] as string"
                    class="w-full !h-10"
                    :invalid="!!editFieldError(p)"
                    @blur="touchedEditProps.add(p.propertyId)"
                  />
                  <InputNumber
                    v-else-if="p.dataType === 'Int'"
                    v-model="editValues[p.propertyId] as number"
                    class="w-full"
                    :input-class="'!h-10 w-full'"
                    :min-fraction-digits="0"
                    :max-fraction-digits="0"
                    :invalid="!!editFieldError(p)"
                    :input-props="{ onBlur: () => touchedEditProps.add(p.propertyId) }"
                  />
                  <InputNumber
                    v-else-if="p.dataType === 'Decimal'"
                    v-model="editValues[p.propertyId] as number"
                    class="w-full"
                    :input-class="'!h-10 w-full'"
                    :min-fraction-digits="0"
                    :max-fraction-digits="4"
                    :invalid="!!editFieldError(p)"
                    :input-props="{ onBlur: () => touchedEditProps.add(p.propertyId) }"
                  />
                  <DatePicker
                    v-else-if="p.dataType === 'Date'"
                    v-model="editValues[p.propertyId] as Date | null"
                    date-format="yy-mm-dd"
                    show-icon
                    class="w-full"
                    :input-class="'!h-10 w-full'"
                    :invalid="!!editFieldError(p)"
                    @blur="touchedEditProps.add(p.propertyId)"
                  />
                  <ToggleSwitch
                    v-else-if="p.dataType === 'Bool'"
                    v-model="editValues[p.propertyId] as boolean"
                  />
                  <small v-if="editFieldError(p)" class="text-xs text-danger block mt-1">
                    {{ editFieldError(p) }}
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
      <h2 class="text-sm font-semibold text-ink-700 uppercase tracking-wide mb-4">{{ t('entityRead.connections') }}</h2>

      <p v-if="!hasRelationshipTabs" class="text-sm text-ink-500">{{ t('entityRead.noConnections') }}</p>

      <div
        v-for="tab in [...outboundRelTabs, ...inboundRelTabs]"
        :key="relTabKey(tab)"
        class="mb-5 last:mb-0"
      >
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-semibold text-ink-600 uppercase tracking-wide">
            {{ tab.otherEntityTypeDisplayName }}
          </span>
          <div v-if="canEditCurrentEntity && !currentEntityAllReadonly && !relatedTypeAllReadonly(tab.otherEntityTypeName)" class="flex items-center gap-0.5">
            <Button
              v-if="tabIsReassign(tab)"
              icon="pi pi-sync"
              severity="secondary"
              text
              size="small"
              :title="t('entityRead.reassign')"
              @click="openLinkModal(tab)"
            />
            <Button
              v-else
              icon="pi pi-link"
              severity="secondary"
              text
              size="small"
              :title="t('entityRead.linkExisting')"
              @click="openLinkModal(tab)"
            />
            <Button
              v-if="tabAllowsMultiple(tab) && tabCanCreateLink(tab)"
              icon="pi pi-plus"
              severity="secondary"
              text
              size="small"
              :title="t('entityRead.createAndLinkNew')"
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
                  <span class="text-brand-700 font-medium">{{ r.relatedEntityTypeDisplayName }}</span>
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
                :title="t('entityRead.goToEntity')"
                @click.stop="goToEntity(r.relatedEntityTypeName, r.relatedEntityId)"
              />
              <Button
                v-if="canEditCurrentEntity && !currentEntityAllReadonly && !(tabIsRequired(tab) && tabLinkCount(tab) <= 1) && !relatedTypeAllReadonly(r.relatedEntityTypeName)"
                icon="pi pi-times"
                severity="danger"
                text
                size="small"
                :title="t('entityRead.unlink')"
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
                <span>{{ t('entityRead.loading') }}</span>
              </div>
              <dl v-else-if="expandedCache.get(r.relatedEntityId)" class="space-y-1.5">
                <div
                  v-for="p in expandedCache.get(r.relatedEntityId)!.propertyValues"
                  :key="p.propertyId"
                  class="flex gap-2 text-xs"
                >
                  <dt class="text-ink-500 min-w-[80px] shrink-0">{{ p.displayName }}</dt>
                  <dd class="text-ink-800 break-all">{{ formatDisplayValue(p) }}</dd>
                </div>
              </dl>
              <p v-else class="text-xs text-ink-500 py-1">{{ t('entityRead.couldNotLoadProperties') }}</p>
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
            <span>{{ t('entityRead.addOther', { type: tab.otherEntityTypeDisplayName }) }}</span>
          </button>
          <p v-else class="text-xs text-ink-500 mt-1">
            {{ t('entityRead.noOtherForThis', { type: tab.otherEntityTypeDisplayName, parent: detail.entityTypeDisplayName }) }}
          </p>
        </template>
      </div>
    </aside>
  </section>

  <Dialog
    v-model:visible="linkModalOpen"
    :header="linkModalTab ? t('entityRead.linkTitle', { action: linkIsReassign ? t('entityRead.reassign') : t('entityRead.link'), type: linkModalTab.otherEntityTypeDisplayName }) : t('entityRead.linkFallback')"
    modal
    class="w-full max-w-lg"
  >
    <div v-if="linkLoading" class="flex items-center gap-2 py-4 text-sm text-ink-500">
      <i class="pi pi-spin pi-spinner" />
      <span>{{ t('entityRead.loading') }}</span>
    </div>
    <Message v-else-if="linkError" severity="error" :closable="false" class="!my-0">
      {{ linkError }}
    </Message>
    <p v-else-if="linkCandidates.length === 0" class="text-sm text-ink-500 py-4">
      {{ t('entityRead.noLinkable') }}
    </p>
    <template v-else>
      <div class="relative mb-2">
        <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-sm text-ink-400" />
        <InputText
          ref="linkSearchInputRef"
          v-model="linkSearch"
          :placeholder="linkModalTab ? t('entityRead.searchByPlaceholder', { type: linkModalTab.otherEntityTypeDisplayName }) : t('entityRead.searchGenericPlaceholder')"
          class="w-full !h-10 !pl-9"
        />
      </div>
      <p class="text-xs text-ink-500 mb-2">
        <template v-if="linkSearch.trim()">
          {{ t('entityRead.showingOf', { shown: filteredLinkCandidates.length, total: linkCandidates.length }) }}
        </template>
        <template v-else>
          {{ t('entityRead.recordCount', linkCandidates.length, { named: { n: linkCandidates.length } }) }}
        </template>
      </p>
      <p v-if="filteredLinkCandidates.length === 0" class="text-sm text-ink-500 py-4">
        {{ t('entityRead.noSearchMatch') }}
      </p>
      <ul v-else class="space-y-2 py-2 max-h-80 overflow-y-auto text-sm">
        <li v-for="item in filteredLinkCandidates" :key="item.id">
          <button
            type="button"
            class="w-full text-left rounded-lg border border-line px-3 py-2 hover:bg-surface/80 transition-colors"
            @click="confirmLink(item)"
          >
            <span class="flex items-baseline gap-2">
              <span class="text-brand-700">{{ item.entityTypeDisplayName }}</span>
              <span class="font-mono text-xs text-ink-600">#{{ item.id }}</span>
            </span>
            <span class="block text-sm text-ink-900 mt-0.5">{{ candidatePrimary(item) }}</span>
            <span v-if="candidateSecondary(item)" class="block text-xs text-ink-500">
              {{ candidateSecondary(item) }}
            </span>
          </button>
        </li>
      </ul>
    </template>
  </Dialog>

  <Dialog
    v-model:visible="createLinkOpen"
    :header="createLinkTargetType ? t('entityForm.nestedTitle', { type: createLinkTargetType.displayName }) : t('entityForm.createAndLink')"
    modal
    class="w-full max-w-lg"
  >
    <div v-if="createLinkTargetType" class="flex flex-col gap-4 py-2">
      <template v-for="r in createLinkOtherRequired" :key="r.relationshipTypeId">
        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-medium text-ink-600">
            {{ r.displayName }} <span class="text-danger">*</span>
          </label>
          <Select
            v-model="createLinkOtherRelPick[r.relationshipTypeId]"
            :options="createLinkRelOptions(r.relationshipTypeId)"
            option-label="label"
            option-value="value"
            :placeholder="t('entityForm.choose', { target: r.targetEntityTypeDisplayName })"
            class="w-full !h-10"
            filter
          />
        </div>
      </template>

      <template v-for="p in createLinkTargetType.properties.filter(p => !p.isReadonly)" :key="p.propertyId">
        <div class="flex flex-col gap-1.5">
          <label :for="`cl-${p.propertyId}`" class="text-xs font-medium text-ink-600">
            {{ p.displayName }}
            <span v-if="p.isRequired && p.dataType !== 'Bool'" class="text-danger">*</span>
          </label>
          <Select
            v-if="p.dataType === 'String' && p.allowedValues?.length > 0"
            :id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as string"
            :options="p.allowedValues"
            option-label="displayName"
            option-value="value"
            :placeholder="t('entityForm.selectPlaceholder')"
            class="w-full !h-10"
            :invalid="!!createLinkPropError(p)"
            @blur="touchedCreateLinkProps.add(p.propertyId)"
          />
          <InputText
            v-else-if="p.dataType === 'String'"
            :id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as string"
            class="w-full !h-10"
            :invalid="!!createLinkPropError(p)"
            @blur="touchedCreateLinkProps.add(p.propertyId)"
          />
          <InputNumber
            v-else-if="p.dataType === 'Int'"
            :input-id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as number"
            :max-fraction-digits="0"
            class="w-full"
            :input-class="'!h-10 w-full'"
            :invalid="!!createLinkPropError(p)"
            :input-props="{ onBlur: () => touchedCreateLinkProps.add(p.propertyId) }"
          />
          <InputNumber
            v-else-if="p.dataType === 'Decimal'"
            :input-id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as number"
            :min-fraction-digits="0"
            :max-fraction-digits="4"
            class="w-full"
            :input-class="'!h-10 w-full'"
            :invalid="!!createLinkPropError(p)"
            :input-props="{ onBlur: () => touchedCreateLinkProps.add(p.propertyId) }"
          />
          <DatePicker
            v-else-if="p.dataType === 'Date'"
            :input-id="`cl-${p.propertyId}`"
            v-model="createLinkValues[p.propertyId] as Date | null"
            date-format="yy-mm-dd"
            show-icon
            class="w-full"
            :input-class="'!h-10 w-full'"
            :invalid="!!createLinkPropError(p)"
            @blur="touchedCreateLinkProps.add(p.propertyId)"
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

      <div class="flex justify-end gap-3 pt-2">
        <Button
          :label="t('common.cancel')"
          outlined
          type="button"
          class="!h-10 !px-4 !bg-white !border !border-brand-600 !text-brand-600 hover:!bg-brand-50"
          @click="createLinkOpen = false"
        />
        <Button
          :label="t('entityForm.createAndLink')"
          icon="pi pi-check"
          :loading="createLinkSubmitting"
          :disabled="createLinkSubmitting"
          class="!h-10 !px-4 !bg-brand-600 !border !border-brand-600 !text-white hover:!bg-brand-700 hover:!border-brand-700"
          @click="submitCreateLink"
        />
      </div>
    </div>
  </Dialog>
</template>
