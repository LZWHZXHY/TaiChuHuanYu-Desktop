<template>
  <div class="activity-module">
    <aside class="sidebar">
      <div class="brand">
        <i class="fas fa-cubes"></i>
        <span>互动中心</span>
      </div>

      <nav class="nav-links">
        <!-- ===== 首页入口 ===== -->
        <router-link to="/activity" exact-active-class="active">
          <i class="fas fa-home"></i><span>首页</span>
        </router-link>

        <!-- ===== 打卡专区 ===== -->
        <div class="nav-section-title">🔥 打卡专区</div>
        <router-link to="/activity/checkin" active-class="active">
          <i class="fas fa-compass"></i><span>发现广场</span>
        </router-link>
        <router-link to="/activity/checkin/create" active-class="active">
          <i class="fas fa-plus-circle"></i><span>发起打卡</span>
        </router-link>
        <router-link to="/activity/checkin/my" active-class="active">
          <i class="fas fa-user-friends"></i><span>我的打卡</span>
        </router-link>
        <router-link to="/activity/checkin/rank" active-class="active">
          <i class="fas fa-trophy"></i><span>排行榜</span>
        </router-link>

        <!-- ===== 问卷专区 ===== -->
        <div class="nav-section-title">📋 问卷专区</div>
        <router-link to="/activity/survey" active-class="active">
          <i class="fas fa-clipboard-list"></i><span>问卷列表</span>
        </router-link>
        <!-- ✅ 只有 Survey_Manage 或 SuperAdmin 权限的用户可以看到“创建问卷” -->
        <router-link 
          v-if="userStore.canManageSurvey"
          to="/activity/survey/create" 
          active-class="active"
        >
          <i class="fas fa-plus-circle"></i><span>创建问卷</span>
        </router-link>
        <!-- ✅ 新增：问卷管理入口（只有 Survey_Manage 或 SuperAdmin 可见） -->
        <router-link 
          v-if="userStore.canManageSurvey"
          to="/activity/survey/manage" 
          active-class="active"
        >
          <i class="fas fa-cog"></i><span>问卷管理</span>
        </router-link>

        <!-- ===== 预留扩展 ===== -->
        <!-- 
        <div class="nav-section-title">🚀 更多专区</div>
        <router-link to="/activity/quiz" active-class="active">
          <i class="fas fa-question-circle"></i><span>问卷调查</span>
        </router-link>
        -->
      </nav>

      <div class="user-profile">
        <div class="avatar">螣蛇</div>
        <div class="user-info">
          <p class="name">螣蛇</p>
          <p class="role">开发者 · 活跃</p>
        </div>
      </div>
    </aside>

    <main class="viewport">
      <div class="content-container">
        <router-view v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { useUserStore } from '@/stores/user'

const userStore = useUserStore()
</script>


<style scoped>
.activity-module {
  display: flex;
  height: auto;
  width: 100%;
  background: #fafafa;
  overflow: hidden;
}

.sidebar {
  width: 220px;
  flex-shrink: 0;
  background: #fff;
  border-right: 1px solid #eee;
  padding: 24px 16px;
  display: flex;
  flex-direction: column;
}

.brand {
  font-size: 1.2rem;
  font-weight: 600;
  letter-spacing: -0.3px;
  padding-left: 8px;
  margin-bottom: 28px;
  display: flex;
  align-items: center;
  gap: 8px;
  color: #1f2937;
}

.brand i {
  color: #6366f1;
  font-size: 1.2rem;
}

.nav-links {
  display: flex;
  flex-direction: column;
  gap: 2px;
  flex: 1;
}

.nav-links a {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  border-radius: 8px;
  color: #6b7280;
  text-decoration: none;
  font-weight: 500;
  font-size: 0.9rem;
  transition: background 0.2s, color 0.2s;
}

.nav-links a i {
  width: 18px;
  font-size: 1rem;
  text-align: center;
}

.nav-links a:hover {
  background: #f3f4f6;
  color: #1f2937;
}

.nav-links a.active {
  background: #1f2937;
  color: #fff;
}

.nav-links a.active i {
  color: #fff;
}

/* ===== 新增：分区标题 ===== */
.nav-section-title {
  font-size: 0.6rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #9ca3af;
  padding: 12px 14px 4px;
  font-weight: 600;
  margin-top: 4px;
}

.nav-section-title:first-of-type {
  margin-top: 8px;
}

.user-profile {
  margin-top: auto;
  display: flex;
  align-items: center;
  gap: 12px;
  padding-top: 16px;
  border-top: 1px solid #f3f4f6;
}

.avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: #6366f1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-weight: 600;
  font-size: 0.8rem;
  flex-shrink: 0;
}

.user-info .name {
  font-weight: 500;
  font-size: 0.85rem;
  color: #1f2937;
}

.user-info .role {
  font-size: 0.7rem;
  color: #9ca3af;
}

.viewport {
  flex: 1;
  overflow-y: auto;
  padding: 24px 32px 32px 32px;
  display: flex;
  justify-content: center;
}

.content-container {
  width: 100%;
  max-width: 1200px;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(4px);
}

@media (max-width: 768px) {
  .sidebar {
    width: 72px;
    padding: 16px 8px;
  }

  .sidebar .brand span,
  .sidebar .nav-links a span,
  .sidebar .user-info,
  .sidebar .nav-section-title {
    display: none;
  }

  .sidebar .brand {
    justify-content: center;
    padding: 0;
    margin-bottom: 24px;
  }

  .sidebar .nav-links a {
    justify-content: center;
    padding: 10px;
  }

  .sidebar .nav-links a i {
    font-size: 1.2rem;
    margin: 0;
  }

  .sidebar .user-profile {
    justify-content: center;
  }

  .viewport {
    padding: 16px;
  }
}
</style>