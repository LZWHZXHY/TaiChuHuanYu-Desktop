<template>
  <div class="create-page">
    <header class="page-header">
      <div class="header-left">
        <div class="title-group">
          <button class="back-btn" @click="router.back()">‹ 返回</button>
          <h1>发起约战</h1>
        </div>
        <p class="subtitle">设定规则，邀请画师，以创作为战</p>
      </div>
    </header>

    <form class="create-form" @submit.prevent="handleSubmit">
      <!-- ===== 参战角色 ===== -->
      <section class="form-section">
        <h2 class="section-title">参战角色</h2>

        <!-- ===== 队伍A ===== -->
        <div class="team-section">
          <div class="team-header">
            <span class="team-label">🔴 队伍 A</span>
            <button type="button" class="btn-add-member" @click="addMember('A')">＋ 添加成员</button>
          </div>
          <p class="hint">默认包含你，可添加其他画师组队</p>

          <div
            v-for="(member, index) in teamA"
            :key="'A' + index"
            class="member-group"
          >
            <div class="member-header">
              <span class="member-label">成员 {{ index + 1 }}</span>
              <button
                v-if="index > 0"
                type="button"
                class="btn-remove-member"
                @click="removeMember('A', index)"
              >
                × 移除
              </button>
            </div>

            <div class="member-row">
              <!-- 用户ID显示 -->
              <div class="member-field">
                <label class="sub-label">用户 ID</label>
                <input :value="member.userId" type="text" disabled placeholder="自动识别" />
              </div>

              <!-- OC 搜索 + 选择 -->
              <div class="member-field">
                <label class="sub-label">出战 OC</label>

                <!-- ⭐ 搜索框 -->
                <div class="search-wrapper">
                  <input
                    v-model="member.searchKeyword"
                    type="text"
                    placeholder="输入 OC 名字或用户 ID 搜索"
                    @input="onMemberSearchInput('A', index)"
                  />
                  <button
                    type="button"
                    class="btn-search"
                    @click="searchOcForMember('A', index)"
                    :disabled="member.searching"
                  >
                    🔍
                  </button>
                </div>
                <span v-if="member.searching" class="searching-hint">检索中...</span>

                <!-- ⭐ 搜索结果 -->
                <div v-if="member.ocList.length > 0" class="search-results">
                  <div
                    v-for="oc in member.ocList"
                    :key="oc.id"
                    class="search-result-item"
                  >
                    <span class="result-info">
                      <strong>{{ oc.title }}</strong>
                      <span class="result-author">— {{ oc.authorName }}</span>
                    </span>
                    <button
                      type="button"
                      class="btn-add-oc"
                      :disabled="member.ocIds.includes(oc.id)"
                      @click="addOcToMember('A', index, oc)"
                    >
                      {{ member.ocIds.includes(oc.id) ? '已添加' : '添加' }}
                    </button>
                  </div>
                </div>

                <!-- ⭐ 已选 OC -->
                <div v-if="member.ocIds.length" class="selected-ocs">
                  <span
                    v-for="id in member.ocIds"
                    :key="id"
                    class="selected-tag"
                  >
                    {{ getOcTitle(allOcList, id) }}
                    <button
                      type="button"
                      class="tag-remove"
                      @click="removeMemberOc('A', index, id)"
                    >
                      ×
                    </button>
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ===== VS ===== -->
        <div class="vs-divider">⚔️ VS</div>

        <!-- ===== 队伍B ===== -->
        <div class="team-section">
          <div class="team-header">
            <span class="team-label">🔵 队伍 B</span>
            <button type="button" class="btn-add-member" @click="addMember('B')">＋ 添加成员</button>
          </div>
          <p class="hint">添加对手成员（支持多人混战）</p>

          <div
            v-for="(member, index) in teamB"
            :key="'B' + index"
            class="member-group"
          >
            <div class="member-header">
              <span class="member-label">成员 {{ index + 1 }}</span>
              <button
                type="button"
                class="btn-remove-member"
                @click="removeMember('B', index)"
              >
                × 移除
              </button>
            </div>

            <div class="member-row">
              <!-- 用户 ID 输入（队伍B需要自己输入） -->
              <div class="member-field">
                <label class="sub-label">用户 ID</label>
                <div class="userId-input-wrapper">
                  <input
                    v-model="member.userId"
                    type="text"
                    placeholder="输入对手的用户 ID"
                  />
                  <button
                    type="button"
                    class="btn-fetch-ocs"
                    @click="fetchOcsForMember('B', index)"
                  >
                    加载 OC
                  </button>
                </div>
              </div>

              <!-- OC 搜索 + 选择 -->
              <div class="member-field">
                <label class="sub-label">出战 OC</label>

                <!-- ⭐ 搜索框 -->
                <div class="search-wrapper">
                  <input
                    v-model="member.searchKeyword"
                    type="text"
                    placeholder="输入 OC 名字或用户 ID 搜索"
                    @input="onMemberSearchInput('B', index)"
                  />
                  <button
                    type="button"
                    class="btn-search"
                    @click="searchOcForMember('B', index)"
                    :disabled="member.searching"
                  >
                    🔍
                  </button>
                </div>
                <span v-if="member.searching" class="searching-hint">检索中...</span>

                <!-- ⭐ 搜索结果 -->
                <div v-if="member.ocList.length > 0" class="search-results">
                  <div
                    v-for="oc in member.ocList"
                    :key="oc.id"
                    class="search-result-item"
                  >
                    <span class="result-info">
                      <strong>{{ oc.title }}</strong>
                      <span class="result-author">— {{ oc.authorName }}</span>
                    </span>
                    <button
                      type="button"
                      class="btn-add-oc"
                      :disabled="member.ocIds.includes(oc.id)"
                      @click="addOcToMember('B', index, oc)"
                    >
                      {{ member.ocIds.includes(oc.id) ? '已添加' : '添加' }}
                    </button>
                  </div>
                </div>

                <!-- ⭐ 已选 OC -->
                <div v-if="member.ocIds.length" class="selected-ocs">
                  <span
                    v-for="id in member.ocIds"
                    :key="id"
                    class="selected-tag opponent-tag"
                  >
                    {{ getOcTitle(allOcList, id) }}
                    <button
                      type="button"
                      class="tag-remove"
                      @click="removeMemberOc('B', index, id)"
                    >
                      ×
                    </button>
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <!-- ===== 基本信息 ===== -->
      <section class="form-section">
        <h2 class="section-title">基本信息</h2>

        <div class="form-group">
          <label>约战标题 <span class="required">*</span></label>
          <input v-model="form.title" type="text" placeholder="给这场约战起个名字" maxlength="100" required />
          <span class="char-count">{{ form.title.length }}/100</span>
        </div>

        <div class="form-group">
          <label>封面图</label>
          <div class="upload-area" @click="uploadCover">
            <div v-if="form.coverUrl" class="cover-preview">
              <img :src="form.coverUrl" alt="封面" />
              <button type="button" class="remove-btn" @click.stop="form.coverUrl = ''">×</button>
            </div>
            <div v-else class="upload-placeholder">
              <span class="upload-icon">🖼</span>
              <span>点击上传封面图</span>
              <span class="upload-hint">建议 16:9 比例，不超过 2MB</span>
            </div>
          </div>
          <div v-if="uploading" class="upload-status">上传中...</div>
        </div>
      </section>

      <!-- ===== 战斗配置 ===== -->
      <section class="form-section">
        <h2 class="section-title">战斗配置</h2>

        <div class="form-group">
          <label>战斗类型 <span class="required">*</span></label>
          <input v-model="form.battleType" type="text" placeholder="如：2v2、3v3、车轮战、自定义..." maxlength="50" required />
          <p class="hint">完全自定义，想怎么打就怎么打</p>
        </div>

        <div class="form-group">
          <label>详细规则 <span class="required">*</span></label>
          <textarea v-model="form.rules" rows="5" placeholder="描述具体规则..." maxlength="2000" required></textarea>
          <span class="char-count">{{ form.rules.length }}/2000</span>
        </div>
      </section>

      <!-- ===== 判定方式 ===== -->
      <section class="form-section">
        <h2 class="section-title">判定方式</h2>

        <div class="radio-group">
          <label class="radio-option" :class="{ active: form.judgmentType === 'vote' }">
            <input type="radio" v-model="form.judgmentType" value="vote" />
            <div class="radio-content">
              <span class="radio-label">📊 投票制</span>
              <span class="radio-desc">社区投票决定胜负，公开透明</span>
            </div>
          </label>

          <label class="radio-option" :class="{ active: form.judgmentType === 'internal' }">
            <input type="radio" v-model="form.judgmentType" value="internal" />
            <div class="radio-content">
              <span class="radio-label">🤝 内定制</span>
              <span class="radio-desc">参与者协商决定胜负，为剧情服务</span>
            </div>
          </label>
        </div>
      </section>

      <!-- ===== 提交 ===== -->
      <div class="form-actions">
        <button type="button" class="btn-cancel" @click="router.back()">取消</button>
        <button type="submit" class="btn-submit" :disabled="loading || !isValid">
          {{ loading ? '发布中...' : '发布约战' }}
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useBattleStore } from '../battle_Store'
import { useCos } from '@/composables/useCos'
import { stickmanApi } from '../../OCs/stickman'
import { useUserStore } from '@/stores/user'
import { ElMessage } from 'element-plus'

const router = useRouter()
const battleStore = useBattleStore()
const userStore = useUserStore()
const { uploadFile } = useCos()

// ===== 类型定义 =====
interface Member {
  userId: string
  ocIds: string[]
  ocList: any[]
  searchKeyword: string
  searching: boolean
}

const loading = ref(false)
const uploading = ref(false)
let searchTimer: any = null

const userId = computed(() => userStore.userInfo?.id || '')

// ===== 所有 OC 缓存 =====
const allOcList = ref<any[]>([])

// ===== 队伍 A（默认包含当前用户） =====
const teamA = ref<Member[]>([
  {
    userId: userId.value,
    ocIds: [],
    ocList: [],
    searchKeyword: '',
    searching: false,
  },
])

// ===== 队伍 B =====
const teamB = ref<Member[]>([])

const form = reactive({
  title: '',
  coverUrl: '',
  battleType: '',
  rules: '',
  judgmentType: 'vote' as 'vote' | 'internal',
})

// ===== 计算属性 =====
const isValid = computed(() => {
  if (!form.title.trim() || !form.battleType.trim() || !form.rules.trim()) return false

  // 队伍A至少有一个OC
  const hasA = teamA.value.some((m) => m.ocIds.length > 0)
  if (!hasA) return false

  // 队伍B如果有成员但没有OC，视为无效
  for (const m of teamB.value) {
    if (m.userId.trim() && m.ocIds.length === 0) return false
  }

  return true
})

// ===== 工具函数 =====
const getOcTitle = (list: any[], id: string) => {
  const oc = list.find((o: any) => o.id === id)
  return oc ? oc.title : id
}

// ===== 加载所有 OC =====
const fetchAllOcs = async () => {
  try {
    const res = await stickmanApi.getList({ pageSize: 200 })
    allOcList.value = res?.items ?? []
  } catch (error) {
    console.error('获取 OC 列表失败:', error)
  }
}

// ===== ⭐ 为成员搜索 OC（核心搜索函数） =====
const searchOcForMember = async (team: 'A' | 'B', index: number) => {
  const member = team === 'A' ? teamA.value[index] : teamB.value[index]
  if (!member) return

  const keyword = member.searchKeyword?.trim()
  if (!keyword) {
    ElMessage.warning('请输入搜索关键词（OC 名字或用户 ID）')
    return
  }

  member.searching = true
  try {
    const res = await stickmanApi.getList({
      pageSize: 50,
      keyword: keyword,
    })
    const list = res?.items ?? []
    // 过滤：已发布 + 可约战 + 未添加的
    let filtered = list.filter(
      (oc: any) =>
        oc.status === 'published' &&
        oc.isBattleEnabled !== false &&
        !member.ocIds.includes(oc.id)
    )
    // 如果已经指定了 userId，只显示该用户的 OC
    if (member.userId.trim()) {
      filtered = filtered.filter((oc: any) => oc.authorId === member.userId.trim())
    }
    member.ocList = filtered
    if (filtered.length === 0) {
      ElMessage.info('没有找到匹配的 OC')
    } else {
      ElMessage.success(`找到 ${filtered.length} 个 OC`)
    }
  } catch (error) {
    console.error('搜索失败:', error)
    ElMessage.error('搜索失败')
  } finally {
    member.searching = false
  }
}

// ===== ⭐ 防抖搜索 =====
const onMemberSearchInput = (team: 'A' | 'B', index: number) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    const member = team === 'A' ? teamA.value[index] : teamB.value[index]
    if (member && member.searchKeyword.trim()) {
      searchOcForMember(team, index)
    }
  }, 500)
}

// ===== ⭐ 加载对手 OC（手动触发） =====
const fetchOcsForMember = async (team: 'B', index: number) => {
  const member = teamB.value[index]
  if (!member) return

  if (!member.userId.trim()) {
    ElMessage.warning('请先输入用户 ID')
    return
  }

  member.searchKeyword = member.userId.trim()
  await searchOcForMember(team, index)
}

// ===== ⭐ 添加 OC 到成员 =====
const addOcToMember = (team: 'A' | 'B', index: number, oc: any) => {
  const member = team === 'A' ? teamA.value[index] : teamB.value[index]
  if (!member) return

  if (!member.ocIds.includes(oc.id)) {
    member.ocIds.push(oc.id)
    // 自动设置 userId（如果还没有）
    if (!member.userId) {
      member.userId = oc.authorId
    }
    // 从搜索结果中移除已添加的
    member.ocList = member.ocList.filter((item: any) => item.id !== oc.id)
    ElMessage.success(`已添加 ${oc.title}`)
  }
}

// ===== 成员管理 =====
const addMember = (team: 'A' | 'B') => {
  const newMember: Member = {
    userId: '',
    ocIds: [],
    ocList: [],
    searchKeyword: '',
    searching: false,
  }
  if (team === 'A') {
    teamA.value.push(newMember)
  } else {
    teamB.value.push(newMember)
  }
}

const removeMember = (team: 'A' | 'B', index: number) => {
  if (team === 'A') {
    if (teamA.value.length <= 1) {
      ElMessage.warning('队伍 A 至少保留一个成员')
      return
    }
    teamA.value.splice(index, 1)
  } else {
    teamB.value.splice(index, 1)
  }
}

const removeMemberOc = (team: 'A' | 'B', index: number, ocId: string) => {
  const member = team === 'A' ? teamA.value[index] : teamB.value[index]
  if (!member) return
  member.ocIds = member.ocIds.filter((id) => id !== ocId)
  if (member.ocIds.length === 0) {
    member.userId = ''
  }
}

// ===== 初始化队伍A =====
watch(
  () => userId.value,
  (newId) => {
    if (newId && teamA.value.length > 0) {
      teamA.value[0].userId = newId
    }
  },
  { immediate: true }
)

// ===== 封面上传 =====
const uploadCover = async () => {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = 'image/*'
  input.onchange = async (e: Event) => {
    const target = e.target as HTMLInputElement
    const file = target.files?.[0]
    if (!file) return
    if (file.size > 2 * 1024 * 1024) {
      ElMessage.warning('图片不能超过 2MB')
      target.value = ''
      return
    }
    uploading.value = true
    try {
      const result = await uploadFile(file, 'battle/cover')
      form.coverUrl = result.url
      ElMessage.success('上传成功')
    } catch (error) {
      console.error('上传失败:', error)
      ElMessage.error('上传失败，请重试')
    } finally {
      uploading.value = false
      target.value = ''
    }
  }
  input.click()
}

// ===== 提交 =====
const handleSubmit = async () => {
  if (!isValid.value) {
    ElMessage.warning('请完整填写约战信息')
    return
  }

  // 队伍A所有OC
  const challengerOcIds: string[] = []
  for (const member of teamA.value) {
    challengerOcIds.push(...member.ocIds)
  }

  // 队伍B按用户分组
  const opponentOcIds: Record<string, string[]> = {}
  for (const member of teamB.value) {
    if (member.userId.trim() && member.ocIds.length > 0) {
      if (opponentOcIds[member.userId.trim()]) {
        opponentOcIds[member.userId.trim()] = [
          ...opponentOcIds[member.userId.trim()],
          ...member.ocIds,
        ]
      } else {
        opponentOcIds[member.userId.trim()] = member.ocIds
      }
    }
  }

  loading.value = true
  try {
    const data: any = {
      title: form.title.trim(),
      content: undefined,
      coverUrl: form.coverUrl || undefined,
      battleType: form.battleType.trim(),
      rules: form.rules.trim(),
      judgmentType: form.judgmentType,
      challengerOcIds: challengerOcIds,
    }

    if (Object.keys(opponentOcIds).length > 0) {
      data.opponentOcIds = opponentOcIds
    }

    const battle = await battleStore.create(data)
    ElMessage.success('约战发布成功！')
    router.push(`/battles/${battle.id}`)
  } catch (error: any) {
    console.error('创建失败:', error)
    ElMessage.error(error.response?.data?.message || '发布失败，请重试')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await fetchAllOcs()
  if (teamA.value.length > 0 && userId.value) {
    teamA.value[0].userId = userId.value
  }
})
</script>

<style scoped>
/* ============================================================
   整体容器
   ============================================================ */
.create-page {
  max-width: 860px;
  margin: 0 auto;
  padding: 32px 20px 60px;
  background: #f5f0eb;
  min-height: 100vh;
  color: #2c2a29;
}

.page-header {
  padding-bottom: 20px;
  border-bottom: 2px solid #d8d0c4;
  margin-bottom: 32px;
}

.header-left {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.title-group {
  display: flex;
  align-items: center;
  gap: 16px;
}

.back-btn {
  background: none;
  border: none;
  font-size: 15px;
  color: #999;
  cursor: pointer;
  font-family: inherit;
  padding: 0;
  transition: color 0.25s;
}
.back-btn:hover {
  color: #2c2a29;
}

.title-group h1 {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.15em;
  margin: 0;
  color: #2c2a29;
}

.subtitle {
  font-size: 14px;
  color: #999;
  margin: 0;
  letter-spacing: 0.08em;
}

.create-form {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.form-section {
  background: #fcfaf7;
  border: 1px solid #d8d0c4;
  padding: 24px 28px;
}

.section-title {
  font-size: 15px;
  font-weight: 400;
  letter-spacing: 0.12em;
  margin: 0 0 18px 0;
  padding-bottom: 12px;
  border-bottom: 1px dashed #d8d0c4;
  color: #2c2a29;
}

/* ===== 队伍 ===== */
.team-section {
  margin-bottom: 16px;
}
.team-section:last-child {
  margin-bottom: 0;
}

.team-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.team-label {
  font-size: 16px;
  font-weight: 500;
  letter-spacing: 0.1em;
}

.btn-add-member {
  padding: 2px 14px;
  border: 1px dashed #d8d0c4;
  background: transparent;
  cursor: pointer;
  font-family: inherit;
  font-size: 12px;
  color: #666;
  transition: all 0.25s;
}
.btn-add-member:hover {
  border-color: #2c2a29;
  color: #2c2a29;
}

.vs-divider {
  text-align: center;
  font-size: 24px;
  color: #9e2a2b;
  padding: 8px 0;
  letter-spacing: 0.2em;
}

/* ===== 成员 ===== */
.member-group {
  border: 1px solid #d8d0c4;
  padding: 12px 16px;
  margin-top: 10px;
  background: #fff;
}

.member-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.member-label {
  font-size: 13px;
  font-weight: 500;
  color: #2c2a29;
}

.btn-remove-member {
  background: none;
  border: none;
  color: #f44336;
  cursor: pointer;
  font-size: 12px;
}
.btn-remove-member:hover {
  text-decoration: underline;
}

.member-row {
  display: grid;
  grid-template-columns: 1fr 2fr;
  gap: 12px;
}

.member-field {
  display: flex;
  flex-direction: column;
}

.member-field input,
.member-field select {
  padding: 6px 12px;
  border: 1px solid #d8d0c4;
  background: #fff;
  font-family: inherit;
  font-size: 13px;
  color: #2c2a29;
  outline: none;
}
.member-field input:focus,
.member-field select:focus {
  border-color: #2c2a29;
}

.member-field input:disabled {
  background: #f0ebe5;
  color: #999;
}

.sub-label {
  font-size: 12px;
  color: #999;
  letter-spacing: 0.05em;
  margin-bottom: 2px;
}

/* ===== 搜索 ===== */
.search-wrapper {
  display: flex;
  gap: 6px;
}
.search-wrapper input {
  flex: 1;
  padding: 6px 12px;
  border: 1px solid #d8d0c4;
  background: #fff;
  font-family: inherit;
  font-size: 13px;
  color: #2c2a29;
  outline: none;
}
.search-wrapper input:focus {
  border-color: #2c2a29;
}

.btn-search {
  width: 34px;
  height: 34px;
  border: 1px solid #d8d0c4;
  background: #fff;
  cursor: pointer;
  font-size: 14px;
  color: #999;
  transition: all 0.25s;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.btn-search:hover:not(:disabled) {
  border-color: #2c2a29;
  color: #2c2a29;
}
.btn-search:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.userId-input-wrapper {
  display: flex;
  gap: 6px;
}
.userId-input-wrapper input {
  flex: 1;
}
.userId-input-wrapper button {
  padding: 4px 12px;
  border: 1px solid #d8d0c4;
  background: #fff;
  cursor: pointer;
  white-space: nowrap;
  font-size: 12px;
  color: #666;
}
.userId-input-wrapper button:hover {
  border-color: #2c2a29;
  color: #2c2a29;
}

.searching-hint {
  font-size: 12px;
  color: #999;
  margin-top: 2px;
}

/* ===== 搜索结果 ===== */
.search-results {
  max-height: 160px;
  overflow-y: auto;
  border: 1px solid #e8e0d8;
  border-radius: 4px;
  background: #fff;
  margin-top: 4px;
}

.search-result-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 12px;
  border-bottom: 1px solid #f0ebe5;
}
.search-result-item:last-child {
  border-bottom: none;
}
.search-result-item:hover {
  background: #f8f5f0;
}

.result-info strong {
  font-size: 13px;
  color: #2c2a29;
}
.result-author {
  font-size: 12px;
  color: #999;
  margin-left: 6px;
}

.btn-add-oc {
  padding: 1px 12px;
  border: 1px solid #d8d0c4;
  background: transparent;
  cursor: pointer;
  font-size: 12px;
  color: #666;
  transition: all 0.25s;
  white-space: nowrap;
}
.btn-add-oc:hover:not(:disabled) {
  border-color: #9e2a2b;
  color: #9e2a2b;
}
.btn-add-oc:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  color: #999;
}

/* ===== 已选标签 ===== */
.selected-ocs {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 6px;
}

.selected-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 10px;
  background: #ede8e2;
  border: 1px solid #d8d0c4;
  border-radius: 4px;
  font-size: 12px;
  color: #2c2a29;
}

.selected-tag.opponent-tag {
  background: #f5ece8;
  border-color: #d8c8c0;
}

.tag-remove {
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  font-size: 14px;
  padding: 0 2px;
  line-height: 1;
}
.tag-remove:hover {
  color: #9e2a2b;
}

/* ===== 其他 ===== */
.form-group {
  margin-bottom: 16px;
}
.form-group:last-child {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  font-size: 13px;
  letter-spacing: 0.08em;
  margin-bottom: 4px;
  color: #2c2a29;
}

.required {
  color: #9e2a2b;
}

.form-group input,
.form-group textarea {
  width: 100%;
  padding: 8px 14px;
  border: 1px solid #d8d0c4;
  background: #fff;
  font-family: inherit;
  font-size: 14px;
  color: #2c2a29;
  outline: none;
  transition: border-color 0.25s;
}
.form-group input:focus,
.form-group textarea:focus {
  border-color: #2c2a29;
}

.form-group textarea {
  resize: vertical;
  min-height: 80px;
  line-height: 1.6;
}

.char-count {
  display: block;
  text-align: right;
  font-size: 12px;
  color: #ccc;
  margin-top: 2px;
}

.hint {
  font-size: 12px;
  color: #bbb;
  letter-spacing: 0.05em;
  margin: 4px 0 0;
}

/* ===== 上传 ===== */
.upload-area {
  border: 1px dashed #d8d0c4;
  background: #fff;
  border-radius: 4px;
  cursor: pointer;
  transition: border-color 0.25s;
  min-height: 120px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.upload-area:hover {
  border-color: #2c2a29;
}

.cover-preview {
  position: relative;
  width: 100%;
  max-width: 240px;
  padding: 8px;
}
.cover-preview img {
  width: 100%;
  aspect-ratio: 16/9;
  object-fit: cover;
  border: 1px solid #d8d0c4;
  border-radius: 4px;
}

.remove-btn {
  position: absolute;
  top: 0;
  right: 0;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: none;
  background: #9e2a2b;
  color: #fff;
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.25s;
}
.remove-btn:hover {
  background: #2c2a29;
}

.upload-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 16px;
  color: #bbb;
}
.upload-icon {
  font-size: 28px;
}
.upload-hint {
  font-size: 12px;
  color: #ddd;
}

.upload-status {
  font-size: 13px;
  color: #999;
  margin-top: 4px;
}

/* ===== 单选 ===== */
.radio-group {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.radio-option {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 12px 16px;
  border: 1px solid #d8d0c4;
  background: #fff;
  cursor: pointer;
  transition: all 0.25s;
  border-radius: 4px;
}
.radio-option:hover {
  border-color: #aaa;
}
.radio-option.active {
  border-color: #9e2a2b;
  background: #fcf6f4;
}

.radio-option input[type="radio"] {
  flex-shrink: 0;
  width: 16px;
  height: 16px;
  accent-color: #9e2a2b;
  cursor: pointer;
}

.radio-content {
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.radio-label {
  font-size: 14px;
  color: #2c2a29;
}
.radio-desc {
  font-size: 12px;
  color: #bbb;
}

/* ===== 提交 ===== */
.form-actions {
  display: flex;
  gap: 12px;
  padding-top: 20px;
  border-top: 2px solid #d8d0c4;
}

.btn-cancel {
  padding: 10px 28px;
  border: 1px solid #d8d0c4;
  background: transparent;
  cursor: pointer;
  font-family: inherit;
  font-size: 14px;
  color: #666;
  transition: all 0.25s;
}
.btn-cancel:hover {
  border-color: #9e2a2b;
  color: #9e2a2b;
}

.btn-submit {
  flex: 1;
  padding: 10px 28px;
  border: 1px solid #2c2a29;
  background: #2c2a29;
  color: #f5f0eb;
  font-family: inherit;
  font-size: 14px;
  letter-spacing: 0.12em;
  cursor: pointer;
  transition: all 0.25s;
}
.btn-submit:hover:not(:disabled) {
  background: #f5f0eb;
  color: #2c2a29;
}
.btn-submit:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

/* ===== 响应式 ===== */
@media (max-width: 640px) {
  .create-page {
    padding: 20px 12px 40px;
  }
  .form-section {
    padding: 18px 16px;
  }
  .title-group h1 {
    font-size: 20px;
  }
  .member-row {
    grid-template-columns: 1fr;
  }
  .form-actions {
    flex-direction: column-reverse;
  }
  .cover-preview {
    max-width: 100%;
  }
  .search-wrapper {
    flex-wrap: wrap;
  }
}
</style>