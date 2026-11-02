<!-- 设定关系（关联到）：一行排好几个小卡片，关系说明直接在卡片里改；颜色跟着目标条目的类型走 -->
<template>
  <div class="rs">
    <ul class="rs-chips" aria-label="关联到的条目">
      <li v-for="(relation, index) in modelValue" :key="relation.targetCardId" class="rs-chip" :style="{ '--tc': typeColor(relation.targetCardId) }">
        <span class="rs-kind" :title="typeLabel(relation.targetCardId)">{{ typeIcon(relation.targetCardId) }}</span>
        <span class="rs-title" :title="title(relation.targetCardId)">{{ title(relation.targetCardId) }}</span>
        <span class="rs-sep" aria-hidden="true">·</span>
        <input class="rs-type" :value="relation.relationType" maxlength="100" :list="listId" placeholder="关系"
          :style="{ width: inputWidth(relation.relationType) }"
          :aria-label="`${title(relation.targetCardId)}的关系说明`"
          @input="updateType(index, ($event.target as HTMLInputElement).value)" />
        <button type="button" class="rs-x" :aria-label="`移除与${title(relation.targetCardId)}的关系`" title="移除"
          @click="emit('update:modelValue', modelValue.filter((_, i) => i !== index))">×</button>
      </li>
      <li class="rs-add-item">
        <el-select v-model="selectedId" class="rs-add" filterable size="small" :placeholder="modelValue.length ? '＋ 再关联一个' : '＋ 关联人物、地点或组织'"
          aria-label="选择关联设定" :no-match-text="'没有找到这个条目'" :no-data-text="'这个世界里没有可以关联的条目了'" @change="addRelation">
          <el-option v-for="card in candidates" :key="card.id" :label="`${card.title} · ${getCardTypeLabel(card.type)}`" :value="card.id" />
        </el-select>
      </li>
    </ul>
    <datalist :id="listId"><option v-for="p in presets" :key="p" :value="p" /></datalist>
    <p v-if="modelValue.length" class="rs-hint">点「关系」可以写说明（如 境内、出生地、所属），随条目一起保存</p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel } from '@/utils/worldPresentation'
import { CardTypeMeta, type CardType } from '../card_type'
import type { RelationInput } from '@/api/types/world'

const props = defineProps<{ modelValue: RelationInput[]; projectId: string; excludeCardId?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: RelationInput[]] }>()
const store = useWorldStore()
const selectedId = ref('')
const listId = `rs-presets-${Math.random().toString(36).slice(2, 8)}`
const COMMON = ['境内', '位于', '首都', '出生地', '居住地', '所属', '成员', '领袖', '盟友', '敌对', '亲属', '师徒', '持有', '创造者', '起源']

const candidates = computed(() => store.cards.filter(card => card.projectId === props.projectId && card.id !== props.excludeCardId && !props.modelValue.some(r => r.targetCardId === card.id)))
const presets = computed(() => [...new Set([...props.modelValue.map(r => r.relationType.trim()).filter(Boolean), ...COMMON])])
const card = (id: string) => store.getCardById(id, props.projectId)
const title = (id: string) => card(id)?.title || '条目已删除'
const meta = (id: string) => CardTypeMeta[card(id)?.type as CardType]
const typeColor = (id: string) => meta(id)?.color ?? 'var(--text-secondary)'
const typeIcon = (id: string) => meta(id)?.icon ?? '◇'
const typeLabel = (id: string) => getCardTypeLabel(card(id)?.type ?? '')
/** 输入框跟着字数变宽：中文按 1 个字宽，至少能放下「关系」两个字 */
const inputWidth = (text: string) => `${Math.min(11, Math.max(3.4, [...text].length * 1.2 + 1.2))}em`

function addRelation(id: string) {
  if (candidates.value.some(c => c.id === id)) emit('update:modelValue', [...props.modelValue, { targetCardId: id, relationType: '' }])
  selectedId.value = ''
}
function updateType(index: number, relationType: string) {
  emit('update:modelValue', props.modelValue.map((relation, i) => i === index ? { ...relation, relationType } : relation))
}
</script>

<style scoped>
.rs { display: grid; gap: 8px; }
.rs-chips { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin: 0; padding: 0; list-style: none; }
.rs-chip {
  --tc: var(--accent-color);
  position: relative; display: inline-flex; align-items: center; gap: 6px; max-width: 100%; min-height: 32px;
  padding: 0 4px 0 10px; background: var(--paper-card); border: 1px solid var(--border-line); border-left: 3px solid var(--tc);
  font-size: 13px; transition: border-color .15s, box-shadow .15s, translate .15s;
}
.rs-chip::after { content: ''; position: absolute; right: -1px; top: -1px; width: 7px; height: 7px; background: linear-gradient(225deg, var(--tc) 50%, transparent 50%); opacity: .55; }
.rs-chip:hover, .rs-chip:focus-within { border-color: var(--tc); border-left-color: var(--tc); box-shadow: 2px 2px 0 color-mix(in srgb, var(--tc) 22%, transparent); translate: 0 -1px; }
.rs-kind { font-size: 12px; line-height: 1; filter: saturate(.8); }
.rs-title { min-width: 0; max-width: 12em; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; color: var(--text-primary); }
.rs-sep { color: var(--text-secondary); opacity: .6; }
.rs-type {
  min-width: 0; padding: 2px 4px; border: 0; border-bottom: 1px dashed color-mix(in srgb, var(--tc) 55%, var(--border-line));
  background: transparent; color: var(--tc); font: 12px/1.6 var(--font-mono, monospace); letter-spacing: .02em; border-radius: 0;
}
.rs-type::placeholder { color: var(--text-secondary); opacity: .7; }
.rs-type:focus { outline: none; border-bottom-style: solid; background: color-mix(in srgb, var(--tc) 8%, transparent); }
.rs-x { width: 24px; height: 24px; border: 0; background: none; color: var(--text-secondary); font-size: 16px; line-height: 1; cursor: pointer; }
.rs-x:hover { color: var(--danger); background: var(--danger-soft); }
.rs-add-item { display: inline-flex; }
.rs-add { width: 220px; }
.rs-add :deep(.el-select__wrapper) { min-height: 32px; border-radius: 0; box-shadow: none; border: 1px dashed var(--border-line); background: transparent; }
.rs-add :deep(.el-select__wrapper.is-focused), .rs-add:hover :deep(.el-select__wrapper) { border-color: var(--accent-color); border-style: solid; }
.rs-add :deep(.el-select__placeholder) { color: var(--accent-color); }
.rs-hint { margin: 0; color: var(--text-secondary); font-size: 12px; }
@media (max-width: 600px) { .rs-add { width: 100%; } .rs-add-item { flex: 1 1 100%; } .rs-title { max-width: 9em; } }
</style>
