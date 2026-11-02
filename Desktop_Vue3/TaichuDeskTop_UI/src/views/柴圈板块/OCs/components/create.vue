<template>
  <div class="community-detail create-page studio-page workbench-skin editor-surface community-surface tone-chai">
    <header class="page-header studio-heading"><SectionTrace particles="chai" />
      <div>
        <span class="page-eyebrow">OC / NEW</span>
        <h1 class="page-title">创建新 OC</h1>
        <p class="page-subtitle">为柴圈注入新的灵魂 · 保存后可以在 OC 页把它加入你的世界观</p>
      </div>
      <router-link to="/ocs" class="back-link">← OC 画阁</router-link>
    </header>

    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    <CharacterForm :loading="store.loading" submit-text="创建角色" @submit="handleSubmit">
      <template #aside>
        <section class="world-hint">
          <span class="wh-tag">WORLD</span>
          <p>想把这个 OC 放进你的世界观？先创建，然后在 OC 详情页的「🌐 世界观」一栏点「关联到世界观」或「复制到我的世界观」。</p>
        </section>
      </template>
    </CharacterForm>
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { useRouter } from 'vue-router'
import { ref } from 'vue'
import { useStickmanStore } from '../stickman_store.ts'
import CharacterForm from '../../components/CharacterForm.vue'

const router = useRouter()
const store = useStickmanStore()
const error = ref('')

async function handleSubmit(data: any) {
  if (store.loading) return
  error.value = ''
  try {
    const result = await store.createCharacter(data)
    router.push(`/ocs/${result.id}`)
  } catch {
    error.value = '保存失败，请重试。已填写的内容仍保留。'
  }
}
</script>

<style scoped>
.studio-page { max-width: 1240px; margin: 0 auto; padding: 28px clamp(12px, 3vw, 32px) 64px; min-height: 100vh; }
.page-header { position: relative; overflow: hidden; display: flex; align-items: flex-end; gap: 16px; padding: 22px 24px 20px; margin-bottom: 22px; border: 1px solid var(--border-line); border-top: 3px solid var(--surface-color, var(--accent-color));
  background: linear-gradient(120deg, var(--paper-card), var(--surface-wash, var(--bg-sub))); }
.page-eyebrow { display: inline-block; margin-bottom: 8px; padding: 1px 8px; font: 10px/1.6 var(--font-mono, monospace); letter-spacing: .14em; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); }
.page-title { margin: 0 0 4px; font-family: var(--font-display, inherit); font-size: 26px; font-weight: 700; letter-spacing: .06em; color: var(--text-primary); }
.page-subtitle { margin: 0; font-size: 13px; color: var(--text-secondary); letter-spacing: .06em; }
.page-header > div { position: relative; z-index: 1; }
.back-link { margin-left: auto; position: relative; z-index: 1; padding: 6px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font-size: 13px; text-decoration: none; }
.back-link:hover { border-color: var(--accent-color); color: var(--accent-color); }
.form-error { margin: 0 0 14px; padding: 10px 14px; border: 1px solid var(--danger); border-left-width: 3px; background: var(--danger-soft); color: var(--danger); font-size: 13px; }
@media (max-width: 640px) { .page-header { padding: 16px; } .page-title { font-size: 21px; } }
.world-hint { padding: 12px 14px; border: 1px dashed var(--border-line); background: var(--paper-card); }
.world-hint p { margin: 6px 0 0; font-size: 12px; line-height: 1.7; color: var(--text-secondary); }
.wh-tag { padding: 1px 7px; font: 10px/1.6 var(--font-mono, monospace); letter-spacing: .14em; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); }
</style>
