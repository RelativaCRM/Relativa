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
  let starting: Promise<void> | null = null;
  let stopped = false;

  async function start() {
    stopped = false;
    const c = buildCoreHubConnection();
    conn = c;
    c.on(ENTITY_RELATIONSHIPS_CHANGED, onChanged);
    starting = (async () => {
      try {
        await c.start();
        if (stopped) {
          await c.stop();
          return;
        }
        await c.invoke('JoinEntity', workspaceId.value, entityId.value);
      } catch {}
    })();
    await starting;
  }

  async function stop() {
    stopped = true;
    const c = conn;
    conn = null;
    if (starting) {
      try {
        await starting;
      } catch {}
      starting = null;
    }
    if (!c) return;
    try {
      await c.invoke('LeaveEntity', workspaceId.value, entityId.value);
    } catch {}
    try {
      await c.stop();
    } catch {}
  }

  return { start, stop };
}
