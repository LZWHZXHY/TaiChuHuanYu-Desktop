<!-- 个人中心「Steam」：用 Steam 官方登录绑定、选择公开主页上显示哪些、手动刷新、解绑。 -->
<template>
  <section class="content-panel library-panel steam-settings">
    <div class="section-heading"><div><h2>Steam</h2><p>绑定后，你的公开主页会显示一张 Steam 卡片：在线状态、正在玩什么、游戏和时长（玩三角洲行动的话单独显示时长）。</p></div></div>

    <p v-if="notice" class="ss-msg" :class="{ error: noticeError }" role="status">{{ notice }}</p>
    <p v-if="loading" class="ss-tip">读取中…</p>

    <template v-else-if="mine && !mine.bound">
      <div class="ss-bind">
        <p>点下面的按钮会跳到 <b>Steam 官方登录页</b>，在那里登录后自动跳回来。我们<b>拿不到你的 Steam 密码</b>，只会知道你的 Steam 号是哪个。</p>
        <ul class="ss-points">
          <li>国内打不开 Steam 登录页的话，先开加速器（UU、Watt Toolkit 等）</li>
          <li>想让大家看到游戏和时长：在 Steam「编辑个人资料 → 隐私设置」里把「游戏详情」设为公开</li>
          <li>我们会保存你的 Steam ID 和公开资料的快照（头像、昵称、游戏、时长），解绑时立即删除</li>
        </ul>
        <button type="button" class="ss-steam-btn" :disabled="busy" @click="bind">
          <span class="ss-steam-logo" aria-hidden="true">S</span>{{ busy ? '正在跳转…' : '用 Steam 登录绑定' }}
        </button>
      </div>
    </template>

    <template v-else-if="mine && mine.bound">
      <p v-if="!mine.keyReady" class="ss-msg error">服务器还没配置 Steam 密钥，暂时读不到你的 Steam 资料（绑定已经成功）。</p>
      <SteamCard v-if="mine.card" :card="mine.card" :show-playtime="true" />
      <p v-else class="ss-tip">已绑定 Steam 号 {{ mine.steamId }}，资料还没读到，可以点「立即更新」。</p>

      <fieldset class="ss-opts" :disabled="busy">
        <legend>公开主页上显示</legend>
        <label><input type="checkbox" :checked="mine.settings.showStatus" @change="save('showStatus', ($event.target as HTMLInputElement).checked)" /> 在线状态、正在玩什么</label>
        <label><input type="checkbox" :checked="mine.settings.showGames" @change="save('showGames', ($event.target as HTMLInputElement).checked)" /> 游戏（最近在玩、玩得最久、游戏数）</label>
        <label><input type="checkbox" :checked="mine.settings.showPlaytime" @change="save('showPlaytime', ($event.target as HTMLInputElement).checked)" /> 游戏时长</label>
        <small>上面的预览是你自己看到的完整版；别人看你的主页时按这里的选择隐藏。</small>
      </fieldset>

      <div class="ss-actions">
        <button type="button" class="btn-line" :disabled="busy || !mine.keyReady" @click="refresh">立即更新</button>
        <RouterLink class="btn-line" :to="{ path: `/user/${userStore.userId}`, query: { view: 'public' } }">看看别人看到的样子</RouterLink>
        <button type="button" class="btn-line danger" :disabled="busy" @click="unbind">解绑</button>
        <small class="ss-tip">在线状态大约 10 分钟更新一次，游戏和时长大约 6 小时更新一次。</small>
      </div>
    </template>

    <p v-else-if="!loading" class="ss-tip">Steam 功能暂时用不了（服务器可能还没更新）。</p>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { steamApi, type MySteam, type SteamSettings } from '@/api/steam'
import SteamCard from '@/components/SteamCard.vue'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const mine = ref<MySteam | null>(null)
const loading = ref(true)
const busy = ref(false)
const notice = ref('')
const noticeError = ref(false)
const say = (text: string, isError = false) => { notice.value = text; noticeError.value = isError }
const errMsg = (e: any, fallback: string) => e?.response?.data?.message || e?.friendlyMessage || fallback

async function load() {
  loading.value = true
  try { mine.value = await steamApi.mine() } catch { mine.value = null } finally { loading.value = false }
}

/** 从 Steam 跳回来时地址上带着结果，显示一句话后把参数去掉 */
function readReturn() {
  const s = route.query.steam
  if (!s) return
  if (s === 'bound') say('绑定成功！')
  else say(`绑定没有成功：${String(route.query.reason || '请再试一次')}`, true)
  const { steam: _s, reason: _r, ...rest } = route.query
  void router.replace({ query: rest })
}

async function bind() {
  busy.value = true
  say('')
  try {
    // 绑完跳回个人中心的 Steam 这一栏
    const back = `${location.origin}${location.pathname}#/user/${userStore.userId}?tab=steam`
    const { url } = await steamApi.start(back)
    location.href = url
  } catch (e) {
    say(errMsg(e, '没法开始绑定，请稍后再试'), true)
    busy.value = false
  }
}

async function save(key: keyof SteamSettings, value: boolean) {
  if (!mine.value?.bound) return
  const old = mine.value.settings[key]
  mine.value.settings[key] = value
  busy.value = true
  try { await steamApi.saveSettings({ [key]: value }); say('已保存') }
  catch (e) { mine.value.settings[key] = old; say(errMsg(e, '保存失败'), true) }
  finally { busy.value = false }
}

async function refresh() {
  if (!mine.value?.bound) return
  busy.value = true
  try { mine.value.card = await steamApi.refresh(); say('已经更新') }
  catch (e) { say(errMsg(e, '更新失败'), true) }
  finally { busy.value = false }
}

async function unbind() {
  if (!confirm('确定解绑 Steam 吗？保存的 Steam 资料会一起删除，以后可以重新绑定。')) return
  busy.value = true
  try { await steamApi.unbind(); say('已解绑'); await load() }
  catch (e) { say(errMsg(e, '解绑失败'), true) }
  finally { busy.value = false }
}

onMounted(() => { readReturn(); void load() })
</script>

<style scoped>
.ss-msg { margin: 0 0 14px; padding: 10px 14px; border-left: 3px solid var(--success, #2f9e44); background: color-mix(in srgb, var(--success, #2f9e44) 8%, var(--paper-card)); font-size: 14px; }
.ss-msg.error { border-left-color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--paper-card)); }
.ss-tip { color: var(--text-secondary); font-size: 13px; }
.ss-bind { display: grid; gap: 12px; padding: 18px; border: 1px dashed var(--border-line); background: var(--paper-card); line-height: 1.8; }
.ss-bind p { margin: 0; }
.ss-points { margin: 0; padding-left: 20px; color: var(--text-secondary); font-size: 13px; }
.ss-steam-btn { justify-self: start; display: inline-flex; align-items: center; gap: 10px; padding: 10px 18px; border: 0; border-radius: 0; background: #171a21; color: #fff; font: inherit; font-weight: 600; cursor: pointer; }
.ss-steam-btn:hover:not(:disabled) { background: #1b2838; box-shadow: 0 0 0 2px #66c0f4 inset; }
.ss-steam-btn:disabled { opacity: .6; cursor: default; }
.ss-steam-btn:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.ss-steam-logo { width: 22px; height: 22px; display: grid; place-items: center; background: #66c0f4; color: #171a21; font-size: 13px; }
.ss-opts { display: grid; gap: 8px; margin: 4px 0 16px; padding: 14px 16px; border: 1px solid var(--border-line); background: var(--paper-card); }
.ss-opts legend { padding: 0 6px; font-weight: 600; }
.ss-opts label { display: flex; align-items: center; gap: 8px; font-size: 14px; cursor: pointer; }
.ss-opts small { color: var(--text-secondary); font-size: 12px; }
.ss-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
.btn-line.danger { color: var(--danger); border-color: color-mix(in srgb, var(--danger) 50%, var(--border-line)); }
</style>
