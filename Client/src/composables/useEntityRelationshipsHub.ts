import type { Ref } from 'vue';
import type { HubConnection } from '@microsoft/signalr';
import { buildCoreHubConnection } from '@/api/coreHub';

const ENTITY_RELATIONSHIPS_CHANGED = 'entity.relationships.changed.v1';

export function useEntityRelationshipsHub(
  workspaceId: Ref<number>,
  entityId: Ref<number>,
  onChanged: () => void,
) {
  let conn: HubConnection | null = null;

  async function start() {
    conn = buildCoreHubConnection();
    conn.on(ENTITY_RELATIONSHIPS_CHANGED, onChanged);
    await conn.start();
    await conn.invoke('JoinEntity', workspaceId.value, entityId.value);
  }

  async function stop() {
    if (!conn) return;
    try {
      await conn.invoke('LeaveEntity', workspaceId.value, entityId.value);
    } catch {}
    await conn.stop();
    conn = null;
  }

  return { start, stop };
}
