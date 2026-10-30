<template>
  <section class="persona-panel" aria-labelledby="persona-title">
    <header class="pp-head">
      <h2 id="persona-title">我的 AI 人格</h2>
      <span v-if="state?.persona" class="pp-status" :class="{ on: form.enabled }">{{ form.enabled ? '已启用' : '未启用' }}</span>
    </header>
    <p class="pp-intro">接入你自己的 AI，让它以独立身份参加聊天。它的每条消息都会标注「AI」，并显示是你接入的。调用费用走你自己的 API。</p>

    <p v-if="loading" class="pp-muted" role="status">读取中…</p>
    <template v-else-if="state">
      <!-- AI 接口（与世界观「一键整理」等功能共用同一份配置） -->
      <details class="pp-block" :open="!state.aiConfig.configured">
        <summary>
          <span>① AI 接口</span>
          <em :class="state.aiConfig.configured ? 'ok' : 'warn'">{{ state.aiConfig.configured ? `已配置 · ${state.aiConfig.model}` : '未配置' }}</em>
        </summary>
        <p class="pp-muted">支持 OpenAI 兼容接口（DeepSeek、通义、Kimi、OpenAI 等）。Key 加密保存，只用于你自己的 AI。</p>
        <label class="pp-field">接口地址<input v-model.trim="api.baseUrl" placeholder="https://api.deepseek.com" /></label>
        <label class="pp-field">模型<input v-model.trim="api.model" placeholder="deepseek-chat" /></label>
        <label class="pp-field">API Key<input v-model.trim="api.apiKey" type="password" autocomplete="off" :placeholder="api.maskedKey || 'sk-...'" /></label>
        <button type="button" class="pp-btn" :disabled="savingApi" @click="saveApi">{{ savingApi ? '保存中…' : '保存接口' }}</button>
      </details>

      <!-- 人设 -->
      <div class="pp-block">
        <h3>② 人设</h3>
        <label class="pp-switch"><input v-model="form.enabled" type="checkbox" :disabled="!state.aiConfig.configured" /> 启用，让它出现在聊天室里</label>
        <label class="pp-field">名字<small>大家用 @名字 呼叫它，不能有空格</small><input v-model.trim="form.name" maxlength="20" placeholder="例如：莱茵小助手" /></label>
        <label class="pp-field">头像链接<small>可选，留空用默认图标</small><input v-model.trim="form.avatar" placeholder="https://..." /></label>
        <label class="pp-field">人设描述<small>性格、说话方式、背景故事、喜欢聊什么（{{ form.prompt.length }}/4000）</small>
          <textarea v-model="form.prompt" rows="6" maxlength="4000" placeholder="你是一位来自莱茵生命的研究员，说话严谨但偶尔冷幽默，喜欢聊材料学和猫……"></textarea>
        </label>
      </div>

      <!-- 发言方式：由主人决定 -->
      <div class="pp-block">
        <h3>③ 什么时候发言</h3>
        <label class="pp-radio"><input v-model="form.replyMode" type="radio" value="mention" /> 只在被 @ 或有人回复它时说话</label>
        <label class="pp-radio"><input v-model="form.replyMode" type="radio" value="active" /> 也会主动接话</label>
        <div v-if="form.replyMode === 'active'" class="pp-sub">
          <label class="pp-field">接话概率 <b>{{ form.autoChance }}%</b><input v-model.number="form.autoChance" type="range" min="1" max="100" /></label>
          <label class="pp-field">冷却时间<small>同一频道两次主动发言至少间隔</small>
            <select v-model.number="form.cooldownSeconds">
              <option :value="30">30 秒</option><option :value="60">1 分钟</option><option :value="180">3 分钟</option>
              <option :value="600">10 分钟</option><option :value="1800">30 分钟</option>
            </select>
          </label>
        </div>
        <label class="pp-switch"><input v-model="form.replyToAi" type="checkbox" /> 也回应其他 AI（AI 互聊）</label>
        <p class="pp-muted">连续 4 条都是 AI 发言后，AI 会停下来等真人说话，不会无限聊下去。</p>
        <label class="pp-field">每天最多发言<select v-model.number="form.dailyLimit">
          <option :value="10">10 次</option><option :value="30">30 次</option><option :value="50">50 次</option><option :value="100">100 次</option><option :value="300">300 次</option>
        </select></label>
        <fieldset class="pp-channels">
          <legend>在哪些频道发言<small>都不勾 = 所有允许 AI 的频道</small></legend>
          <label v-for="c in aiChannels" :key="c.id"><input v-model="form.channels" type="checkbox" :value="c.id" /> {{ c.icon }} {{ c.name }}</label>
        </fieldset>
      </div>

      <p v-if="message" class="pp-msg" :class="messageTone" role="status">{{ message }}</p>
      <p v-if="state.persona?.lastError" class="pp-msg bad" role="alert">上次发言失败：{{ state.persona.lastError }}</p>
      <div class="pp-actions">
        <button type="button" class="pp-btn primary" :disabled="saving" @click="save">{{ saving ? '保存中…' : '保存人设' }}</button>
        <button v-if="state.persona?.enabled && channelId" type="button" class="pp-btn" :disabled="speaking || !canSpeakHere" :title="canSpeakHere ? '' : '这个频道不允许你的 AI 发言'" @click="speak">让 TA 在本频道说一句</button>
      </div>
      <p v-if="state.persona" class="pp-muted">今天已发言 {{ state.usedToday }} / {{ form.dailyLimit }} 次</p>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { aiConfigApi } from '@/api/aiConfig'
import { chatApi, chatError, type ChatChannel, type MyPersona, type MyPersonaState } from '@/api/chat'

const props = defineProps<{ channels: ChatChannel[]; channelId: string }>()
const emit = defineEmits<{ saved: [] }>()

const loading = ref(true), saving = ref(false), savingApi = ref(false), speaking = ref(false)
const state = ref<MyPersonaState | null>(null)
const message = ref(''), messageTone = ref<'good' | 'bad'>('good')
const api = reactive({ baseUrl: 'https://api.deepseek.com', model: 'deepseek-chat', apiKey: '', maskedKey: '' })
const form = reactive<MyPersona>({
  enabled: false, name: '', avatar: '', prompt: '', replyMode: 'mention', autoChance: 20, cooldownSeconds: 180,
  replyToAi: false, dailyLimit: 50, channels: [],
})

const aiChannels = computed(() => props.channels.filter(c => c.allowAi))
const canSpeakHere = computed(() => {
  const ch = props.channels.find(c => c.id === props.channelId)
  return Boolean(ch?.allowAi) && (!form.channels.length || form.channels.includes(props.channelId))
})

function say(text: string, tone: 'good' | 'bad' = 'good') { message.value = text; messageTone.value = tone }

async function load() {
  loading.value = true
  try {
    const [s, cfg] = await Promise.all([chatApi.myPersona(), aiConfigApi.get().catch(() => null)])
    state.value = s
    if (cfg) Object.assign(api, { baseUrl: cfg.baseUrl, model: cfg.model, maskedKey: cfg.maskedKey, apiKey: '' })
    if (s.persona) Object.assign(form, { ...s.persona, avatar: s.persona.avatar ?? '', channels: [...s.persona.channels] })
  } catch (e) {
    say(chatError(e, '读取 AI 人格失败，请稍后重试'), 'bad')
  } finally { loading.value = false }
}

async function saveApi() {
  savingApi.value = true
  try {
    await aiConfigApi.save({ baseUrl: api.baseUrl, model: api.model, apiKey: api.apiKey || undefined })
    say('AI 接口已保存')
    await load()
  } catch (e) { say(chatError(e, '保存接口失败'), 'bad') } finally { savingApi.value = false }
}

async function save() {
  if (!form.name) { say('先给你的 AI 起个名字', 'bad'); return }
  saving.value = true
  try {
    const r = await chatApi.savePersona({ ...form, avatar: form.avatar || null })
    say(r.message)
    await load()
    emit('saved')
  } catch (e) { say(chatError(e, '保存失败'), 'bad') } finally { saving.value = false }
}

async function speak() {
  speaking.value = true
  try { say((await chatApi.speak(props.channelId)).message) }
  catch (e) { say(chatError(e, '没能让它开口'), 'bad') }
  finally { window.setTimeout(() => { speaking.value = false }, 3000) }
}

defineExpose({ reload: load, notify: say })
onMounted(load)
</script>

<style scoped>
.persona-panel { display: grid; gap: 12px; font-size: 13px; }
.pp-head { display: flex; align-items: center; gap: 8px; padding-right: 36px; }
.pp-head h2 { margin: 0; font-size: 16px; }
.pp-status { padding: 2px 8px; border: 1px solid var(--border-line); color: var(--text-secondary); font-size: 12px; }
.pp-status.on { border-color: var(--signal-mint); color: var(--signal-mint); }
.pp-intro, .pp-muted { margin: 0; color: var(--text-secondary); line-height: 1.6; font-size: 12px; }
.pp-block { display: grid; gap: 10px; padding: 12px; border: 1px solid var(--border-line); background: var(--paper-card); }
.pp-block h3 { margin: 0; font-size: 13px; }
.pp-block summary { display: flex; justify-content: space-between; gap: 8px; cursor: pointer; font-weight: 600; }
.pp-block summary em { font-style: normal; font-weight: 400; font-size: 12px; }
.pp-block summary em.ok { color: var(--success); }
.pp-block summary em.warn { color: var(--warning, #b7791f); }
.pp-block[open] summary { margin-bottom: 4px; }
.pp-field { display: grid; gap: 4px; color: var(--text-primary); }
.pp-field small, .pp-channels small { color: var(--text-secondary); font-size: 11px; }
.pp-field input:not([type=range]), .pp-field textarea, .pp-field select { width: 100%; box-sizing: border-box; padding: 7px 9px; border: 1px solid var(--border-line); background: var(--bg-main, var(--paper-card)); color: var(--text-primary); font: inherit; }
.pp-field textarea { resize: vertical; line-height: 1.6; }
.pp-field input:focus-visible, .pp-field textarea:focus-visible, .pp-field select:focus-visible { outline: 2px solid var(--accent-color); outline-offset: -1px; }
.pp-switch, .pp-radio { display: flex; align-items: center; gap: 8px; cursor: pointer; }
.pp-sub { display: grid; gap: 10px; padding-left: 22px; }
.pp-channels { display: grid; gap: 6px; margin: 0; padding: 8px 10px; border: 1px dashed var(--border-line); }
.pp-channels legend { padding: 0 4px; display: flex; gap: 6px; align-items: baseline; }
.pp-channels label { display: flex; gap: 6px; align-items: center; cursor: pointer; }
.pp-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.pp-btn { padding: 8px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; cursor: pointer; }
.pp-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.pp-btn.primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
.pp-btn:disabled { opacity: .55; cursor: default; }
.pp-msg { margin: 0; padding: 8px 10px; border-left: 3px solid var(--success); background: var(--paper-card); }
.pp-msg.bad { border-left-color: var(--danger); color: var(--danger); }
</style>
