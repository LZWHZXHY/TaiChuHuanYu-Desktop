<template>
  <div class="project-create workbench-skin">
    <div class="container">
      <!-- 返回按钮 -->
      <button class="back-btn" @click="goBack">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="20" height="20">
          <line x1="19" y1="12" x2="5" y2="12"/>
          <polyline points="12 19 5 12 12 5"/>
        </svg>
        返回
      </button>

      <!-- 主标题 -->
      <header class="page-header">
        <h1>创建新世界</h1>
        <p>从一个想法开始，构建属于你的世界观</p>
      </header>

      <!-- 🆕 配额信息横幅 -->
      <WorldQuotaBanner :quota-info="quotaInfo" :upgrading="upgrading" @upgrade="upgradeWorldQuota" />

      <!-- 创建表单 -->
      <div class="create-layout">
        <!-- 左侧：表单 -->
        <section class="form-section">
          <form @submit.prevent="handleSubmit">
            <!-- 世界名称 -->
            <div class="field-group">
              <label for="name">世界名称 <span class="required">*</span></label>
              <input
                required
                id="name"
                v-model="form.name"
                type="text"
                placeholder="给世界起一个响亮的名字"
                maxlength="100"
                autofocus
              />
              <span class="counter">{{ form.name.length }}/100</span>
            </div>

            <!-- 项目描述 -->
            <div class="field-group">
              <label for="description">简介</label>
              <textarea
                id="description"
                v-model="form.description"
                rows="4"
                placeholder="简单描述这个世界的故事背景、风格或核心设定"
                maxlength="500"
              ></textarea>
              <span class="counter">{{ form.description.length }}/500</span>
            </div>

            <!-- 可见性 -->
            <div class="field-group">
              <label>可见性</label>
              <div class="visibility-options">
                <button type="button"
                  class="visibility-option"
                  :class="{ active: !form.isPublic }"
                  :aria-pressed="!form.isPublic"
                  @click="form.isPublic = false"
                >
                  <span class="icon">🔒</span>
                  <div>
                    <strong>私有</strong>
                    <p>只有你自己能看到</p>
                  </div>
                </button>
                <button type="button"
                  class="visibility-option"
                  :class="{ active: form.isPublic }"
                  :aria-pressed="form.isPublic"
                  @click="form.isPublic = true"
                >
                  <span class="icon">🌐</span>
                  <div>
                    <strong>公开</strong>
                    <p>所有人都可以浏览</p>
                  </div>
                </button>
              </div>
            </div>

            <!-- 提交按钮 -->
            <div class="form-actions">
              <button type="button" class="btn-outline" @click="goBack">取消</button>
              <button type="submit" class="btn-primary" :disabled="submitting || quotaFull">
                {{ submitting ? '创建中...' : quotaFull ? '配额已满，请扩容' : '创建世界' }}
              </button>
            </div>
          </form>
        </section>


      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import WorldQuotaBanner from './世界观组件/WorldQuotaBanner.vue';
import { ref, reactive, onMounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '../../stores/world';
import { worldApi, type WorldQuota } from '@/api/worldApi';

const router = useRouter();
const store = useWorldStore();

const submitting = ref(false);
const quotaInfo = ref<WorldQuota | null>(null);

const form = reactive({
  name: '',
  description: '',
  isPublic: false,
});


// ===== 🆕 判断配额是否已满 =====
const quotaFull = computed(() => {
  if (!quotaInfo.value) return false;
  return quotaInfo.value.remainingWorldCount <= 0;
});

// ===== 🆕 加载配额信息 =====
const loadQuota = async () => {
  try {
    quotaInfo.value = await worldApi.getQuota();
  } catch (error) {
    console.error('获取配额信息失败:', error);
  }
};

// ===== 🆕 跳转到配额管理 =====
const upgrading = ref(false);
const upgradeWorldQuota = async () => {
  if (upgrading.value || !quotaInfo.value) return;
  upgrading.value = true;
  try {
    await ElMessageBox.confirm(`增加 1 个世界观名额将消耗 ${quotaInfo.value.expCostPerWorldSlot} 经验，是否继续？`, '扩充世界观名额', { confirmButtonText: '确认扩容', cancelButtonText: '取消' });
    const result = await worldApi.upgradeQuota('WorldCount');
    if (result.success) { ElMessage.success(result.message); await loadQuota(); }
    else ElMessage.warning(result.message);
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error('扩容失败，请稍后重试');
  } finally { upgrading.value = false; }
};

const warnQuota = (message: string) => ElMessage.warning({ message, duration: 5000, showClose: true });

// ===== 🆕 提交前检查配额 =====
const handleSubmit = async () => {
  if (submitting.value) return;
  if (!form.name.trim()) {
    ElMessage.warning('请输入世界名称');
    return;
  }

  // ✅ 如果配额已满，直接提示
  if (quotaFull.value && quotaInfo.value) {
    warnQuota(`世界观数量已达上限（${quotaInfo.value.maxWorldCount} 个），请先去扩容`);
    return;
  }

  submitting.value = true;
  // 再次检查配额期间也禁止重复提交。
  try {
    const data = await worldApi.canCreateProject();
    if (!data.canCreate) {
      warnQuota(data.message || '世界观数量已达上限');
      // 刷新配额信息
      await loadQuota();
      submitting.value = false;
      return;
    }
  } catch (error) {
    console.error('检查配额失败:', error);
    // 如果检查失败，继续尝试创建（让后端最终决定）
  }

  try {
    const project = await store.createProject({
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      isPublic: form.isPublic,
    });
    ElMessage.success('🎉 世界创建成功！');
    router.push(`/world/project/${project.id}`);
  } catch (error: any) {
    // ✅ 处理后端返回的配额错误
    if (error?.response?.data?.code === 'QUOTA_EXCEEDED') {
      warnQuota(error.response.data.message || '世界观数量已达上限，请扩容');
      // 刷新配额信息
      await loadQuota();
    } else {
      ElMessage.error('创建失败，请稍后重试');
    }
    console.error(error);
  } finally {
    submitting.value = false;
  }
};

// 返回
const goBack = () => {
  router.back();
};

// ===== 生命周期 =====
onMounted(loadQuota);
</script>

<style scoped>
.project-create { padding: clamp(20px, 4vw, 44px); color: var(--text-primary); }
.container { max-width: 720px; margin: auto; }
button, input, textarea { font: inherit; }
button { cursor: pointer; }button:disabled { opacity: .55; cursor: not-allowed; }
.back-btn { display: flex; align-items: center; gap: 8px; border: 0; background: none; color: var(--text-secondary); padding: 0; }
.page-header { margin: 28px 0; }.page-header h1 { margin: 0 0 12px; font-size: 30px; }.page-header p { color: var(--text-secondary); line-height: 1.7; }
.form-section { margin-top: 24px; padding: clamp(20px, 4vw, 36px); background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 16px; }
.field-group { margin-bottom: 28px; }.field-group>label { display: block; margin-bottom: 10px; font-weight: 600; }
.required { color: var(--danger); }input, textarea { box-sizing: border-box; width: 100%; padding: 12px 14px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); line-height: 1.7; }textarea { resize: vertical; }
.counter { display: block; margin-top: 6px; text-align: right; font-size: 12px; color: var(--text-secondary); }
.visibility-options { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }.visibility-option { display: flex; gap: 10px; align-items: flex-start; text-align: left; padding: 16px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--bg-main); color: var(--text-primary); }.visibility-option.active { border-color: var(--accent-color); background: var(--accent-soft); }.visibility-option p { margin: 6px 0 0; font-size: 13px; line-height: 1.6; color: var(--text-secondary); }
.form-actions { display: flex; justify-content: flex-end; gap: 12px; padding-top: 12px; }.btn-primary,.btn-outline { padding: 11px 22px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); }.btn-primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
button:focus-visible,input:focus-visible,textarea:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
@media(max-width: 480px) { .visibility-options { grid-template-columns: 1fr; } }
</style>
