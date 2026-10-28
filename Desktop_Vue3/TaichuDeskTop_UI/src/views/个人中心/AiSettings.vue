<template>
  <section class="ai-settings content-panel">
    <div class="section-heading">
      <div>
        <h2>AI 助手</h2>
        <p>填入你自己的 AI 接口（OpenAI 兼容），用于世界观 / 人物的一键整理。Key 加密存储，界面只显示后 4 位。</p>
      </div>
    </div>

    <p v-if="loading" class="state">加载中…</p>
    <div v-else class="form">
      <label class="field">
        <span>接口地址（Base URL）</span>
        <input v-model="form.baseUrl" placeholder="https://api.deepseek.com" />
        <small>需为 OpenAI 兼容接口：DeepSeek / Moonshot / Kimi / 通义 / 本地 Ollama 等</small>
      </label>

      <label class="field">
        <span>模型名</span>
        <input v-model="form.model" placeholder="deepseek-chat" />
      </label>

      <label class="field">
        <span>API Key</span>
        <input v-model="form.apiKey" type="password" :placeholder="maskedKey ? `已配置 ${maskedKey}` : 'sk-…'" autocomplete="off" />
        <small>{{ maskedKey ? `当前已配置（${maskedKey}）。留空则保留现有 Key。` : '尚未配置，填写后保存。' }}</small>
      </label>

      <div class="actions">
        <button class="btn primary" :disabled="saving" @click="save">{{ saving ? '保存中…' : '保存' }}</button>
        <span v-if="savedMsg" class="saved">{{ savedMsg }}</span>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { aiConfigApi } from '@/api/aiConfig'

const loading = ref(false)
const saving = ref(false)
const savedMsg = ref('')
const maskedKey = ref('')

const form = ref({ baseUrl: 'https://api.deepseek.com', model: 'deepseek-chat', apiKey: '' })

async function load() {
  loading.value = true
  try {
    const info = await aiConfigApi.get()
    form.value.baseUrl = info.baseUrl || 'https://api.deepseek.com'
    form.value.model = info.model || 'deepseek-chat'
    maskedKey.value = info.maskedKey
  } catch { /* ignore */ } finally { loading.value = false }
}

async function save() {
  if (!form.value.baseUrl.trim()) { alert('请填写接口地址'); return }
  saving.value = true
  savedMsg.value = ''
  try {
    await aiConfigApi.save({ baseUrl: form.value.baseUrl.trim(), model: form.value.model.trim(), apiKey: form.value.apiKey.trim() || undefined })
    savedMsg.value = '已保存'
    await load()
    form.value.apiKey = ''
  } catch (e: any) {
    alert(e.friendlyMessage || '保存失败')
  } finally { saving.value = false }
}

onMounted(load)
</script>

<style scoped>
.ai-settings { padding: 24px; }
.section-heading { margin-bottom: 20px; }
.section-heading h2 { margin: 0 0 8px; font-size: 20px; }
.section-heading p { font-size: 13px; color: var(--text-secondary); line-height: 1.7; margin: 0; }
.state { text-align: center; padding: 32px; color: var(--text-secondary); }
.form { display: grid; gap: 18px; max-width: 520px; }
.field { display: grid; gap: 6px; font-size: .85rem; }
.field span { font-weight: 500; color: var(--text-primary); }
.field input { padding: 10px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font-size: .9rem; }
.field input:focus { outline: none; border-color: var(--accent-color); }
.field small { color: var(--text-secondary); font-size: .74rem; line-height: 1.6; }
.actions { display: flex; align-items: center; gap: 14px; }
.btn { padding: 9px 20px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .85rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: .5; cursor: default; }
.saved { color: var(--success); font-size: .82rem; }
</style>
