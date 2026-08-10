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
        
        <!-- ✅ 根据类型渲染不同控件 -->
        <input
          v-if="attr.type === 'short'"
          v-model="attr.value"
          placeholder="短文本值"
          class="attr-value"
        />
        <textarea
          v-else-if="attr.type === 'long'"
          v-model="attr.value"
          placeholder="长文本内容..."
          rows="3"
          class="attr-value attr-value-long"
        />
        <input
          v-else-if="attr.type === 'number'"
          v-model.number="attr.value"
          type="number"
          placeholder="数字值"
          class="attr-value"
        />
        <input
          v-else-if="attr.type === 'date'"
          v-model="attr.value"
          type="date"
          class="attr-value"
        />
        <div v-else-if="attr.type === 'boolean'" class="attr-value-boolean">
          <button
            class="bool-btn"
            :class="{ active: attr.value === true }"
            @click="attr.value = true"
          >是</button>
          <button
            class="bool-btn"
            :class="{ active: attr.value === false }"
            @click="attr.value = false"
          >否</button>
        </div>
        
        <!-- ✅ 类型选择下拉 -->
        <select v-model="attr.type" class="attr-type">
          <option value="short">短文本</option>
          <option value="long">长文本</option>
          <option value="number">数字</option>
          <option value="date">日期</option>
          <option value="boolean">是/否</option>
        </select>
        
        <button class="remove-btn" @click="removeAttribute(idx)">✕</button>
      </div>
      <div v-if="!localValue.length" class="attr-empty">暂无自定义属性</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { AttributeItem } from '../card_type'


const props = defineProps<{
  modelValue: AttributeItem[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: AttributeItem[]): void
}>()

const localValue = ref<AttributeItem[]>([...props.modelValue])

const addAttribute = () => {
  localValue.value.push({ key: '', value: '', type: 'short' })
}

const removeAttribute = (idx: number) => {
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
  align-items: flex-start;
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
  font-family: inherit;
}
.attr-value:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}
.attr-value-long {
  min-height: 60px;
  resize: vertical;
}

.attr-value-boolean {
  display: flex;
  gap: 4px;
  flex: 2;
}
.bool-btn {
  padding: 4px 16px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 13px;
}
.bool-btn.active {
  background: #4f46e5;
  color: white;
  border-color: #4f46e5;
}
.bool-btn:hover:not(.active) {
  background: #f1f5f9;
}

.attr-type {
  flex: 0 0 80px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 4px 6px;
  font-size: 12px;
  background: #fafbfc;
  color: #475569;
  height: 32px;
}
.attr-type:focus {
  outline: none;
  border-color: #4f46e5;
}

.attr-sep {
  color: #94a3b8;
  padding-top: 4px;
}

.remove-btn {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
  padding: 4px 4px 0 4px;
}
.remove-btn:hover {
  color: #ef4444;
}

.attr-empty {
  font-size: 13px;
  color: #94a3b8;
  padding: 4px 0;
}
</style>