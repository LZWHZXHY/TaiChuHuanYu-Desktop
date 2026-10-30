<!-- 岁月史书里的一段：类型、发生日期、标题、内容、截图，谁上传的、什么时候上传的，表情回应。
     上传的人可以删除自己的；管理员可以编辑、下架、恢复、删除。 -->
<template>
  <article class="he" :class="['k-' + entry.kind, { 'is-hidden': entry.hidden, compact }]" :id="`history-${entry.id}`">
    <p v-if="entry.hidden" class="he-hidden" role="note">
      🚫 已被管理员下架<template v-if="entry.hiddenReason">：{{ entry.hiddenReason }}</template>（{{ canManage ? '其他人看不到' : '只有你自己看得到' }}）
    </p>
    <header class="he-head">
      <span class="he-kind">{{ KIND_META[entry.kind].icon }} {{ KIND_META[entry.kind].label }}</span>
      <time class="he-date" :datetime="entry.happenedOn">{{ dateText }}</time>
      <span v-if="ago" class="he-ago">{{ ago }}</span>
    </header>
    <h3 v-if="entry.title" class="he-title">{{ entry.title }}</h3>
    <div v-if="entry.content" class="he-content" :class="{ clamped: !expanded && long }">{{ entry.content }}</div>
    <button v-if="long" type="button" class="he-more" @click="expanded = !expanded">{{ expanded ? '收起' : '展开全文' }}</button>

    <div v-if="entry.images.length" class="he-images" :class="'n' + (entry.images.length >= 5 ? 'many' : entry.images.length)">
      <button v-for="(url, i) in shownImages" :key="url" type="button" class="he-img" :aria-label="`查看第 ${i + 1} 张图`" @click="emit('preview', entry.images, i)">
        <img :src="entry.images.length === 1 ? url : withThumbnail(url, 480)" alt="" loading="lazy" />
        <span v-if="i === shownImages.length - 1 && entry.images.length > shownImages.length" class="he-img-more">+{{ entry.images.length - shownImages.length }}</span>
      </button>
    </div>

    <footer class="he-foot">
      <UserLink :id="entry.author.id" :name="entry.author.username" class="he-author">
        <AvatarFrame :user-id="entry.author.id" size="sm">
          <img v-if="entry.author.avatar" :src="entry.author.avatar" class="he-avatar" alt="" />
          <span v-else class="he-avatar he-avatar-empty" aria-hidden="true">{{ entry.author.username.slice(0, 1) }}</span>
        </AvatarFrame>
        <span class="he-author-name">{{ entry.author.username }}</span>
      </UserLink>
      <span class="he-meta" :title="`上传于 ${fullTime(entry.createdAt)}`">记于 {{ fullTime(entry.createdAt) }}</span>
      <span v-if="entry.editedAt" class="he-meta" :title="`管理员 ${entry.editedByName || ''} 于 ${fullTime(entry.editedAt)} 编辑过`">· 管理员编辑过</span>
    </footer>

    <div class="he-actions">
      <div class="he-reacts" role="group" aria-label="表情回应">
        <button v-for="(m, k) in REACTION_META" :key="k" type="button" class="he-react" :class="{ on: entry.myReactions.includes(k) }"
          :aria-pressed="entry.myReactions.includes(k)" :title="m.label" :disabled="busy" @click="emit('react', entry, k)">
          <span aria-hidden="true">{{ m.icon }}</span><span class="he-react-label">{{ m.label }}</span><b v-if="entry.reactions[k]">{{ entry.reactions[k] }}</b>
        </button>
      </div>
      <div class="he-ops">
        <button type="button" class="he-op" title="复制这一段的链接" @click="emit('copy-link', entry)">链接</button>
        <template v-if="canManage">
          <button type="button" class="he-op" @click="emit('edit', entry)">编辑</button>
          <button v-if="!entry.hidden" type="button" class="he-op" @click="emit('hide', entry)">下架</button>
          <button v-else type="button" class="he-op" @click="emit('unhide', entry)">恢复</button>
        </template>
        <button v-if="entry.canDelete" type="button" class="he-op danger" @click="emit('delete', entry)">删除</button>
      </div>
    </div>
    <HistoryComments v-if="!compact" :entry="entry" @update="patch => emit('update', { ...entry, ...patch })" />
  </article>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { KIND_META, REACTION_META, type HistoryEntry, type ReactionKind } from '@/api/history'
import { withThumbnail } from '@/utils/imageThumb'
import UserLink from '@/components/UserLink.vue'
import AvatarFrame from '@/components/AvatarFrame.vue'
import HistoryComments from './HistoryComments.vue'

const props = defineProps<{ entry: HistoryEntry; canManage?: boolean; busy?: boolean; compact?: boolean }>()
const emit = defineEmits<{
  preview: [images: string[], index: number]
  react: [entry: HistoryEntry, kind: ReactionKind]
  edit: [entry: HistoryEntry]
  hide: [entry: HistoryEntry]
  unhide: [entry: HistoryEntry]
  delete: [entry: HistoryEntry]
  'copy-link': [entry: HistoryEntry]
  update: [entry: HistoryEntry]
}>()

const expanded = ref(false)
const long = computed(() => !props.compact && ((props.entry.content ?? '').length > 280 || (props.entry.content ?? '').split('\n').length > 8))
const shownImages = computed(() => props.entry.images.slice(0, props.compact ? 3 : 9))

const dateText = computed(() => {
  const [y, m, d] = props.entry.happenedOn.split('-').map(Number)
  if (props.entry.precision === 'year') return `${y} 年的某一天`
  if (props.entry.precision === 'month') return `${y} 年 ${m} 月的某一天`
  return `${y} 年 ${m} 月 ${d} 日`
})
/** 几年前 / 几个月前（按北京时间的今天算） */
const ago = computed(() => {
  const [y, m, d] = props.entry.happenedOn.split('-').map(Number) as [number, number, number]
  const now = new Date(Date.now() + 8 * 3600_000)
  const ty = now.getUTCFullYear(), tm = now.getUTCMonth() + 1, td = now.getUTCDate()
  const days = Math.round((Date.UTC(ty, tm - 1, td) - Date.UTC(y, m - 1, d)) / 86_400_000)
  if (props.entry.precision === 'year') return ty - y > 0 ? `${ty - y} 年前` : '今年'
  if (props.entry.precision === 'month') { const n = (ty - y) * 12 + tm - m; return n <= 0 ? '这个月' : n < 12 ? `${n} 个月前` : `${Math.floor(n / 12)} 年前` }
  if (days <= 0) return '今天'
  if (days === 1) return '昨天'
  if (days < 30) return `${days} 天前`
  if (days < 365) return `${Math.floor(days / 30)} 个月前`
  return `${Math.floor(days / 365)} 年前`
})
function fullTime(iso: string) {
  const t = new Date(new Date(iso).getTime() + 8 * 3600_000)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${t.getUTCFullYear()}-${p(t.getUTCMonth() + 1)}-${p(t.getUTCDate())} ${p(t.getUTCHours())}:${p(t.getUTCMinutes())}`
}
</script>

<style scoped>
.he { position: relative; padding: 14px 16px 12px; background: var(--paper-card); border: 1px solid var(--border-line); border-left: 3px solid var(--kc, var(--accent-color)); }
.he.k-funny { --kc: #f59e0b; }
.he.k-chat { --kc: #3b82f6; }
.he.k-life { --kc: #22c55e; }
.he.k-progress { --kc: #a855f7; }
.he.k-event { --kc: #ef4444; }
.he.k-other { --kc: #64748b; }
.he.is-hidden { opacity: .8; background: repeating-linear-gradient(135deg, var(--paper-card) 0 10px, color-mix(in srgb, var(--text-secondary) 6%, var(--paper-card)) 10px 11px); }
.he-hidden { margin: -4px 0 10px; padding: 6px 10px; background: color-mix(in srgb, #ef4444 10%, transparent); color: #b91c1c; font-size: 12px; }
.he-head { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 10px; font-size: 12px; color: var(--text-secondary); }
.he-kind { padding: 1px 8px; background: color-mix(in srgb, var(--kc) 14%, transparent); color: var(--text-primary); font-weight: 600; }
.he-date { font-weight: 600; color: var(--text-primary); font-variant-numeric: tabular-nums; }
.he-title { margin: 8px 0 4px; font-size: 16px; line-height: 1.5; overflow-wrap: anywhere; }
.he-content { margin-top: 6px; font-size: 14px; line-height: 1.8; white-space: pre-wrap; overflow-wrap: anywhere; color: var(--text-primary); }
.he-content.clamped { display: -webkit-box; -webkit-line-clamp: 8; line-clamp: 8; -webkit-box-orient: vertical; overflow: hidden; }
.he-more { margin-top: 4px; padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 13px; cursor: pointer; }

.he-images { display: grid; gap: 4px; margin-top: 10px; }
.he-images.n1 { grid-template-columns: minmax(0, 1fr); max-width: 560px; }
.he-images.n2, .he-images.n4 { grid-template-columns: repeat(2, minmax(0, 1fr)); max-width: 560px; }
.he-images.n3, .he-images.nmany { grid-template-columns: repeat(3, minmax(0, 1fr)); max-width: 600px; }
.he-img { position: relative; padding: 0; border: 0; background: var(--bg-sub); cursor: zoom-in; overflow: hidden; }
.he-images:not(.n1) .he-img { aspect-ratio: 1; }
.he-img img { display: block; width: 100%; height: 100%; object-fit: cover; }
.he-images.n1 .he-img img { height: auto; max-height: 480px; object-fit: contain; background: var(--bg-sub); }
.he-img-more { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: rgba(0, 0, 0, .5); color: #fff; font-size: 20px; font-weight: 700; }

.he-foot { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 10px; margin-top: 12px; font-size: 12px; color: var(--text-secondary); }
.he-author { display: inline-flex; align-items: center; gap: 6px; color: var(--text-primary); }
.he-avatar { width: 24px; height: 24px; object-fit: cover; display: block; }
.he-avatar-empty { display: flex; align-items: center; justify-content: center; background: var(--bg-sub); font-size: 12px; font-weight: 700; color: var(--text-secondary); }
.he-author-name { font-weight: 600; }

.he-actions { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; margin-top: 10px; padding-top: 10px; border-top: 1px dashed var(--border-line); }
.he-reacts { display: flex; flex-wrap: wrap; gap: 4px; }
.he-react { display: inline-flex; align-items: center; gap: 4px; padding: 3px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.he-react:hover:not(:disabled) { border-color: var(--accent-color); }
.he-react.on { border-color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 12%, transparent); color: var(--text-primary); }
.he-react b { font-variant-numeric: tabular-nums; color: var(--text-primary); }
.he-ops { display: flex; gap: 4px; }
.he-op { padding: 3px 8px; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.he-op:hover { color: var(--accent-color); }
.he-op.danger:hover { color: #dc2626; }
.compact .he-actions { display: none; }
@media (max-width: 520px) { .he-react-label { display: none; } }
</style>
