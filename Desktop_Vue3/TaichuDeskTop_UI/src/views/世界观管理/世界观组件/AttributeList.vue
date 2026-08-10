<!-- src/views/世界观管理/components/AttributeList.vue -->
<template>
  <div class="attribute-list">
    <div class="attr-header">
      <label>自定义属性</label>
      <button class="add-btn" @click="addAttribute">+ 添加</button>
    </div>
    <div class="attr-list">
      <div v-for="(attr, idx) in localValue" :key="idx" class="attr-row">
        <input v-model="attr.key" placeholder="属性名" class="attr-key" />
        <span class="attr-sep">：</span>
        <input v-model="attr.value" placeholder="值" class="attr-value" />
        <button class="remove-btn" @click="removeAttribute(idx)">✕</button>
      </div>
      <div v-if="!localValue.length" class="attr-empty">暂无自定义属性</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'

const props = defineProps<{
  modelValue: { key: string; value: string }[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: { key: string; value: string }[]): void
}>()

const localValue = ref([...props.modelValue])

const addAttribute = () => {
  localValue.value.push({ key: '', value: '' })
}

const removeAttribute = (idx: number) => {
  localValue.value.splice(idx, 1)
}

watch(localValue, (val) => {
  emit('update:modelValue', val)
}, { deep: true })

// 同步外部变化
watch(() => props.modelValue, (val) => {
  if (JSON.stringify(val) !== JSON.stringify(localValue.value)) {
    localValue.value = [...val]
  }
}, { deep: true })
</script>

<style scoped>
.attribute-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.attr-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.attr-header label {
  font-weight: 500;
  font-size: 14px;
  color: #334155;
}
.add-btn {
  padding: 2px 12px;
  border: 1px dashed #d1d5db;
  border-radius: 6px;
  background: transparent;
  color: #64748b;
  cursor: pointer;
  font-size: 12px;
}
.add-btn:hover {
  border-color: #4f46e5;
  color: #4f46e5;
}
.attr-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.attr-row {
  display: flex;
  align-items: center;
  gap: 4px;
}
.attr-key {
  flex: 1;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 13px;
  background: #fafbfc;
}
.attr-key:focus {
  outline: none;
  border-color: #4f46e5;
}
.attr-value {
  flex: 2;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 13px;
  background: #fafbfc;
}
.attr-value:focus {
  outline: none;
  border-color: #4f46e5;
}
.attr-sep { color: #94a3b8; }
.remove-btn {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
}
.remove-btn:hover { color: #ef4444; }
.attr-empty {
  font-size: 13px;
  color: #94a3b8;
  padding: 4px 0;
}
</style>