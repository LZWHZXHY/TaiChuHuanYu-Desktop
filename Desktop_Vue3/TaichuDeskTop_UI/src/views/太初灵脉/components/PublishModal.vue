<template>
  <transition name="fade">
    <div v-if="modelValue" class="md-overlay" @click.self="$emit('update:modelValue', false)">
      <div class="md-container">
        <header class="md-header">
          <div class="md-title">
            <span class="md-label">发布碎片</span>
            <h1>{{ publishTitle }}</h1>
          </div>
          <button class="md-close" @click="$emit('update:modelValue', false)">✕</button>
        </header>

        <div class="md-body">
          <div class="simple-confirm-text">
            即将将此 <strong>{{ displayType }}</strong> 固化至灵脉广场。
          </div>

          <transition name="fade">
            <div v-if="noteType === 'wiki'" class="extra-zone">
              <div class="form-group">
                <label>所属界域 (Category)</label>
                <select v-model="wikiData.categoryId" :disabled="isLoadingCategories">
                  <option value="" disabled>选择界域...</option>
                  <option v-for="cat in flatCategories" :key="cat.id" :value="cat.id">
                    {{ '—'.repeat(cat.level) }} {{ cat.name }}
                  </option>
                </select>
              </div>

              <div class="form-group">
                <label>意象标签 (Tags)</label>
                <div class="tag-input-box">
                  <span v-for="(tag, i) in wikiData.tags" :key="i" class="tag-pill">
                    {{ tag }} <button @click="removeTag(i)">×</button>
                  </span>
                  <input v-model="tagInput" @keydown.enter.prevent="addTag" placeholder="输入标签按回车" />
                </div>
              </div>
            </div>
          </transition>

          <transition name="fade">
            <div v-if="noteType === 'doc'" class="extra-zone">
              <div class="form-group">
                <label>归属协作项目 (Project)</label>
                <select v-model="projectData.projectId" :disabled="isLoadingProjects">
                  <option value="" disabled>选择要发布到的项目...</option>
                  <option v-for="proj in availableProjects" :key="proj.id" :value="proj.id">
                    {{ proj.name }}
                  </option>
                </select>
              </div>
            </div>
          </transition>
        </div>

        <footer class="md-footer">
          <button class="btn-cancel" @click="$emit('update:modelValue', false)">放弃</button>
          <button 
            class="btn-primary" 
            :disabled="isProcessing || !canSubmit"
            @click="handleConfirm"
          >
            {{ isProcessing ? '正在锚定...' : '确认发布' }}
          </button>
        </footer>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue';
import { wikiApi, type IWikiCategory } from '@/api/Wiki';
import { lingmaiApi } from '../../../api/lingmai';
import projectService from '@/api/projectService'; 

const props = defineProps<{ 
  modelValue: boolean; 
  noteId: string; 
  noteType: string; 
  spaceName: string; 
}>();

const emit = defineEmits(['update:modelValue', 'success']);

const isProcessing = ref(false);

// Wiki 数据
const wikiData = reactive({ categoryId: 3, tags: [] as string[] }); 
const tagInput = ref('');
const rawCategories = ref<IWikiCategory[]>([]);
const isLoadingCategories = ref(false);

// Doc (项目文档) 数据
const projectData = reactive({ projectId: '' });
const availableProjects = ref<{ id: string; name: string }[]>([]); // 🌟 显式锁死强类型约束
const isLoadingProjects = ref(false);

const displayType = computed(() => {
  const map: Record<string, string> = { 
    'wiki': '百科词条', 'char': '角色档案', 
    'post': '太初随笔', 'blog': '博客', 'art': '艺术画廊', 'doc': '项目文档' 
  };
  return map[props.noteType] || '灵脉碎片';
});

const publishTitle = computed(() => {
  if (props.noteType === 'wiki') return '发布至百科宇宙';
  if (props.noteType === 'doc') return '发布至协作项目'; 
  return '固化至灵脉广场';
});

const flatCategories = computed(() => {
  const result: (IWikiCategory & { level: number })[] = [];
  const buildTree = (parentId: number | null, level: number) => {
    rawCategories.value.filter(c => c.parentId === parentId).forEach(c => {
      result.push({ ...c, level });
      buildTree(c.id, level + 1);
    });
  };
  buildTree(null, 0);
  return result;
});

// 拦截逻辑：不同类型校验不同必填项
const canSubmit = computed(() => {
  if (props.noteType === 'wiki') return !!wikiData.categoryId;
  if (props.noteType === 'doc') return !!projectData.projectId;
  return true;
});

const addTag = () => {
  const t = tagInput.value.trim();
  if (t && !wikiData.tags.includes(t)) wikiData.tags.push(t);
  tagInput.value = '';
};

const removeTag = (i: number) => wikiData.tags.splice(i, 1);

const handleConfirm = async () => {
  console.log("即将发送的发布形态:", props.noteType);

  isProcessing.value = true;
  try {
    const payload: any = {
      type: props.noteType
    };

    if (props.noteType === 'wiki') {
      payload.categoryId = wikiData.categoryId;
      payload.tags = wikiData.tags;
    } else if (props.noteType === 'doc') {
      payload.projectId = projectData.projectId; 
    }

    await lingmaiApi.publishNote(props.noteId, payload);

    emit('success');
    emit('update:modelValue', false);
  } catch (err) { 
    console.error(err);
    alert('发布系统感应到干扰'); 
  } finally {
    isProcessing.value = false;
  }
};

onMounted(async () => {
  // --- 拉取 Wiki 界域 ---
  if (props.noteType === 'wiki') {
    console.log("【调试】发布弹窗已挂载，开始拉取界域...");
    isLoadingCategories.value = true;
    try {
      const data = await wikiApi.getCategories();
      rawCategories.value = data || [];
    } catch (error) {
      console.error("【调试】界域读取崩溃:", error);
    } finally {
      isLoadingCategories.value = false;
    }
  }

  // --- 🌟 拉取参与的项目列表 ---
  if (props.noteType === 'doc') {
    isLoadingProjects.value = true;
    try {
      // 1. 通过 unknown 优雅解开拦截器的响应包装，彻底根除 never 报错
      const res = await projectService.getMyProjects() as unknown as any;
      
      // 2. 智能化平铺数据，同时包容 axios 拦截器处理完或未处理的两种状态
      const rawList = Array.isArray(res) ? res : (res?.data || []);
      
      // 3. 🌟【核心对齐逻辑】：强制将后端返回的属性（不论大写还是小写）统一清洗为模板需要的小写
      availableProjects.value = rawList.map((item: any) => ({
        id: item.id || item.Id,
        name: item.name || item.Name
      }));
      
      console.log("【调试】清洗后的可用协作项目列表:", availableProjects.value);
    } catch (error) {
      console.error("获取项目列表失败:", error);
      alert("无法加载协作项目列表");
    } finally {
      isLoadingProjects.value = false;
    }
  }
});
</script>

<style scoped>
.md-overlay { position: fixed; inset: 0; background: rgba(255,255,255,0.8); backdrop-filter: blur(8px); display: flex; align-items: center; justify-content: center; z-index: 5000; }
.md-container { width: 440px; background: #fff; padding: 32px; border-radius: 20px; box-shadow: 0 20px 40px rgba(0,0,0,0.08); }
.md-header { margin-bottom: 32px; display: flex; justify-content: space-between; }
.md-label { font-size: 10px; color: #86868b; text-transform: uppercase; letter-spacing: 0.1em; }
.md-title h1 { font-size: 20px; font-weight: 700; margin: 4px 0 0; }
.simple-confirm-text { padding: 20px; color: #1d1d1f; font-size: 14px; text-align: center; border: 1px solid #f2f2f7; border-radius: 12px; }
.extra-zone { padding-top: 10px; }
.form-group { margin-bottom: 16px; }
.form-group label { display: block; font-size: 11px; color: #86868b; margin-bottom: 8px; }
select { width: 100%; border: 1px solid #f2f2f7; padding: 8px; border-radius: 6px; font-size: 13px; outline: none; }
.tag-input-box { border: 1px solid #f2f2f7; border-radius: 6px; padding: 4px; display: flex; flex-wrap: wrap; gap: 4px; }
.tag-pill { background: #f2f2f7; padding: 2px 8px; border-radius: 4px; font-size: 11px; display: flex; align-items: center; gap: 4px; }
.tag-pill button { border: none; background: none; cursor: pointer; }
.tag-input-box input { border: none; padding: 4px; font-size: 12px; outline: none; flex: 1; }
.md-footer { margin-top: 32px; display: flex; gap: 12px; }
.btn-cancel { flex: 1; background: none; border: none; color: #86868b; font-size: 13px; cursor: pointer; }
.btn-primary { flex: 2; padding: 10px; background: #000; color: #fff; border: none; border-radius: 8px; font-weight: 600; cursor: pointer; }
.btn-primary:disabled { opacity: 0.4; cursor: not-allowed; }
.md-close { background: none; border: none; color: #c7c7cc; cursor: pointer; }
</style>