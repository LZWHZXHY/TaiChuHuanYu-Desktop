<template>
  <div class="create-page content-page workbench-skin editor-surface community-surface tone-chai">
    <header class="content-header studio-heading"><SectionTrace />
      <RouterLink to="/works">← 返回作品</RouterLink>
      <h1>上传作品</h1>
      <p class="intro">上传火柴人动画（视频）或单张作品图，分享给柴圈同好。</p>
    </header>

    <div class="form-panel content-form">
      <div class="form-group">
        <label>作品标题 <span class="required">*</span></label>
        <input v-model="form.title" class="text-input" maxlength="200" placeholder="为作品起个标题" />
      </div>

      <div class="form-group">
        <label>作品说明</label>
        <textarea v-model="form.description" class="text-input" rows="4" maxlength="2000" placeholder="介绍你的作品（可选）"></textarea>
      </div>

      <div class="form-group">
        <label>标签（逗号分隔，可选）</label>
        <input v-model="tagsText" class="text-input" maxlength="200" placeholder="例如：打斗，剧情，练习" />
      </div>

      <div class="form-group">
        <label>作品文件（视频或图片）<span class="required">*</span></label>
        <div v-if="mediaUploading" class="upload-hint">上传中 {{ progress }}% …</div>
        <div v-else-if="form.mediaUrl" class="media-preview">
          <video v-if="form.mediaType === 'video'" :src="form.mediaUrl" controls class="preview-media" />
          <img v-else :src="form.mediaUrl" class="preview-media" alt="作品预览" />
          <button class="btn-line" @click="form.mediaUrl = ''; form.mediaType = 'video'">移除</button>
        </div>
        <button v-else class="upload-box" @click="mediaInput?.click()">点击上传视频 / 图片</button>
        <input ref="mediaInput" type="file" accept="video/*,image/*" class="hidden-input" @change="onMediaSelect" />
      </div>

      <div class="form-group" v-if="form.mediaType === 'video'">
        <label>封面图（可选，不传则无封面）</label>
        <div v-if="coverUploading" class="upload-hint">封面上传中…</div>
        <div v-else-if="form.coverUrl" class="cover-preview">
          <img :src="form.coverUrl" alt="封面" />
          <button class="btn-line" @click="form.coverUrl = ''">移除封面</button>
        </div>
        <button v-else class="upload-box" @click="coverInput?.click()">上传封面图</button>
        <input ref="coverInput" type="file" accept="image/*" class="hidden-input" @change="onCoverSelect" />
      </div>

      <p v-if="error" class="error-text" role="alert">{{ error }}</p>

      <div class="actions">
        <button class="btn-line" :disabled="busy" @click="submit(false)">{{ busy ? '处理中…' : '保存草稿' }}</button>
        <button class="btn-primary" :disabled="busy" @click="submit(true)">{{ busy ? '处理中…' : '发布' }}</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useCos } from '@/composables/useCos'
import { chaiWorkApi } from '@/api/chaiWork'

const router = useRouter()
const { uploadFile, isUploading, progress } = useCos()

const form = ref<{ title: string; description: string; mediaUrl: string; coverUrl: string; mediaType: string }>({
  title: '', description: '', mediaUrl: '', coverUrl: '', mediaType: 'video'
})
const tagsText = ref('')
const busy = ref(false)
const error = ref('')
const mediaInput = ref<HTMLInputElement | null>(null)
const coverInput = ref<HTMLInputElement | null>(null)
const mediaUploading = ref(false)
const coverUploading = ref(false)

async function onMediaSelect(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  const isVideo = file.type.startsWith('video/')
  form.value.mediaType = isVideo ? 'video' : 'image'
  mediaUploading.value = true
  try {
    const res = await uploadFile(file, 'chai-works')
    form.value.mediaUrl = res.url
  } catch (err: any) {
    error.value = err.message || '上传失败，请重试'
  } finally {
    mediaUploading.value = false
    if (input) input.value = ''
  }
}

async function onCoverSelect(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  coverUploading.value = true
  try {
    const res = await uploadFile(file, 'chai-works/covers')
    form.value.coverUrl = res.url
  } catch (err: any) {
    error.value = err.message || '封面上传失败，请重试'
  } finally {
    coverUploading.value = false
    if (input) input.value = ''
  }
}

async function submit(publish: boolean) {
  if (busy.value) return
  error.value = ''
  if (!form.value.title.trim()) { error.value = '请填写标题'; return }
  if (!form.value.mediaUrl) { error.value = '请先上传作品文件'; return }
  busy.value = true
  try {
    const tags = tagsText.value.split(/[,，]/).map(t => t.trim()).filter(Boolean)
    const work = await chaiWorkApi.create({
      title: form.value.title.trim(),
      description: form.value.description.trim() || undefined,
      mediaUrl: form.value.mediaUrl,
      coverUrl: form.value.coverUrl || undefined,
      mediaType: form.value.mediaType,
      status: publish ? 'published' : 'draft',
      tags,
    })
    router.replace(`/works/${work.id}`)
  } catch (err: any) {
    error.value = err.friendlyMessage || '提交失败，请重试'
  } finally {
    busy.value = false
  }
}
</script>

<style scoped>
.content-header a { color: var(--accent-color); text-decoration: none; font-size: .85rem; }
.content-header h1 { margin: 10px 0 6px; }
.intro { color: var(--text-secondary); }
.form-panel { max-width: 680px; margin: 20px 0; padding: 24px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0; display: grid; gap: 18px; }
.form-group { display: grid; gap: 8px; }
.form-group label { font-size: .85rem; color: var(--text-secondary); }
.required { color: var(--accent-color); }
.text-input { width: 100%; padding: 10px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font-size: .9rem; box-sizing: border-box; resize: vertical; }
.upload-box { padding: 24px; border: 1px dashed var(--border-line); border-radius: 0; background: var(--paper-sub); color: var(--text-secondary); cursor: pointer; font-size: .9rem; }
.upload-box:hover { border-color: var(--accent-color); color: var(--accent-color); }
.hidden-input { display: none; }
.upload-hint { color: var(--accent-color); font-size: .85rem; }
.media-preview { display: grid; gap: 10px; }
.preview-media { width: 100%; max-height: 320px; border-radius: 0; object-fit: contain; background: #000; }
.cover-preview { display: grid; gap: 10px; }
.cover-preview img { width: 160px; height: 90px; object-fit: cover; border-radius: 0; }
.error-text { color: var(--accent-color); font-size: .85rem; }
.actions { display: flex; gap: 12px; justify-content: flex-end; }
.btn-line { padding: 9px 16px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; }
.btn-primary { padding: 9px 22px; border: none; border-radius: 0; background: var(--accent-color); color: var(--on-accent); cursor: pointer; }
.btn-primary:disabled, .btn-line:disabled { opacity: .5; cursor: default; }
</style>
