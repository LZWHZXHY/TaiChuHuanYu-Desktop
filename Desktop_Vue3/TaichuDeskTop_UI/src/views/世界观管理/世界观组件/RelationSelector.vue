<!-- src/views/世界观管理/components/RelationSelector.vue -->
<template>
  <div class="relation-selector">
    <label>关联卡片</label>
    <div class="relation-list">
      <div v-for="(rel, idx) in localValue" :key="idx" class="relation-item">
        <span class="rel-target">{{ getCardTitle(rel.targetCardId) }}</span>
        <span class="rel-type">「{{ rel.relationType }}」</span>
        <button class="remove-btn" @click="removeRelation(idx)">✕</button>
      </div>
      <div v-if="!localValue.length" class="relation-empty">暂无关联</div>
    </div>
    <div class="relation-add">
      <el-select
        v-model="newTargetId"
        filterable
        remote
        :remote-method="searchCards"
        placeholder="搜索并选择卡片"
        size="small"
      >
        <el-option
          v-for="card in searchResults"
          :key="card.id"
          :label="`${card.title} (${getTypeLabel(card.type)})`"
          :value="card.id"
        />
      </el-select>
      <input
        v-model="newRelationType"
        placeholder="关系描述"
        class="relation-input"
        @keydown.enter.prevent="addRelation"
      />
      <button class="add-btn" @click="addRelation">+</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { CardTypeMeta } from '../card_type'

const props = defineProps<{
  modelValue: { targetCardId: string; relationType: string }[]
  projectId: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: { targetCardId: string; relationType: string }[]): void
}>()

const store = useWorldStore()

const localValue = ref([...props.modelValue])
const newTargetId = ref('')
const newRelationType = ref('')
const searchResults = ref<any[]>([])

const getCardTitle = (cardId: string) => {
  const card = store.cards.find(c => c.id === cardId)
  return card?.title || '已删除'
}

const getTypeLabel = (type: string) => {
  return CardTypeMeta[type as keyof typeof CardTypeMeta]?.label || type
}

const searchCards = (query: string) => {
  const cards = store.cards
  if (!query) {
    searchResults.value = cards.slice(0, 10)
    return
  }
  const lower = query.toLowerCase()
  searchResults.value = cards.filter(c => c.title.toLowerCase().includes(lower)).slice(0, 10)
}

const addRelation = () => {
  if (!newTargetId.value) {
    ElMessage.warning('请选择卡片')
    return
  }
  if (!newRelationType.value.trim()) {
    ElMessage.warning('请输入关系描述')
    return
  }
  if (localValue.value.some(r => r.targetCardId === newTargetId.value)) {
    ElMessage.warning('已关联该卡片')
    return
  }
  localValue.value.push({
    targetCardId: newTargetId.value,
    relationType: newRelationType.value.trim(),
  })
  newTargetId.value = ''
  newRelationType.value = ''
}

const removeRelation = (idx: number) => {
  localValue.value.splice(idx, 1)
}

watch(localValue, (val) => {
  emit('update:modelValue', val)
}, { deep: true })

watch(() => props.modelValue, (val) => {
  if (JSON.stringify(val) !== JSON.stringify(localValue.value)) {
    localValue.value = [...val]
  }
}, { deep: true })

// 初始加载卡片列表
searchCards('')
</script>

<style scoped>
.relation-selector {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.relation-selector label {
  font-weight: 500;
  font-size: 14px;
  color: #334155;
}
.relation-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-height: 20px;
}
.relation-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 2px 8px;
  background: #f8f9fc;
  border-radius: 4px;
  font-size: 13px;
}
.relation-item .rel-target {
  font-weight: 500;
}
.relation-item .rel-type {
  color: #4f46e5;
}
.remove-btn {
  margin-left: auto;
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
}
.remove-btn:hover {
  color: #ef4444;
}
.relation-empty {
  font-size: 13px;
  color: #94a3b8;
  padding: 2px 0;
}
.relation-add {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}
.relation-add :deep(.el-select) {
  flex: 2;
  min-width: 100px;
}
.relation-input {
  flex: 3;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 13px;
}
.relation-input:focus {
  outline: none;
  border-color: #4f46e5;
}
.add-btn {
  padding: 4px 12px;
  border: none;
  border-radius: 6px;
  background: #4f46e5;
  color: white;
  cursor: pointer;
  font-size: 14px;
}
.add-btn:hover {
  background: #4338ca;
}
</style>