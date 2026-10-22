import { diffRelations } from '@/utils/worldCards'

export function useRelationDiff() {
  return { computeDiff: diffRelations }
}
