<!-- AuthorPicker.vue：后台给素材填「原作者」（社区投稿被采用的图）。按用户名查找、点选；作者会自动免费拥有这件素材。 -->
<template>
  <div class="author-picker">
    <div v-if="modelValue" class="ap-current">
      <span>原作者：<b>{{ modelValue.name || '（已选）' }}</b></span>
      <button type="button" class="ap-link" @click="emit('update:modelValue', null)">清除</button>
    </div>
    <div v-else class="ap-find">
      <input v-model.trim="q" placeholder="原作者用户名（社区投稿时填）" @keydown.enter.prevent="find" />
      <button type="button" :disabled="!q || busy" @click="find">{{ busy ? '查找中…' : '查找' }}</button>
    </div>
    <ul v-if="!modelValue && hits.length" class="ap-hits">
      <li v-for="u in hits" :key="u.userId"><button type="button" @click="pick(u)">{{ u.name }}</button></li>
    </ul>
    <small v-if="msg" class="ap-msg">{{ msg }}</small>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

export interface AuthorRef { id: string; name: string }
const props = defineProps<{ modelValue: AuthorRef | null; search: (q: string) => Promise<{ userId: string; name: string }[]> }>()
const emit = defineEmits<{ 'update:modelValue': [value: AuthorRef | null] }>()

const q = ref('')
const busy = ref(false)
const hits = ref<{ userId: string; name: string }[]>([])
const msg = ref('')

async function find() {
  if (!q.value) return
  busy.value = true
  msg.value = ''
  try {
    hits.value = await props.search(q.value)
    if (!hits.value.length) msg.value = '没有找到这个用户名'
    else if (hits.value.length === 1) pick(hits.value[0]!)
  } catch { msg.value = '查找失败' } finally { busy.value = false }
}
function pick(u: { userId: string; name: string }) {
  emit('update:modelValue', { id: u.userId, name: u.name })
  hits.value = []
  q.value = ''
}
</script>

<style scoped>
.author-picker { display: grid; gap: 6px; }
.ap-find { display: flex; gap: 6px; }
.ap-find input { flex: 1; min-width: 0; padding: 7px 9px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 13px; }
.ap-find button, .ap-hits button { padding: 5px 10px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.ap-find button:disabled { opacity: .5; }
.ap-current { display: flex; align-items: center; gap: 10px; padding: 6px 10px; border: 1px solid var(--accent-color); font-size: 13px; }
.ap-current b { color: var(--accent-color); }
.ap-link { padding: 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; text-decoration: underline; cursor: pointer; }
.ap-hits { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 6px; }
.ap-msg { color: var(--text-secondary); font-size: 12px; }
</style>
