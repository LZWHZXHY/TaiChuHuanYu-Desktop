<!-- AvatarPicker.vue：更换头像。上传自己的图片（只能是静态图，GIF / 动图会被转成第一帧或拒绝），或者选用官方头像（静态 / 动图，交易行在卖的要先兑换）。 -->
<template>
  <Teleport to="body">
    <div class="ap-mask" role="dialog" aria-modal="true" aria-label="更换头像" @click.self="close">
      <div class="ap">
        <header class="ap-head">
          <div>
            <p class="eyebrow">AVATAR / 更换头像</p>
            <h2>选择你的头像</h2>
          </div>
          <button type="button" class="x" aria-label="关闭" @click="close">✕</button>
        </header>

        <section class="ap-upload">
          <AvatarFrame :user-id="user.userId">
            <span class="ap-current"><img v-if="current" :src="current" alt="当前头像" /><b v-else>{{ user.userInfo?.username?.[0] ?? '?' }}</b></span>
          </AvatarFrame>
          <div class="ap-upload-text">
            <strong>上传自己的图片</strong>
            <small>支持 JPG、PNG、WebP，会自动裁成方形并压缩。不能上传 GIF 或动图——动图头像请在下面选官方的。</small>
            <input ref="fileInput" type="file" accept="image/jpeg,image/png,image/webp" hidden @change="onPick" />
            <button type="button" class="btn-primary" :disabled="busy" @click="fileInput?.click()">{{ busy && stage === 'upload' ? '上传中…' : busy && stage === 'convert' ? '处理中…' : '上传图片' }}</button>
          </div>
        </section>

        <section class="ap-official">
          <div class="ap-sec-head">
            <h3>官方头像</h3>
            <div class="ap-filter" role="tablist" aria-label="筛选">
              <button v-for="f in FILTERS" :key="f.id" type="button" role="tab" :aria-selected="filter === f.id" :class="{ on: filter === f.id }" @click="filter = f.id">{{ f.label }}</button>
            </div>
          </div>
          <p v-if="loading" class="muted">读取中…</p>
          <p v-else-if="!shown.length" class="muted">{{ list.length ? '这一类还没有头像。' : '官方头像还在准备中。' }}</p>
          <ul v-else class="ap-grid">
            <li v-for="a in shown" :key="a.id" :class="{ using: current === a.url }">
              <span class="ap-thumb"><img :src="a.url" :alt="a.name" loading="lazy" /><em v-if="a.animated" class="tag-anim">动图</em></span>
              <b>{{ a.name }}</b>
              <small v-if="a.author">作者 {{ a.author }}</small>
              <span v-if="current === a.url" class="state on">使用中</span>
              <RouterLink v-else-if="a.locked && !a.owned" :to="{ name: '交易行' }" class="state buy" @click="close">🔒 去交易行兑换</RouterLink>
              <button v-else type="button" class="state use" :disabled="busy" @click="useOfficial(a)">{{ a.locked ? '已拥有 · 换上' : '免费 · 换上' }}</button>
            </li>
          </ul>
        </section>

        <p v-if="error" class="err" role="alert">{{ error }}</p>
        <p v-if="message" class="ok" role="status">{{ message }}</p>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useUserStore } from '@/stores/user'
import AvatarFrame from '@/components/AvatarFrame.vue'
import { TradeApi, tradeError, type IOfficialAvatar } from '@/api/trade'

const emit = defineEmits<{ close: []; changed: [url: string] }>()
const user = useUserStore()

const FILTERS = [
  { id: 'all', label: '全部' },
  { id: 'static', label: '静态' },
  { id: 'animated', label: '动图' },
  { id: 'mine', label: '已拥有' },
] as const
type Filter = typeof FILTERS[number]['id']

const list = ref<IOfficialAvatar[]>([])
const loading = ref(true)
const filter = ref<Filter>('all')
const fileInput = ref<HTMLInputElement | null>(null)
const busy = ref(false)
const stage = ref<'' | 'convert' | 'upload'>('')
const error = ref(''), message = ref('')
const current = computed(() => user.userInfo?.avatar || '')

const shown = computed(() => list.value.filter(a =>
  filter.value === 'all' ? true
    : filter.value === 'static' ? !a.animated
      : filter.value === 'animated' ? a.animated
        : a.owned || !a.locked))

function say(text: string, isError = false) { message.value = isError ? '' : text; error.value = isError ? text : '' }
function close() { if (!busy.value) emit('close') }
const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') close() }

function applyAvatar(url: string) {
  if (user.userInfo) user.setUser({ ...user.userInfo, avatar: url })
  emit('changed', url)
}

/** 读第一帧，居中裁成方形，缩到 512 以内，转成静态 WebP（不支持时用 JPG） */
async function toStaticSquare(file: File): Promise<File> {
  const bitmap = await createImageBitmap(file)
  try {
    const side = Math.min(bitmap.width, bitmap.height)
    const size = Math.min(512, side)
    const canvas = document.createElement('canvas')
    canvas.width = canvas.height = size
    const ctx = canvas.getContext('2d')
    if (!ctx) throw new Error('canvas')
    ctx.drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, size, size)
    const toBlob = (type: string) => new Promise<Blob | null>(resolve => canvas.toBlob(resolve, type, 0.9))
    const blob = (await toBlob('image/webp')) ?? (await toBlob('image/jpeg'))
    if (!blob) throw new Error('encode')
    return new File([blob], `avatar.${blob.type === 'image/webp' ? 'webp' : 'jpg'}`, { type: blob.type })
  } finally { bitmap.close() }
}

async function onPick(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  if (file.type === 'image/gif') return say('不能上传 GIF 头像。动图头像可以在下面选官方的，或者去交易行兑换。', true)
  if (!/^image\/(jpeg|png|webp)$/.test(file.type)) return say('只支持 JPG、PNG、WebP 图片', true)
  if (file.size > 30 * 1024 * 1024) return say('图片太大了（超过 30MB）', true)
  busy.value = true
  say('')
  try {
    stage.value = 'convert'
    const stat = await toStaticSquare(file).catch(() => null)
    if (!stat) throw new Error('这张图片读不出来，换一张试试')
    stage.value = 'upload'
    const r = await TradeApi.uploadAvatar(stat)
    applyAvatar(r.avatar)
    say('头像已更新')
  } catch (e) {
    say(e instanceof Error && !('response' in e) ? e.message : tradeError(e, '上传失败，请稍后再试'), true)
  } finally { busy.value = false; stage.value = '' }
}

async function useOfficial(a: IOfficialAvatar) {
  busy.value = true
  say('')
  try {
    const r = await TradeApi.useAvatar(a.id)
    applyAvatar(r.avatar)
    say(`已换上「${a.name}」`)
  } catch (e) { say(tradeError(e, '更换失败，请稍后再试'), true) } finally { busy.value = false }
}

onMounted(async () => {
  window.addEventListener('keydown', onKey)
  try { list.value = await TradeApi.avatars() } catch { list.value = [] } finally { loading.value = false }
})
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
</script>

<style scoped>
.ap-mask { position: fixed; inset: 0; z-index: 3200; display: grid; place-items: center; padding: 16px; background: rgb(8 12 16 / 62%); }
.ap { display: grid; gap: 18px; width: min(760px, 100%); max-height: 100%; overflow-y: auto; padding: 18px 20px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); }
.ap-head { display: flex; justify-content: space-between; align-items: flex-start; }
.eyebrow { margin: 0 0 4px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.ap-head h2 { margin: 0; font-size: 20px; }
.x { width: 32px; height: 32px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); cursor: pointer; }
.ap-upload { display: flex; align-items: center; gap: 24px; padding: 18px 20px; border: 1px dashed var(--border-line); background: var(--bg-main); }
.ap-current { display: grid; place-items: center; width: 84px; height: 84px; overflow: hidden; background: linear-gradient(135deg, var(--accent-color), var(--signal-mint, #2dd4bf)); color: #fff; font-size: 30px; }
.ap-current img { width: 100%; height: 100%; object-fit: cover; }
.ap-upload-text { display: grid; gap: 6px; justify-items: start; }
.ap-upload-text small { color: var(--text-secondary); font-size: 12px; line-height: 1.6; }
.btn-primary { padding: 7px 16px; border: 0; background: var(--accent-color); color: var(--on-accent); font: inherit; font-weight: 600; cursor: pointer; }
.btn-primary:disabled { opacity: .55; cursor: wait; }
.ap-official { display: grid; gap: 10px; }
.ap-sec-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 10px; }
.ap-sec-head h3 { margin: 0; font-size: 15px; }
.ap-filter { display: flex; gap: 4px; }
.ap-filter button { padding: 3px 10px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.ap-filter button.on { border-color: var(--accent-color); color: var(--accent-color); }
.ap-grid { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(120px, 1fr)); gap: 10px; }
.ap-grid li { display: grid; justify-items: center; gap: 4px; padding: 10px 8px; border: 1px solid var(--border-line); background: var(--bg-main); text-align: center; }
.ap-grid li.using { border-color: var(--accent-color); box-shadow: inset 0 -3px 0 var(--accent-color); }
.ap-thumb { position: relative; display: block; width: 72px; height: 72px; overflow: hidden; background: var(--bg-sub); }
.ap-thumb img { width: 100%; height: 100%; object-fit: cover; }
.tag-anim { position: absolute; right: 0; bottom: 0; padding: 0 4px; background: var(--accent-color); color: var(--on-accent, #fff); font-size: 10px; font-style: normal; }
.ap-grid b { font-size: 13px; }
.ap-grid small { color: var(--text-secondary); font-size: 11px; }
.state { margin-top: 2px; padding: 3px 8px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 11px; text-decoration: none; cursor: pointer; }
.state.on { border-color: var(--accent-color); color: var(--accent-color); cursor: default; }
.state.buy { color: var(--text-secondary); }
.state.use { border-color: var(--accent-color); color: var(--accent-color); }
.state:disabled { opacity: .55; }
.muted { margin: 0; color: var(--text-secondary); font-size: 13px; }
.err { margin: 0; color: var(--danger); font-size: 13px; }
.ok { margin: 0; color: var(--success); font-size: 13px; }
@media (max-width: 600px) { .ap-upload { flex-direction: column; align-items: flex-start; } }
</style>
