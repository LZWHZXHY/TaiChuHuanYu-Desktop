<template>
  <div class="community-detail edit-page studio-page workbench-skin editor-surface community-surface tone-chai">
    <header class="page-header studio-heading"><SectionTrace particles="chai" />
      <div>
        <span class="page-eyebrow">OC / EDIT</span>
        <h1 class="page-title">编辑 OC</h1>
        <p class="page-subtitle">修改「{{ character?.title || '角色' }}」的设定</p>
      </div>
      <router-link v-if="character" :to="`/ocs/${character.id}`" class="back-link">← 回到 OC 页</router-link>
    </header>

    <div v-if="loading" class="loading-state">加载中…</div>

    <div v-else-if="!character" class="empty-state">
      <p>{{ error || '角色不存在' }}</p>
      <router-link to="/ocs" class="empty-link">返回 OC 画阁</router-link>
    </div>

    <template v-else>
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <CharacterForm :initial-data="character" :loading="store.loading" @submit="handleUpdate">
        <!-- 能力面板：单独的接口、单独的保存按钮，不经过 OC 表单的保存（也不在表单里，按回车不会误提交） -->
        <template #extra>
          <section class="panels-card">
            <header class="panels-head"><span class="panels-tag">04</span><h3>能力面板</h3><span>展示面板 · 约战面板（各自单独保存）</span></header>
            <OcPanelsEditor ref="panelsRef" :character-id="character.id" />
          </section>
        </template>
        <!-- 世界观联动（关联、复制到世界观、从对应条目更新、备份）在详情页做：这里的表单还没保存时同步，保存会把同步的内容盖掉 -->
        <template #aside>
          <section class="world-card">
            <span class="panels-tag">WORLD</span>
            <p>要把这个 OC 加入世界观（关联、复制成世界观条目）、从世界观条目更新或者查看备份，请到
              <router-link :to="`/ocs/${character.id}`">OC 详情页</router-link> 的「🌐 世界观」一栏。先保存这里的修改。</p>
          </section>
        </template>
      </CharacterForm>
    </template>
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { computed, ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useStickmanStore } from '../stickman_store.ts'
import CharacterForm from '../../components/CharacterForm.vue'
import OcPanelsEditor from './OcPanelsEditor.vue'

const router = useRouter()
const route = useRoute()
const store = useStickmanStore()

const character = computed(() => store.currentCharacter)
const loading = ref(true)
const error = ref('')
const panelsRef = ref<InstanceType<typeof OcPanelsEditor> | null>(null)

onMounted(async () => {
  const id = route.params.id as string
  store.clearCurrent()
  try { await store.fetchDetail(id) }
  catch { error.value = '角色加载失败，请返回后重试。' }
  finally { loading.value = false }
})


async function handleUpdate(data: any) {
  if (!character.value || store.loading) return
  error.value = ''
  try {
    await store.updateCharacter(character.value.id, data)
  } catch {
    error.value = '保存失败，请重试。已填写的内容仍保留。'
    return
  }
  // OC 已经保存好了。能力面板如果有没保存的修改，顺便用面板自己的接口保存一下（不影响上面 OC 的数据）；
  // 面板没保存成功就留在这个页面，免得跳走后面板的修改丢了
  if (panelsRef.value?.dirty && !await panelsRef.value.saveAll()) {
    error.value = 'OC 已保存，但能力面板没保存成功（原因见面板下方），请再点一次「保存展示面板 / 保存约战面板」。'
    return
  }
  router.push(`/ocs/${character.value.id}`)
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
.loading-state, .empty-state { padding: 60px 0; text-align: center; color: var(--text-secondary); font-size: 14px; letter-spacing: .1em; }
.empty-link { color: var(--accent-color); text-decoration: none; border-bottom: 1px solid var(--border-line); padding-bottom: 2px; }
.panels-card { position: relative; padding: 18px 20px 20px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 2px solid var(--surface-color, var(--accent-color)); }
.panels-head { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 10px; margin-bottom: 14px; }
.panels-head h3 { margin: 0; font-family: var(--font-display, inherit); font-size: 17px; font-weight: 600; letter-spacing: .04em; }
.panels-head span:last-child { font-size: 12px; color: var(--text-secondary); }
.panels-tag { padding: 1px 7px; font: 10px/1.6 var(--font-mono, monospace); letter-spacing: .14em; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); }
.world-card { padding: 14px 16px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 2px solid var(--signal-mint); }
.world-card p { margin: 8px 0 0; font-size: 12px; line-height: 1.75; color: var(--text-secondary); }
.world-card a { color: var(--accent-color); }
</style>
