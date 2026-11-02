<!-- 小说封面：上传（带百分比）、更换、移除；v-model 是图片地址，空字符串 = 没有封面。新建小说和编辑小说信息共用。 -->
<template>
  <div class="ncp">
    <div class="ncp-preview" :class="{ empty: !modelValue }">
      <img v-if="modelValue" :src="modelValue" alt="封面预览" />
      <span v-else class="ncp-empty">{{ (fallbackText || '书').slice(0, 1) }}</span>
      <div v-if="uploading" class="ncp-progress" role="status" aria-live="polite">
        <span>{{ percent }}%</span><i><b :style="{ width: percent + '%' }"></b></i>
      </div>
    </div>
    <div class="ncp-side">
      <div class="ncp-actions">
        <button type="button" class="ncp-btn main" :disabled="uploading" @click="input?.click()">{{ modelValue ? '更换封面' : '上传封面' }}</button>
        <button v-if="modelValue" type="button" class="ncp-btn" :disabled="uploading" @click="emit('update:modelValue', '')">移除</button>
      </div>
      <small class="ncp-hint">竖图（3:4 左右）效果最好，不超过 5MB</small>
      <p v-if="error" class="ncp-err" role="alert">{{ error }}</p>
    </div>
    <input ref="input" type="file" accept="image/*" hidden @change="onPicked" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useCos } from '@/composables/useCos'

defineProps<{ modelValue: string; fallbackText?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const { uploadFile } = useCos()
const input = ref<HTMLInputElement | null>(null)
const uploading = ref(false)
const percent = ref(0)
const error = ref('')

async function onPicked(e: Event) {
  const el = e.target as HTMLInputElement
  const file = el.files?.[0]
  el.value = ''
  if (!file) return
  error.value = ''
  if (!file.type.startsWith('image/')) { error.value = '请选择图片文件'; return }
  if (file.size > 5 * 1024 * 1024) { error.value = '图片不能超过 5MB'; return }
  uploading.value = true
  percent.value = 0
  try {
    const res = await uploadFile(file, 'novel-covers', p => { percent.value = Math.round(p) })
    emit('update:modelValue', res.url)
  } catch { error.value = '上传失败，请稍后再试' } finally { uploading.value = false }
}
</script>

<style scoped>
.ncp { display: flex; gap: 14px; align-items: flex-start; }
.ncp-preview { position: relative; flex-shrink: 0; width: 96px; aspect-ratio: 3 / 4; overflow: hidden; border: 1px solid var(--border-line); background: linear-gradient(160deg, var(--accent-soft), var(--bg-sub)); }
.ncp-preview.empty { border-style: dashed; }
.ncp-preview img { width: 100%; height: 100%; object-fit: cover; display: block; }
.ncp-empty { position: absolute; inset: 0; display: grid; place-items: center; font-size: 2rem; font-weight: 800; color: var(--accent-color); opacity: .6; }
.ncp-progress { position: absolute; left: 6px; right: 6px; bottom: 6px; display: grid; gap: 4px; padding: 4px 6px; background: color-mix(in srgb, var(--paper-card) 90%, transparent); font-size: 11px; color: var(--text-primary); }
.ncp-progress i { display: block; height: 3px; background: var(--border-line); }
.ncp-progress b { display: block; height: 100%; background: var(--accent-color); transition: width .2s; }
.ncp-side { display: grid; gap: 6px; min-width: 0; }
.ncp-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.ncp-btn { padding: 6px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.ncp-btn.main { border-color: var(--accent-color); color: var(--accent-color); }
.ncp-btn:hover:not(:disabled) { background: var(--accent-soft); }
.ncp-btn:disabled { opacity: .5; cursor: not-allowed; }
.ncp-hint { color: var(--text-secondary); font-size: 12px; }
.ncp-err { margin: 0; color: var(--danger); font-size: 12px; }
</style>
