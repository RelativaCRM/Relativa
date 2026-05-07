import type { EntityTypeDto } from '@/api/entities';

/** True when every schema field on the type is readonly (UI must not offer create/edit). */
export function isEntityTypeUiLocked(type: EntityTypeDto): boolean {
  const props = type.properties ?? [];
  return props.length > 0 && props.every((p) => p.isReadonly);
}
