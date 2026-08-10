<!-- src/views/世界观管理/components/TagInput.vue -->
<template>
  <div class="tag-input">
    <div class="tag-header">
      <label>标签</label>
      <span class="tag-count">{{ localValue.length }} / 10</span>
    </div>
    <div class="tag-input-row">
      <input
        v-model="tagText"
        placeholder="输入标签，按回车添加"
        @keydown.enter.prevent="addTag"
      />
      <button class="add-btn" @click="addTag">添加</button>
    </div>
    <div class="tag-list">
      <span v-for="tag in localValue" :key="tag" class="tag-item">
        #{{ tag }}
        <button class="remove-btn" @click="removeTag(tag)">×</button>
      </span>
      <span v-if="!localValue.length" class="tag-empty">暂无标签</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'

const props = defineProps<{
  modelValue: string[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string[]): void
}>()

const localValue = ref([...props.modelValue])
const tagText = ref('')

const addTag = () => {
  const text = tagText.value.trim()
  if (!text) return
  if (localValue.value.includes(text)) {
    ElMessage.warning('标签已存在')
    return
  }
  if (localValue.value.length >= 10) {
    ElMessage.warning('最多添加 10 个标签')
    return
  }
  localValue.value.push(text)
  tagText.value = ''
}

const removeTag = (tag: string) => {
  localValue.value = localValue.value.filter(t => t !== tag)
}

watch(localValue, (val) => {
  emit('update:modelValue', val)
}, { deep: true })

watch(() => props.modelValue, (val) => {
  if (JSON.stringify(val) !== JSON.stringify(localValue.value)) {
    localValue.value = [...val]
  }
}, { deep: true })
</script>

<style scoped>
.tag-input {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.tag-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.tag-header label {
  font-weight: 500;
  font-size: 14px;
  color: #334155;
}
.tag-count {
  font-size: 12px;
  color: #94a3b8;
}
.tag-input-row {
  display: flex;
  gap: 4px;
}
.tag-input-row input {
  flex: 1;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 13px;
}
.tag-input-row input:focus {
  outline: none;
  border-color: #4f46e5;
}
.add-btn {
  padding: 4px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 13px;
}
.add-btn:hover {
  background: #f1f5f9;
}
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 2px;
}
.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  background: #eef2ff;
  color: #4f46e5;
  padding: 2px 8px 2px 10px;
  border-radius: 12px;
  font-size: 12px;
}
.remove-btn {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
  padding: 0 2px;
}
.remove-btn:hover {
  color: #ef4444;
}
.tag-empty {
  font-size: 13px;
  color: #94a3b8;
  padding: 2px 0;
}
</style>