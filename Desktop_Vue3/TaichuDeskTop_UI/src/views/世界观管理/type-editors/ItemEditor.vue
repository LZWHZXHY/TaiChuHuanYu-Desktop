<!-- src/views/世界观管理/type-editors/ItemEditor.vue -->
<template>
  <div class="item-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>稀有度</label>
          <div class="rarity-options">
            <button
              v-for="r in rarityOptions"
              :key="r"
              class="rarity-btn"
              :class="{ active: localData.rarity === r, [r]: true }"
              @click="localData.rarity = r"
            >
              {{ r }}
            </button>
          </div>
          <p class="hint">选择物品的稀有度，会显示对应的颜色标识</p>
        </div>
      </div>

      <div class="right-column">
        <div class="empty-hint">
          材质、重量、价值、来源、用途等信息<br />
          可通过「<strong>自定义属性</strong>」自由添加
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：如需关联「持有者」，可在「关联内容」区域点击「+角色」按钮插入角色卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { ItemData } from '../card_type'

const props = defineProps<{
  modelValue?: ItemData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: ItemData): void
}>()

// ❌ 删除了 material、weight、value、origin、usage
const defaultData: ItemData = {
  id: '',
  projectId: '',
  type: 'item',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  rarity: undefined,
}

const localData = ref<ItemData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const rarityOptions = ['普通', '稀有', '史诗', '传说', '神器'] as const

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.item-editor {
  padding: 16px 0;
  border-top: 1px solid #eef2f6;
  border-bottom: 1px solid #eef2f6;
}

.editor-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 32px;
}

.field-group {
  margin-bottom: 16px;
}
.field-group label {
  display: block;
  font-size: 13px;
  font-weight: 500;
  color: #334155;
  margin-bottom: 4px;
}

.hint {
  font-size: 11px;
  color: #94a3b8;
  margin: 4px 0 0;
  font-style: italic;
}

.empty-hint {
  padding: 20px;
  text-align: center;
  color: #c0c4cc;
  font-size: 13px;
  border: 1px dashed #e2e8f0;
  border-radius: 6px;
  line-height: 1.8;
}
.empty-hint strong {
  color: #4f46e5;
}

.rarity-options {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.rarity-btn {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: 0.15s;
  font-size: 13px;
}
.rarity-btn.active {
  border-color: #4f46e5;
  background: #eef2ff;
}
.rarity-btn.普通.active {
  background: #f1f5f9;
  border-color: #94a3b8;
}
.rarity-btn.稀有.active {
  background: #e0f2fe;
  border-color: #0ea5e9;
  color: #0369a1;
}
.rarity-btn.史诗.active {
  background: #f3e8ff;
  border-color: #8b5cf6;
  color: #6d28d9;
}
.rarity-btn.传说.active {
  background: #fef3c7;
  border-color: #f59e0b;
  color: #b45309;
}
.rarity-btn.神器.active {
  background: #fecaca;
  border-color: #ef4444;
  color: #dc2626;
}

.editor-hint {
  margin-top: 12px;
  padding: 8px 12px;
  background: #f0f4ff;
  border-radius: 6px;
  font-size: 12px;
  color: #4f46e5;
  border: 1px solid #c7d2fe;
}
.editor-hint p {
  margin: 0;
}
</style>