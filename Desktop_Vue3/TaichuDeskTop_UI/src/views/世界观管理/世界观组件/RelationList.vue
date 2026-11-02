<!-- src/views/世界观管理/components/RelationList.vue -->
<template>
  <div v-if="relations && relations.length" class="relation-list-wrapper">
    <div class="relation-header">
      <span class="relation-label">{{ label }}</span>
      <span class="relation-count">{{ relations.length }}</span>
    </div>
    <div class="relation-list">
      <button
        v-for="rel in relations"
        :key="rel.id"
        type="button"
        class="relation-item"
        :class="direction"
        :style="{ '--tc': typeColor(rel) }"
        :title="getDirectionLabel(rel)"
        @click="handleCardClick(rel)"
      >
        <span class="rel-title">{{ getCardTitle(rel) }}</span>
        <span v-if="rel.relationType" class="rel-type">{{ rel.relationType }}</span>
        <span class="rel-direction" aria-hidden="true">{{ type === 'in' ? '↘' : '↗' }}</span>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { CardTypeMeta, type CardType } from '../card_type'

const props = defineProps<{
  relations: any[]  // inRelations 或 outRelations 数组
  type: 'in' | 'out'  // 'in' 表示入度（被关联），'out' 表示出度（我关联的）
  label?: string  // 自定义标题
  projectId?: string  // 用于跳转
  cardId?: string  // 当前卡片ID，用于排除自己
}>()

const emit = defineEmits<{
  (e: 'card-click', cardId: string): void
}>()

const router = useRouter()

// 默认标签
const label = computed(() => {
  if (props.label) return props.label
  return props.type === 'in' ? '被这些条目关联' : '关联到'
})

// 获取卡片标题
const getCardTitle = (rel: any) => {
  if (props.type === 'in') {
    return rel.sourceCardTitle || rel.sourceCardId || '未知卡片'
  } else {
    return rel.targetCardTitle || rel.targetCardId || '未知卡片'
  }
}

// 条目类型的颜色（左边那条色带）
const typeColor = (rel: any) => CardTypeMeta[(props.type === 'in' ? rel.sourceCardType : rel.targetCardType) as CardType]?.color ?? 'var(--border-line)'

// 获取方向标签
const getDirectionLabel = (rel: any) => {
  if (props.type === 'in') {
    return `「${getCardTitle(rel)}」把这个条目关联为${rel.relationType ? '「' + rel.relationType + '」' : '相关条目'}`
  } else {
    return `这个条目 → 「${getCardTitle(rel)}」`
  }
}

// 获取目标卡片ID用于跳转
const getTargetCardId = (rel: any): string => {
  if (props.type === 'in') {
    return rel.sourceCardId
  } else {
    return rel.targetCardId
  }
}

// 处理卡片点击
const handleCardClick = (rel: any) => {
  const targetId = getTargetCardId(rel)
  if (!targetId) return
  if (targetId === props.cardId) return // 不跳转自己
  
  emit('card-click', targetId)
  
  // 如果有 projectId，跳转到项目内的卡片详情
  if (props.projectId) {
    router.push(`/world/project/${props.projectId}/card/${targetId}`)
  }
}

// 方向类名（用于样式区分）
const direction = computed(() => {
  return props.type === 'in' ? 'incoming' : 'outgoing'
})
</script>

<style scoped>
.relation-list-wrapper { padding: 2px 0; }
.relation-header { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
.relation-label { font: 600 12px/1.4 var(--font-mono, monospace); letter-spacing: .06em; color: var(--text-secondary); }
.relation-count { padding: 0 6px; font: 11px/1.6 var(--font-mono, monospace); color: var(--inverse-text); background: var(--inverse-bg); }
.relation-list { display: flex; flex-wrap: wrap; gap: 6px; }
.relation-item {
  --tc: var(--border-line);
  display: inline-flex; align-items: center; gap: 6px; max-width: 100%; min-height: 28px; padding: 0 8px;
  background: var(--paper-card); border: 1px solid var(--border-line); border-left: 3px solid var(--tc);
  font-size: 13px; color: var(--text-primary); cursor: pointer; transition: border-color .15s, translate .15s, box-shadow .15s;
}
.relation-item:hover, .relation-item:focus-visible { border-color: var(--tc); translate: 0 -1px; box-shadow: 2px 2px 0 color-mix(in srgb, var(--tc) 22%, transparent); }
.rel-title { min-width: 0; max-width: 12em; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.rel-type { padding: 0 5px; font: 11px/1.6 var(--font-mono, monospace); color: var(--accent-color); background: var(--accent-soft); white-space: nowrap; }
.rel-direction { font-size: 11px; color: var(--text-secondary); }
.relation-item.incoming .rel-direction { color: var(--warning); }
</style>
