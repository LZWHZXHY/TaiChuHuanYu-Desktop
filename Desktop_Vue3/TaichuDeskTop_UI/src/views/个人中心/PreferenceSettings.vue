<!-- PreferenceSettings.vue -->
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import request from '../../utils/request'

const isSaving = ref(false)
const settings = ref({
  receiveUpdateEmail: true,
  receiveActivityEmail: false,
  weeklyReport: true
})

// 从后端获取当前用户的偏好设置
const fetchSettings = async () => {
  try {
    // 这里的路径对应你后端的 [Route("api/User/Settings")]
    // (假设你的 request.ts 里已经配置了 baseURL: '/api')
    const res: any = await request.get('/Users/Settings')
    
    if (res) {
      settings.value = {
        receiveUpdateEmail: res.receiveUpdateEmail ?? true,
        receiveActivityEmail: res.receiveActivityEmail ?? false,
        weeklyReport: res.weeklyReport ?? true
      }
    }
  } catch (error) {
    console.error('获取偏好设置失败:', error)
  }
}

// 保存修改到后端
const saveSettings = async () => {
  if (isSaving.value) return // 防止重复点击
  
  isSaving.value = true
  try {
    await request.put('/Users/Settings', settings.value)
    alert('偏好设置已成功保存！')
  } catch (error: any) {
    console.error('保存失败:', error)
    // 根据你的拦截器格式，提示具体的错误或默认错误
    alert(error.friendlyMessage || error.response?.data?.message || '保存失败，请稍后再试')
  } finally {
    // 无论成功或失败，最后都解除 loading 状态
    isSaving.value = false
  }
}

onMounted(() => {
  fetchSettings()
})
</script>

<template>
  <div class="preference-panel">
    <div class="settings-group">
      <!-- 设置项 1 -->
      <div class="setting-item">
        <div class="item-info">
          <span class="item-name">系统更新邮件</span>
          <span class="item-desc">当灵脉有重要功能更新或维护时，第一时间通知我</span>
        </div>
        <label class="ink-toggle">
          <input type="checkbox" v-model="settings.receiveUpdateEmail">
          <span class="toggle-slider"></span>
        </label>
      </div>

      <!-- 设置项 2 -->
      <div class="setting-item">
        <div class="item-info">
          <span class="item-name">活动与资讯邮件 (未实装)</span>
          <span class="item-desc">接收最新的社区活动、征稿邀约及精选内容推荐</span>
        </div>
        <label class="ink-toggle">
          <input type="checkbox" v-model="settings.receiveActivityEmail">
          <span class="toggle-slider"></span>
        </label>
      </div>

      <!-- 设置项 3 -->
      <div class="setting-item">
        <div class="item-info">
          <span class="item-name">个人周报推送 (未实装)</span>
          <span class="item-desc">每周一清晨，发送上周的使用数据与灵感简报</span>
        </div>
        <label class="ink-toggle">
          <input type="checkbox" v-model="settings.weeklyReport">
          <span class="toggle-slider"></span>
        </label>
      </div>
    </div>

    <div class="actions">
      <button 
        class="save-btn" 
        :class="{ 'is-loading': isSaving }"
        @click="saveSettings"
        :disabled="isSaving"
      >
        {{ isSaving ? '保存中...' : '保存修改' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.preference-panel {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 12px;
  padding: 24px;
  animation: fadeIn 0.4s ease;
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 0;
  border-bottom: 1px dashed #eaeef2;
}
.setting-item:last-child {
  border-bottom: none;
}

.item-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding-right: 40px;
}

.item-name {
  font-size: 0.95rem;
  color: #24292f;
  font-weight: 600;
}

.item-desc {
  font-size: 0.8rem;
  color: #57606a;
}

.actions {
  margin-top: 24px;
  text-align: right;
}

.save-btn {
  background: #24292f;
  color: #fff;
  border: none;
  padding: 8px 24px;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: opacity 0.3s;
}
.save-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* ===== 极简 Toggle 开关 ===== */
.ink-toggle {
  position: relative;
  display: inline-block;
  width: 44px;
  height: 24px;
  flex-shrink: 0;
}
.ink-toggle input { opacity: 0; width: 0; height: 0; }

.toggle-slider {
  position: absolute;
  cursor: pointer;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: #eaeef2;
  transition: .4s;
  border-radius: 24px;
}
.toggle-slider:before {
  position: absolute;
  content: "";
  height: 18px; width: 18px;
  left: 3px; bottom: 3px;
  background-color: #fff;
  transition: .4s;
  border-radius: 50%;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

/* 选中状态 (使用你全局偏好的深色/红色系，这里用深黑契合徽墨) */
.ink-toggle input:checked + .toggle-slider {
  background-color: #24292f;
}
.ink-toggle input:checked + .toggle-slider:before {
  transform: translateX(20px);
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(5px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>