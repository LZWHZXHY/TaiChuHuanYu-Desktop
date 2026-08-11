<!-- src/views/世界观管理/components/RelationList.vue -->
<template>
  <div v-if="relations && relations.length" class="relation-list-wrapper">
    <div class="relation-header">
      <span class="relation-label">{{ label }}</span>
      <span class="relation-count">{{ relations.length }}</span>
    </div>
    <div class="relation-list">
      <div
        v-for="rel in relations"
        :key="rel.id"
        class="relation-item"
        :class="direction"
        @click="handleCardClick(rel)"
      >
        <span class="rel-title">{{ getCardTitle(rel) }}</span>
        <span class="rel-type">「{{ rel.relationType }}」</span>
        <span class="rel-direction">{{ getDirectionLabel(rel) }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'

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
  return props.type === 'in' ? '🔗 被以下卡片关联' : '🔗 关联了以下卡片'
})

// 获取卡片标题
const getCardTitle = (rel: any) => {
  if (props.type === 'in') {
    return rel.sourceCardTitle || rel.sourceCardId || '未知卡片'
  } else {
    return rel.targetCardTitle || rel.targetCardId || '未知卡片'
  }
}

// 获取方向标签
const getDirectionLabel = (rel: any) => {
  if (props.type === 'in') {
    return '→ 本卡片'
  } else {
    return '本卡片 →'
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
.relation-list-wrapper {
  padding: 4px 0;
}

.relation-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.relation-label {
  font-weight: 500;
  font-size: 14px;
  color: #1e293b;
}

.relation-count {
  font-size: 12px;
  color: #94a3b8;
  background: #f1f5f9;
  padding: 0 10px;
  border-radius: 12px;
}

.relation-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.relation-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 14px;
  background: #f8fafc;
  border-radius: 8px;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.15s;
  border-left: 3px solid #e2e8f0;
}

.relation-item:hover {
  background: #f1f5f9;
  transform: translateX(2px);
}

/* 入度样式（被关联） */
.relation-item.incoming {
  border-left-color: #f59e0b;
}

/* 出度样式（我关联的） */
.relation-item.outgoing {
  border-left-color: #4f46e5;
}

.relation-item .rel-title {
  font-weight: 500;
  color: #0f172a;
}

.relation-item .rel-type {
  color: #4f46e5;
  background: #eef2ff;
  padding: 0 8px;
  border-radius: 4px;
  font-size: 12px;
}

.relation-item .rel-direction {
  font-size: 11px;
  color: #94a3b8;
  margin-left: auto;
  opacity: 0.7;
}
</style>