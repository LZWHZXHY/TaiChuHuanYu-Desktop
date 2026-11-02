<!-- 资源的标题 / 分类 / 位置 / 价格 / 标签 / 介绍：上传和「我的资源」里修改共用这一组输入框 -->
<template>
  <div class="rmf">
    <label class="rmf-field rmf-title">
      <span>标题 <small>{{ meta.title.length }}/{{ config?.titleMax ?? 60 }}</small></span>
      <input v-model.trim="meta.title" :maxlength="config?.titleMax ?? 60" :disabled="disabled" placeholder="一句话说清楚这是什么，比如：雨夜环境音效包" required />
    </label>
    <div class="rmf-row">
      <label class="rmf-field">
        <span>分类</span>
        <select v-model="meta.category" :disabled="disabled">
          <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
        </select>
      </label>
      <label class="rmf-field">
        <span>价格（经验）</span>
        <input v-model.number="meta.price" type="number" min="0" :max="maxPrice" step="1" inputmode="numeric" :disabled="disabled" @blur="clampPrice" />
      </label>
    </div>
    <label class="rmf-field">
      <span>位置 <small>可以选已有的，也可以填新的，用 / 分层</small></span>
      <input v-model="meta.folder" :list="listId" maxlength="200" :disabled="disabled" :placeholder="`不填就放在「${meta.category}」`" @blur="meta.folder = cleanFolder(meta.folder)" />
      <datalist :id="listId"><option v-for="f in folders" :key="f" :value="f" /></datalist>
    </label>
    <p class="rmf-hint">
      <template v-if="!meta.price">免费资源：每多一个不同的人下载，你得 {{ config?.freeDownloadExp ?? 2 }} 经验（每天最多 {{ config?.freeDownloadDailyCap ?? 50 }} 经验）。</template>
      <template v-else>付费资源：别人第一次下载时花掉的 {{ meta.price }} 经验<b>全部加给你</b>，买过的人以后可以随时重新下载。</template>
      价格最多 {{ maxPrice }}，填 0 就是免费。
    </p>
    <label class="rmf-field">
      <span>标签 <small>用空格或逗号分开，最多 {{ config?.tagCountMax ?? 8 }} 个</small></span>
      <input v-model="tagText" maxlength="160" :disabled="disabled" placeholder="比如：雨声 环境音 循环" @blur="syncTags" />
    </label>
    <ul v-if="meta.tags.length" class="rmf-tags" aria-label="已填的标签">
      <li v-for="t in meta.tags" :key="t">#{{ t }}</li>
    </ul>
    <label class="rmf-field">
      <span>介绍 <small>{{ meta.description.length }}/{{ config?.descriptionMax ?? 2000 }}</small></span>
      <textarea v-model="meta.description" rows="5" :maxlength="config?.descriptionMax ?? 2000" :disabled="disabled" placeholder="里面有什么、怎么用、有没有使用限制（比如能不能商用）、来源和授权……"></textarea>
    </label>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, useId, watch } from 'vue'
import { RESOURCE_CATEGORIES, resourceApi, type ResourceConfig, type ResourceMeta } from '@/api/resources'

const props = defineProps<{ config: ResourceConfig | null; disabled?: boolean }>()
const meta = defineModel<ResourceMeta>({ required: true })

const categories = computed(() => props.config?.categories ?? RESOURCE_CATEGORIES)

// 位置：下拉里给出官方资源的文件夹和别人用过的位置，也可以手填（只是展示 / 整理用，文件在 NAS 上的真实位置不变）
const listId = useId()
const folders = ref<string[]>([])
onMounted(async () => { try { folders.value = await resourceApi.folders() } catch { /* 读不到就只能手填 */ } })
/** 和后端的清洗规则一样：反斜杠换成 /、去掉空段和「..」，每层最多 40 字，最多 4 层 */
function cleanFolder(raw: string) {
  const parts = raw.replace(/\\/g, '/').split('/')
    .map(p => p.replace(/[\u0000-\u001f\u007f]/g, '').replace(/\.\./g, '').trim())
    .filter(p => p && p !== '.')
    .map(p => p.slice(0, 40))
  return parts.slice(0, 4).join('/').slice(0, 200)
}
const maxPrice = computed(() => props.config?.maxPrice ?? 500)

// 标签：输入框里是一串文字，失焦或输入时拆成标签（去掉 #、去重、限制个数和长度，和后端规则一致）
const tagText = ref(meta.value.tags.join(' '))
function parseTags(text: string) {
  const list: string[] = []
  for (const raw of text.split(/[\s,，、]+/)) {
    const t = raw.replace(/^#+/, '').trim().slice(0, 16)
    if (t && !list.some(x => x.toLowerCase() === t.toLowerCase())) list.push(t)
    if (list.length >= (props.config?.tagCountMax ?? 8)) break
  }
  return list
}
function syncTags() { meta.value.tags = parseTags(tagText.value) }
watch(tagText, syncTags)
// 外面换了一份资料（比如打开另一个资源编辑）时，输入框跟着变
watch(() => meta.value, value => { tagText.value = value.tags.join(' ') })

function clampPrice() {
  const n = Math.floor(Number(meta.value.price) || 0)
  meta.value.price = Math.min(maxPrice.value, Math.max(0, n))
}
</script>

<style scoped>
.rmf { display: grid; gap: 14px; }
.rmf-row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; }
.rmf-field { display: grid; gap: 6px; font-size: 13px; color: var(--text-secondary); }
.rmf-field > span { display: flex; justify-content: space-between; gap: 8px; }
.rmf-field small { font: 11px var(--font-mono); color: var(--text-secondary); }
.rmf-field :is(input, select, textarea) { width: 100%; box-sizing: border-box; padding: 10px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-bg, var(--bg-main)); color: var(--text-primary); font: inherit; font-size: 14px; }
.rmf-field textarea { resize: vertical; min-height: 110px; line-height: 1.7; }
.rmf-field :is(input, select, textarea):focus-visible { outline: 2px solid var(--accent-color); outline-offset: 1px; }
.rmf-hint { margin: -4px 0 0; padding: 10px 12px; border-left: 2px solid var(--signal-mint); background: var(--bg-sub); color: var(--text-secondary); font-size: 12px; line-height: 1.7; }
.rmf-hint b { color: var(--accent-color); }
.rmf-tags { display: flex; flex-wrap: wrap; gap: 6px; margin: -6px 0 0; padding: 0; list-style: none; }
.rmf-tags li { padding: 2px 8px; border: 1px solid var(--border-line); font: 11px var(--font-mono); color: var(--text-secondary); }
@media (max-width: 560px) { .rmf-row { grid-template-columns: minmax(0, 1fr); } }
</style>
