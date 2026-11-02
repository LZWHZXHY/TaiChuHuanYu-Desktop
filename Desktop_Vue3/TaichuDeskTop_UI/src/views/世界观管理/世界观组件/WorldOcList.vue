<!-- WorldOcList.vue：世界观首页的「柴圈人设」：作者们关联到这个世界观的、已发布的柴圈 OC。
     看得到世界观的人才看得到（后端按世界观可见性检查）；一个都没有时整块不显示。
     世界观创建者和编写者可以「移出」某个 OC：只删关联，OC 本身不受影响（会通知 OC 作者）。 -->
<template>
  <section v-if="items.length" class="wol">
    <div class="wol-head">
      <h3>🧍 柴圈人设</h3>
      <small>作者们把自己的柴圈 OC 关联到了这个世界</small>
    </div>
    <ul class="wol-list">
      <li v-for="oc in items" :key="oc.id" class="wol-item">
        <RouterLink :to="`/ocs/${oc.id}`" class="wol-card">
          <span class="wol-cover">
            <img v-if="oc.coverUrl" :src="withThumbnail(oc.coverUrl, 240)" :alt="oc.title" loading="lazy" />
            <span v-else>{{ oc.title.slice(0, 1) }}</span>
          </span>
          <span class="wol-main">
            <span class="wol-title">{{ oc.title }}</span>
            <span class="wol-author">{{ oc.authorName }}</span>
            <span v-if="oc.description" class="wol-desc">{{ oc.description }}</span>
          </span>
        </RouterLink>
        <button v-if="canManage" type="button" class="wol-remove" :disabled="busy === oc.id" :aria-label="`把 ${oc.title} 移出这个世界观`" @click="remove(oc)">
          {{ busy === oc.id ? '移出中…' : '移出' }}
        </button>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { withThumbnail } from '@/utils/imageThumb'
import { getErrorMessage } from '@/utils/apiError'
import { ocWorldApi, type WorldOcItem } from '@/views/柴圈板块/OCs/ocWorld'

const props = defineProps<{ projectId: string }>()

const items = ref<WorldOcItem[]>([])
const canManage = ref(false)
const busy = ref<string | null>(null)

let version = 0
async function load() {
  const v = ++version
  try {
    const res = await ocWorldApi.worldOcs(props.projectId)
    if (v !== version) return
    items.value = res.items
    canManage.value = res.canManage
  } catch {
    // 读不到就不显示（不影响世界观首页其他内容）
    if (v === version) { items.value = []; canManage.value = false }
  }
}
watch(() => props.projectId, id => { items.value = []; if (id) void load() }, { immediate: true })

async function remove(oc: WorldOcItem) {
  try {
    await ElMessageBox.confirm(`把《${oc.title}》移出这个世界观？只是取消关联，OC 本身和世界观里的条目都不会变，会通知 OC 的作者。`, '移出柴圈人设',
      { confirmButtonText: '移出', cancelButtonText: '不了', type: 'warning' })
  } catch { return }
  busy.value = oc.id
  try {
    await ocWorldApi.unlink(oc.id, props.projectId)
    items.value = items.value.filter(x => x.id !== oc.id)
    ElMessage.success('已移出')
  } catch (e) { ElMessage.error(getErrorMessage(e, '移出失败')) }
  finally { busy.value = null }
}
</script>

<style scoped>
.wol { margin-top: 28px; }
.wol-head { display: flex; flex-wrap: wrap; align-items: baseline; gap: 8px 12px; margin-bottom: 12px; }
.wol-head h3 { margin: 0; font-size: 16px; color: var(--text-primary); }
.wol-head small { color: var(--text-secondary); font-size: 12px; }
.wol-list { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 10px; }
.wol-item { position: relative; border: 1px solid var(--border-line); background: var(--paper-card); }
.wol-item:hover { border-color: var(--accent-color); }
.wol-card { display: flex; gap: 10px; padding: 10px; color: var(--text-primary); text-decoration: none; min-width: 0; }
.wol-cover { flex: 0 0 64px; width: 64px; height: 64px; display: flex; align-items: center; justify-content: center; background: var(--bg-sub); color: var(--text-secondary); font-size: 22px; overflow: hidden; }
.wol-cover img { width: 100%; height: 100%; object-fit: cover; }
.wol-main { display: grid; align-content: start; gap: 2px; min-width: 0; }
.wol-title { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.wol-author { font-size: 12px; color: var(--text-secondary); }
.wol-desc { font-size: 12px; color: var(--text-secondary); line-height: 1.5; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.wol-remove { position: absolute; top: 6px; right: 6px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 12px; padding: 1px 8px; cursor: pointer; opacity: 0; transition: opacity .15s; }
.wol-item:hover .wol-remove, .wol-remove:focus-visible { opacity: 1; }
.wol-remove:hover { color: var(--accent-color); border-color: var(--accent-color); }
.wol-remove:disabled { opacity: .6; cursor: not-allowed; }
@media (hover: none) { .wol-remove { opacity: 1; } }
</style>
