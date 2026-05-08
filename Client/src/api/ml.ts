import { api } from '@/api/http';

export interface DealScoreDto {
  entity_id: number;
  closure_score: number | null;
  churn_score: number | null;
  unavailable_reason: string | null;
}

const ML = '/ml/api/ml';

export const mlApi = {
  scoreBatch(entityIds: number[]): Promise<DealScoreDto[]> {
    return api.post<DealScoreDto[]>(`${ML}/score/batch`, { entity_ids: entityIds });
  },
};
