// src/composables/世界观管理/useRelationDiff.ts
export function useRelationDiff() {
  const computeDiff = (
    existing: { id: string; targetCardId: string; relationType: string }[],
    newRelations: { targetCardId: string; relationType: string }[]
  ) => {
    const toRemove = existing.filter(old =>
      !newRelations.some(n => n.targetCardId === old.targetCardId)
    )
    const toAdd = newRelations.filter(n =>
      !existing.some(old => old.targetCardId === n.targetCardId)
    )
    return { toRemove, toAdd }
  }

  return { computeDiff }
}