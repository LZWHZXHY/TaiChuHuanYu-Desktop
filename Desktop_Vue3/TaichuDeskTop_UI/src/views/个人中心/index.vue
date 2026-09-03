<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useUserStore } from '../../stores/user' 
import { useCos } from '../../composables/useCos'
import request from '../../utils/request'
import ProfileCard from './ProfileCard.vue'
import PreferenceSettings from './PreferenceSettings.vue' // 🌟 引入偏好设置组件

const userStore = useUserStore()
const { uploadFile, isUploading } = useCos()

const fileInput = ref<HTMLInputElement | null>(null)
const defaultAvatar = 'https://api.dicebear.com/7.x/avataaars/svg?seed=Felix'

// 🌟 新增 Tab 状态控制
const activeTab = ref('overview') // 'overview' | 'settings'

// 经验条计算（无改动）
const expPercentage = computed(() => {
  const exp = userStore.userInfo?.experience || 0
  if (exp <= 0) return 0
  const currentLevel = Math.floor(Math.sqrt(exp / 100))
  const currentLevelStartExp = Math.pow(currentLevel, 2) * 100
  const nextLevelStartExp = Math.pow(currentLevel + 1, 2) * 100
  const progressInLevel = exp - currentLevelStartExp
  const levelExpRange = nextLevelStartExp - currentLevelStartExp
  const percentage = (progressInLevel / levelExpRange) * 100
  return Math.min(Math.max(percentage, 0), 100)
})

// ---------- 强制刷新：完全替换 userInfo ----------
const fetchFullProfile = async () => {
  console.log('🔄 开始获取完整资料...')
  try {
    const res = await request.get('/User/me')
    console.log('✅ 请求成功，原始响应:', res)

    // 🌟 关键修复：res 本身就是用户数据，不需要再取 res.data
    const userData = res
    console.log('📦 解析后的用户数据:', userData)

    if (userData && userData.username) {
      // 完全替换，确保响应式
      userStore.userInfo = userData
      localStorage.setItem('userInfo', JSON.stringify(userData))
      console.log('💾 已更新 store 和 localStorage')
    } else {
      console.warn('⚠️ 响应数据无效:', userData)
    }
  } catch (error) {
    console.error('❌ 请求失败:', error)
  }
}

// ---------- 页面加载 ----------
onMounted(() => {
  console.log('🚀 个人页面已挂载')
  // 不管有没有数据，都强制刷新一次（保证最新）
  fetchFullProfile()
})

// ---------- 头像上传 ----------
const triggerUpload = () => {
  if (isUploading.value) return
  fileInput.value?.click()
}

const handleFileChange = async (e: Event) => {
  const target = e.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return
  if (!file.type.startsWith('image/')) {
    alert('请选择有效的图片文件')
    return
  }
  try {
    const result = await uploadFile(file, 'avatars')
    const newAvatarUrl = result.url
    await request.patch('/User/update-profile', { avatar: newAvatarUrl })
    if (userStore.userInfo) {
      userStore.userInfo.avatar = newAvatarUrl
      localStorage.setItem('userInfo', JSON.stringify(userStore.userInfo))
    }
  } catch (error: any) {
    console.error('头像处理失败:', error)
    alert(error.friendlyMessage || '上传失败，请稍后再试')
  } finally {
    target.value = ''
  }
}

// ---------- 退出登录 ----------
const handleLogout = () => {
  localStorage.removeItem('token')
  window.location.reload()
}

const handleEditProfile = () => {
  console.log('打开编辑资料弹窗')
}
</script>

<template>
  <article class="user-center" v-if="userStore.userInfo">
    <div class="content-layout">
      <section class="main-content">
        <header class="user-header">
          <div class="avatar-wrapper" @click="triggerUpload">
            <img 
              :src="userStore.userInfo.avatar || defaultAvatar" 
              alt="Avatar" 
              class="avatar" 
              :class="{ 'uploading': isUploading }"
            />
            <div v-if="isUploading" class="upload-mask">
              <div class="spinner"></div>
              <span>上传中...</span>
            </div>
            <input 
              type="file" 
              ref="fileInput" 
              style="display: none" 
              accept="image/*" 
              @change="handleFileChange" 
            />
          </div>

          <div class="user-info">
            <h1>{{ userStore.userInfo.username }}</h1>
            <p class="user-bio">{{ userStore.userInfo.address || '这个道友很神秘，什么都没留下。' }}</p>
          </div>
        </header>

        <div class="stats-grid">
          <div class="stat-item">
            <span class="stat-label">当前等级</span>
            <span class="stat-value">Lv.{{ userStore.userInfo.level }}</span>
            <div class="exp-bar">
              <div class="exp-progress" :style="{ width: expPercentage + '%' }"></div>
            </div>
            <div class="exp-text-wrapper">
              <span class="exp-val">{{ userStore.userInfo.experience }} 经验</span>
            </div>
          </div>

          <div class="stat-item">
            <span class="stat-label">佩戴头衔</span>
            <span class="stat-value title-value">{{ userStore.userInfo.title || '太初散修' }}</span>
          </div>

          <div class="stat-item">
            <span class="stat-label">连续签到</span>
            <span class="stat-value">{{ userStore.userInfo.maxSignStreak }} <small>天</small></span>
          </div>
        </div>

        <!-- 🌟 新增：内容切换 Tab -->
        <div class="content-tabs">
          <button 
            class="tab-btn" 
            :class="{ active: activeTab === 'overview' }" 
            @click="activeTab = 'overview'"
          >
            近期动态
          </button>
          <button 
            class="tab-btn" 
            :class="{ active: activeTab === 'settings' }" 
            @click="activeTab = 'settings'"
          >
            偏好设置
          </button>
        </div>

        <!-- 🌟 新增：Tab 内容区 -->
        <div class="tab-content-area">
          <div v-if="activeTab === 'overview'" class="overview-placeholder">
            <p class="empty-hint">暂无近期动态</p>
          </div>
          
          <PreferenceSettings v-if="activeTab === 'settings'" />
        </div>

      </section>
      
      <aside class="side-widgets">
        <ProfileCard 
          :userInfo="userStore.userInfo" 
          @edit="handleEditProfile"
          @logout="handleLogout"
        />
      </aside>
    </div>
  </article>
  
  <div v-else class="loading-state">
    载入灵脉数据中...
  </div>
</template>

<style scoped>
.user-center { width: 100%; color: #24292f; }
.content-layout { display: flex; justify-content: space-between; gap: 40px; }
.main-content { flex: 1; max-width: 800px; }
.side-widgets { width: 350px; flex-shrink: 0; }

.user-header { display: flex; align-items: center; gap: 24px; margin-bottom: 40px; }

.avatar-wrapper {
  position: relative;
  width: 100px;
  height: 100px;
  cursor: pointer;
  overflow: hidden;
  border-radius: 12px;
  transition: transform 0.2s ease;
}
.avatar-wrapper:hover { transform: translateY(-2px); }
.avatar { width: 100%; height: 100%; object-fit: cover; border: 4px solid #f6f8fa; border-radius: 12px; background: #eee; }
.avatar.uploading { filter: blur(2px) brightness(0.8); }

.upload-mask {
  position: absolute;
  top: 0; left: 0; width: 100%; height: 100%;
  background: rgba(0, 0, 0, 0.4);
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  color: #fff; font-size: 12px; gap: 8px;
}

.spinner { width: 16px; height: 16px; border: 2px solid #fff; border-top-color: transparent; border-radius: 50%; animation: spin 0.8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

.user-info h1 { font-size: 2rem; font-weight: 800; margin: 0; }
.user-bio { color: #57606a; margin-top: 8px; }

.stats-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; margin-bottom: 40px; }
.stat-item { background: #f6f8fa; padding: 20px; border-radius: 12px; display: flex; flex-direction: column; justify-content: center; }
.stat-label { font-size: 0.85rem; color: #57606a; margin-bottom: 8px; }
.stat-value { font-size: 1.5rem; font-weight: 700; font-family: monospace; }
.title-value { font-size: 1.1rem; color: #cf8a05; } 

.exp-bar { width: 100%; height: 6px; background: #eaeef2; border-radius: 3px; margin-top: 12px; overflow: hidden; }
.exp-progress { height: 100%; background: #24292f; transition: width 0.3s; }

.exp-text-wrapper { text-align: right; margin-top: 4px; }
.exp-val { font-size: 12px; color: #8c959f; }

/* 🌟 新增 Tab 样式 */
.content-tabs {
  display: flex;
  gap: 20px;
  border-bottom: 1px solid #eaeef2;
  margin-bottom: 24px;
}

.tab-btn {
  background: none;
  border: none;
  padding: 10px 4px;
  font-size: 1rem;
  font-weight: 600;
  color: #57606a;
  cursor: pointer;
  position: relative;
  transition: color 0.3s;
}

.tab-btn:hover {
  color: #24292f;
}

.tab-btn.active {
  color: #24292f;
}

/* 墨划风格底部指示线 */
.tab-btn.active::after {
  content: '';
  position: absolute;
  bottom: -1px;
  left: 0;
  width: 100%;
  height: 2px;
  background: #24292f;
  border-radius: 2px;
}

.tab-content-area {
  min-height: 200px;
}

.overview-placeholder {
  background: #f6f8fa;
  border-radius: 12px;
  padding: 60px 20px;
  text-align: center;
  animation: fadeIn 0.4s ease;
}

.empty-hint {
  color: #8c959f;
  font-size: 0.9rem;
}

.loading-state { padding: 100px; text-align: center; color: #57606a; font-style: italic; }

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(5px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 1024px) {
  .content-layout { flex-direction: column; }
  .side-widgets { width: 100%; order: -1; } 
  .stats-grid { grid-template-columns: 1fr; }
}
</style>