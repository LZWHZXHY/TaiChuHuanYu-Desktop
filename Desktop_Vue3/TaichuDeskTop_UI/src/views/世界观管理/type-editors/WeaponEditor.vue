<!-- src/views/世界观管理/type-editors/WeaponEditor.vue -->
<template>
  <div class="weapon-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>武器类型</label>
          <el-select v-model="localData.weaponType" filterable allow-create placeholder="选择或输入武器类型">
            <el-option
              v-for="t in ['剑', '斧', '锤', '长矛', '弓', '弩', '法杖', '匕首', '盾牌', '拳套']"
              :key="t"
              :label="t"
              :value="t"
            />
          </el-select>
        </div>

        <div class="field-group">
          <label>材质</label>
          <el-select v-model="localData.material" filterable allow-create placeholder="选择或输入材质">
            <el-option
              v-for="m in ['钢铁', '秘银', '精金', '龙骨', '魔法合金', '木材', '石材']"
              :key="m"
              :label="m"
              :value="m"
            />
          </el-select>
        </div>

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
        </div>

        <div class="field-group">
          <label>伤害</label>
          <input v-model="localData.damage" placeholder="如：2d6+3 或 50-80" />
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>重量 (kg)</label>
          <input v-model.number="localData.weight" type="number" placeholder="如：2.5" min="0" step="0.1" />
        </div>

        <div class="field-group">
          <label>来源</label>
          <input v-model="localData.origin" placeholder="如：矮人锻造、上古遗迹" />
        </div>

        <div class="empty-hint">此处可放置更多设定</div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+角色」按钮，可以插入「持有者」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { WeaponData } from '../card_type'

const props = defineProps<{
  modelValue?: WeaponData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: WeaponData): void
}>()

// ❌ 删除了 wielder 字段
const defaultData: WeaponData = {
  id: '',
  projectId: '',
  type: 'weapon',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  weaponType: '',
  material: '',
  rarity: undefined,
  damage: '',
  weight: undefined,
  origin: '',
}

const localData = ref<WeaponData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const rarityOptions = ['普通', '稀有', '史诗', '传说', '神器'] as const

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.weapon-editor {
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
.field-group input,
.field-group .el-select {
  width: 100%;
}
.field-group input {
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
}
.field-group input:focus {
  outline: none;
  border-color: #4f46e5;
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
  font-size: 13px;
}
.rarity-btn.active {
  border-color: #4f46e5;
  background: #eef2ff;
}
.rarity-btn.普通.active { background: #f1f5f9; border-color: #94a3b8; }
.rarity-btn.稀有.active { background: #e0f2fe; border-color: #0ea5e9; color: #0369a1; }
.rarity-btn.史诗.active { background: #f3e8ff; border-color: #8b5cf6; color: #6d28d9; }
.rarity-btn.传说.active { background: #fef3c7; border-color: #f59e0b; color: #b45309; }
.rarity-btn.神器.active { background: #fecaca; border-color: #ef4444; color: #dc2626; }

.empty-hint {
  padding: 16px;
  text-align: center;
  color: #c0c4cc;
  font-size: 13px;
  border: 1px dashed #e2e8f0;
  border-radius: 6px;
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