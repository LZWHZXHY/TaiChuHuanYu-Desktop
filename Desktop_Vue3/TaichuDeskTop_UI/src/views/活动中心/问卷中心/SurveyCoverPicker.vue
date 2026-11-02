<!-- 问卷封面：上传（带百分比）、更换、移除。v-model 是封面图地址（空字符串 = 没有封面，列表里显示默认图）。
     问卷编辑页和问卷管理里「改封面」共用。 -->
<template>
  <div class="scp">
    <div class="scp-preview" :class="{ empty: !modelValue }">
      <img v-if="modelValue" :src="modelValue" alt="问卷封面预览" />
      <span v-else class="scp-empty">还没有封面<small>列表里会显示默认图</small></span>
      <div v-if="uploading" class="scp-progress" role="status" aria-live="polite">
        <span>上传中 {{ percent }}%</span>
        <i class="scp-bar"><b :style="{ width: percent + '%' }"></b></i>
      </div>
    </div>
    <div class="scp-actions">
      <button type="button" class="scp-btn main" :disabled="uploading || disabled" @click="pick">{{ modelValue ? '更换封面' : '上传封面' }}</button>
      <button v-if="modelValue" type="button" class="scp-btn" :disabled="uploading || disabled" @click="emit('update:modelValue', '')">移除</button>
      <small class="scp-hint">建议横图（2:1 左右），不超过 5MB</small>
    </div>
    <p v-if="error" class="scp-err" role="alert">{{ error }}</p>
    <input ref="input" type="file" accept="image/*" hidden @change="onPicked" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useCos } from '@/composables/useCos'

defineProps<{ modelValue: string; disabled?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const { uploadFile } = useCos()
const input = ref<HTMLInputElement | null>(null)
const uploading = ref(false)
const percent = ref(0)
const error = ref('')
const MAX = 5 * 1024 * 1024

const pick = () => input.value?.click()

async function onPicked(e: Event) {
  const el = e.target as HTMLInputElement
  const file = el.files?.[0]
  el.value = ''
  if (!file) return
  error.value = ''
  if (!file.type.startsWith('image/')) { error.value = '请选择图片文件'; return }
  if (file.size > MAX) { error.value = '图片不能超过 5MB'; return }
  uploading.value = true
  percent.value = 0
  try {
    const res = await uploadFile(file, 'survey-covers', p => { percent.value = Math.round(p) })
    emit('update:modelValue', res.url)
  } catch {
    error.value = '上传失败，请稍后再试'
  } finally { uploading.value = false }
}
</script>

<style scoped>
.scp { display: grid; gap: 10px; }
.scp-preview { position: relative; width: 100%; max-width: 480px; aspect-ratio: 2 / 1; overflow: hidden; border: 1px solid var(--border-line); background: var(--bg-sub); }
.scp-preview.empty { border-style: dashed; }
.scp-preview img { width: 100%; height: 100%; object-fit: cover; display: block; }
.scp-empty { position: absolute; inset: 0; display: grid; place-content: center; gap: 4px; text-align: center; color: var(--text-secondary); font-size: 14px; }
.scp-empty small { font-size: 12px; opacity: .8; }
.scp-progress { position: absolute; left: 12px; right: 12px; bottom: 12px; padding: 8px 10px; background: color-mix(in srgb, var(--paper-card) 90%, transparent); font-size: 12px; display: grid; gap: 6px; }
.scp-bar { display: block; height: 4px; background: var(--border-line); }
.scp-bar b { display: block; height: 100%; background: var(--accent-color); transition: width .2s; }
.scp-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.scp-btn { padding: 6px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.scp-btn.main { border-color: var(--accent-color); color: var(--accent-color); }
.scp-btn:hover:not(:disabled) { border-color: var(--accent-color); background: var(--accent-soft); }
.scp-btn:disabled { opacity: .5; cursor: not-allowed; }
.scp-btn:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.scp-hint { color: var(--text-secondary); font-size: 12px; }
.scp-err { margin: 0; color: var(--danger); font-size: 13px; }
</style>
