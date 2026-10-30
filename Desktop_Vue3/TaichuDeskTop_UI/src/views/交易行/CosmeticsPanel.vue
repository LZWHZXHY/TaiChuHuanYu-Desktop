<!-- CosmeticsPanel.vue：我的装扮。佩戴 / 取下头衔和头像框，看还有几张改名卡。交易行和个人中心都用它。 -->
<template>
  <Teleport to="body">
    <div class="cos-mask" role="dialog" aria-modal="true" aria-label="我的装扮" @click.self="emit('close')">
      <div class="cos">
        <header class="cos-head">
          <div>
            <p class="eyebrow">WARDROBE / 我的装扮</p>
            <h2>头衔与头像框</h2>
          </div>
          <button type="button" class="x" aria-label="关闭" @click="emit('close')">✕</button>
        </header>

        <p v-if="loading" class="state">正在读取…</p>
        <p v-else-if="error" class="state err">{{ error }} <button type="button" class="btn-s" @click="load">重试</button></p>
        <template v-else-if="inv">
          <section class="preview">
            <AvatarFrame :frame="inv.equippedFrame">
              <span class="pv-avatar"><img v-if="avatar" :src="avatar" alt="" /><b v-else>{{ initial }}</b></span>
            </AvatarFrame>
            <div class="pv-text">
              <strong>{{ user.userInfo?.username }}</strong>
              <span class="title-badge" :class="{ empty: !inv.equippedTitle }">{{ inv.equippedTitle || '未佩戴头衔' }}</span>
            </div>
          </section>

          <section class="group">
            <div class="group-head"><h3>头衔 · {{ inv.titles.length }}</h3><small>佩戴后显示在个人主页、荣誉墙</small></div>
            <div v-if="inv.titles.length" class="titles">
              <button v-for="t in inv.titles" :key="t.key" type="button" class="title-opt" :class="{ on: inv.equippedTitle === t.key }" :disabled="busy" @click="equip('title', inv.equippedTitle === t.key ? null : t.key)">
                <span class="title-badge">{{ t.key }}</span>
                <small>{{ inv.equippedTitle === t.key ? '佩戴中 · 点击取下' : t.source }}</small>
              </button>
            </div>
            <p v-else class="empty-tip">还没有头衔，去交易行看看。</p>
          </section>

          <section class="group">
            <div class="group-head"><h3>头像框 · {{ inv.frames.length }}</h3><small>佩戴后套在头像外面</small></div>
            <div v-if="inv.frames.length" class="frames">
              <button v-for="f in inv.frames" :key="f.key" type="button" class="frame-opt" :class="{ on: inv.equippedFrame === f.key }" :disabled="busy" @click="equip('frame', inv.equippedFrame === f.key ? null : f.key)">
                <AvatarFrame :frame="f.key"><span class="mini-avatar"><img v-if="avatar" :src="avatar" alt="" /><b v-else>{{ initial }}</b></span></AvatarFrame>
                <span>{{ frameName(f.key) }}</span>
                <small>{{ inv.equippedFrame === f.key ? '佩戴中 · 点击取下' : '点击佩戴' }}</small>
              </button>
            </div>
            <p v-else class="empty-tip">还没有头像框，去交易行看看。</p>
          </section>

          <section class="group cards">
            <div class="group-head"><h3>官方头像 · {{ inv.avatars?.length ?? 0 }}</h3><small>点个人中心的头像就能换上</small>
              <RouterLink v-if="inv.avatars?.length && user.userId" :to="{ path: `/user/${user.userId}`, query: { pick: 'avatar' } }" class="btn-s" @click="emit('close')">去换头像 →</RouterLink></div>
            <div class="group-head"><h3>网站背景 · {{ inv.backgrounds?.length ?? 0 }}</h3><small>在「个人中心 → 网站背景」里换上</small>
              <RouterLink v-if="inv.backgrounds?.length && user.userId" :to="{ path: `/user/${user.userId}`, query: { tab: 'appearance' } }" class="btn-s" @click="emit('close')">去换背景 →</RouterLink></div>
            <div class="group-head"><h3>主页横幅 / 背景 · {{ inv.profileBackgrounds?.length ?? 0 }}</h3><small>在「个人中心 → 主页装扮」里换上</small>
              <RouterLink v-if="inv.profileBackgrounds?.length && user.userId" :to="{ path: `/user/${user.userId}`, query: { tab: 'profile' } }" class="btn-s" @click="emit('close')">去装扮主页 →</RouterLink></div>
          </section>

          <section class="group cards">
            <div class="group-head"><h3>改名卡 · {{ inv.renameCards }} 张</h3><small>在个人中心名字旁边点「✎ 改名卡」时可以选择用掉一张</small></div>
          </section>

          <footer class="cos-foot">
            <RouterLink v-if="showShopLink && hasShop" :to="{ name: '交易行' }" class="btn-s" @click="emit('close')">去交易行买更多 →</RouterLink>
            <span class="grow"></span>
            <span v-if="msg" class="msg">{{ msg }}</span>
          </footer>
        </template>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import AvatarFrame from '@/components/AvatarFrame.vue'
import { frameName } from '@/utils/avatarFrames'
import { TradeApi, tradeError, type IInventory } from '@/api/trade'
import { setAppearance } from '@/utils/appearanceCache'

withDefaults(defineProps<{ showShopLink?: boolean }>(), { showShopLink: false })
const emit = defineEmits<{ close: []; changed: [inv: IInventory] }>()

const user = useUserStore()
// 交易行的路由是插件表动态注册的，没注册时不显示入口
const hasShop = useRouter().hasRoute('交易行')
const inv = ref<IInventory | null>(null)
const loading = ref(true)
const error = ref('')
const busy = ref(false)
const msg = ref('')
const avatar = computed(() => user.userInfo?.avatar || '')
const initial = computed(() => user.userInfo?.username?.[0] ?? '?')

async function load() {
  loading.value = true
  error.value = ''
  try { inv.value = await TradeApi.inventory() } catch (e) { error.value = tradeError(e, '读取装扮失败') } finally { loading.value = false }
}

async function equip(kind: 'title' | 'frame', key: string | null) {
  if (!inv.value || busy.value) return
  busy.value = true
  msg.value = ''
  try {
    await TradeApi.equip(kind, key)
    if (kind === 'title') inv.value.equippedTitle = key
    else inv.value.equippedFrame = key
    msg.value = key ? '已佩戴' : '已取下'
    // 论坛、聊天室等地方的缓存也马上换成新的
    setAppearance(user.userId, kind === 'title' ? { title: key } : { frame: key })
    // 头衔存在账户资料里：刷新一下，个人中心等地方马上显示新的
    if (kind === 'title') await user.fetchUserInfo(true).catch(() => null)
    emit('changed', inv.value)
  } catch (e) { msg.value = tradeError(e, '操作失败') } finally { busy.value = false }
}

onMounted(load)
</script>

<style scoped>
.cos-mask { position: fixed; inset: 0; z-index: 3200; display: grid; place-items: center; padding: 16px; background: rgb(8 12 16 / 62%); }
.cos { display: grid; gap: 16px; width: min(720px, 100%); max-height: 100%; overflow-y: auto; padding: 18px 20px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); }
.cos-head { display: flex; justify-content: space-between; align-items: flex-start; }
.eyebrow { margin: 0 0 4px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.cos-head h2 { margin: 0; font-size: 20px; }
.x { width: 32px; height: 32px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); cursor: pointer; }
.state { margin: 0; padding: 24px; text-align: center; color: var(--text-secondary); }
.state.err { color: var(--danger); }
.preview { display: flex; align-items: center; gap: 22px; padding: 18px 20px; border: 1px dashed var(--border-line); background: var(--bg-main); }
.pv-avatar { display: grid; place-items: center; width: 72px; height: 72px; overflow: hidden; background: linear-gradient(135deg, var(--accent-color), var(--signal-mint, #2dd4bf)); color: #fff; font-size: 28px; }
.pv-avatar img, .mini-avatar img { width: 100%; height: 100%; object-fit: cover; }
.pv-text { display: grid; gap: 6px; }
.pv-text strong { font-size: 18px; }
.title-badge { display: inline-block; width: max-content; padding: 2px 10px; border: 1px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 10%, transparent); color: var(--accent-color); font-size: 12px; font-weight: 600; letter-spacing: .06em; }
.title-badge.empty { border-style: dashed; color: var(--text-secondary); background: transparent; font-weight: 400; }
.group { display: grid; gap: 10px; }
.group-head { display: flex; flex-wrap: wrap; align-items: baseline; gap: 10px; }
.group-head h3 { margin: 0; font-size: 15px; }
.group-head small { font-size: 12px; color: var(--text-secondary); }
.titles { display: flex; flex-wrap: wrap; gap: 8px; }
.title-opt { display: grid; gap: 4px; justify-items: start; padding: 8px 12px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; cursor: pointer; }
.title-opt small, .frame-opt small { font-size: 11px; color: var(--text-secondary); }
.title-opt.on, .frame-opt.on { border-color: var(--accent-color); box-shadow: inset 0 -3px 0 var(--accent-color); }
.frames { display: grid; grid-template-columns: repeat(auto-fill, minmax(118px, 1fr)); gap: 10px; }
.frame-opt { display: grid; justify-items: center; gap: 8px; padding: 18px 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.mini-avatar { display: grid; place-items: center; width: 52px; height: 52px; overflow: hidden; background: linear-gradient(135deg, var(--accent-color), var(--signal-mint, #2dd4bf)); color: #fff; font-size: 20px; }
button:disabled { opacity: .6; cursor: wait; }
.empty-tip { margin: 0; font-size: 13px; color: var(--text-secondary); }
.cos-foot { display: flex; align-items: center; gap: 10px; padding-top: 12px; border-top: 1px solid var(--border-line); }
.grow { flex: 1; }
.msg { font-size: 13px; color: var(--accent-color); }
.btn-s { padding: 6px 12px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; text-decoration: none; cursor: pointer; }
</style>
