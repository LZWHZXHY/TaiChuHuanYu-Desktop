<template>
  <div class="activity-module">
    <aside class="sidebar">
      <div class="brand">
        <i class="fas fa-cubes"></i>
        <span>活动中心</span>
      </div>

      <nav class="nav-links">
        <!-- ===== 首页入口 ===== -->
        <router-link to="/activity" exact-active-class="active">
          <i class="fas fa-home"></i><span>首页</span>
        </router-link>

        <!-- ===== 打卡专区 ===== -->
        <div class="nav-section-title">🔥 打卡专区</div>
        <router-link to="/activity/checkin" exact-active-class="active">
          <i class="fas fa-compass"></i><span>发现广场</span>
        </router-link>
        <router-link to="/activity/checkin/create" exact-active-class="active">
          <i class="fas fa-plus-circle"></i><span>发起打卡</span>
        </router-link>
        <router-link to="/activity/checkin/my" exact-active-class="active">
          <i class="fas fa-user-friends"></i><span>我的打卡</span>
        </router-link>
        <span class="nav-unavailable" aria-disabled="true">打卡排行榜 · 待开放</span>

        <!-- ===== 问卷专区 ===== -->
        <div class="nav-section-title">📋 问卷专区</div>
        <router-link to="/activity/survey" exact-active-class="active">
          <i class="fas fa-clipboard-list"></i><span>问卷列表</span>
        </router-link>
        <!-- ✅ 只有 Survey_Manage 或 SuperAdmin 权限的用户可以看到“创建问卷” -->
        <router-link
          v-if="userStore.canManageSurvey"
          to="/activity/survey/create"
          exact-active-class="active"
        >
          <i class="fas fa-plus-circle"></i><span>创建问卷</span>
        </router-link>
        <!-- ✅ 新增：问卷管理入口（只有 Survey_Manage 或 SuperAdmin 可见） -->
        <router-link
          v-if="userStore.canManageSurvey"
          to="/activity/survey/manage"
          exact-active-class="active"
        >
          <i class="fas fa-cog"></i><span>问卷管理</span>
        </router-link>
      </nav>

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


<style src="./theme.css"></style>
