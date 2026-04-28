<script setup lang="ts">
import { ref, computed } from 'vue'
import { useUserStore } from '../../stores/user' 
import { useCos } from '../../composables/useCos'
import request from '../../utils/request'
// 1. 引入新组件
import ProfileCard from './ProfileCard.vue'

const userStore = useUserStore()
const { uploadFile, isUploading } = useCos()

const fileInput = ref<HTMLInputElement | null>(null)
const defaultAvatar = 'https://api.dicebear.com/7.x/avataaars/svg?seed=Felix'

const expPercentage = computed(() => {
  if (!userStore.userInfo) return 0
  return (userStore.userInfo.experience % 1000) / 10
})

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

    await request.patch('/User/update-profile', { 
      avatar: newAvatarUrl 
    })

    if (userStore.userInfo) {
      userStore.userInfo.avatar = newAvatarUrl
    }
  } catch (error: any) {
    console.error('头像处理失败:', error)
    alert(error.friendlyMessage || '上传失败，请稍后再试')
  } finally {
    target.value = ''
  }
}

// 退出登录逻辑
const handleLogout = () => {
  localStorage.removeItem('token')
  window.location.reload()
}

// 编辑资料逻辑（预留）
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
          </div>
          <div class="stat-item">
            <span class="stat-label">累积积分</span>
            <span class="stat-value">{{ userStore.userInfo.points }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">连续签到</span>
            <span class="stat-value">{{ userStore.userInfo.maxSignStreak }} <small>天</small></span>
          </div>
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
/* 保持原有的布局 CSS，删除 .profile-card 相关的样式，因为已经移入组件 */
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
.stat-item { background: #f6f8fa; padding: 20px; border-radius: 12px; display: flex; flex-direction: column; }
.stat-label { font-size: 0.85rem; color: #57606a; margin-bottom: 8px; }
.stat-value { font-size: 1.5rem; font-weight: 700; font-family: monospace; }

.exp-bar { width: 100%; height: 6px; background: #eaeef2; border-radius: 3px; margin-top: 12px; overflow: hidden; }
.exp-progress { height: 100%; background: #24292f; transition: width 0.3s; }

@media (max-width: 1024px) {
  .content-layout { flex-direction: column; }
  .side-widgets { width: 100%; }
  .stats-grid { grid-template-columns: 1fr; }
}
</style>