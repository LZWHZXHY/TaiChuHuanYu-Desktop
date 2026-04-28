<script setup lang="ts">
import { ref, computed } from 'vue'
import request from '../../utils/request'
import type { UserInfo } from '../../api/auth'

// 1. 定义社交链接接口
interface SocialLink {
  platform: string
  url: string
}

const props = defineProps<{
  userInfo: UserInfo 
}>()

const emit = defineEmits(['updateSuccess', 'logout'])

// --- 状态控制 ---
const isEditing = ref(false)
const loading = ref(false)

// --- 编辑表单临时数据 ---
const editForm = ref({
  gender: props.userInfo.gender || '未知',
  mood: props.userInfo.mood || '',
  bio: props.userInfo.bio || '',
  address: props.userInfo.address || '',
  birthday: props.userInfo.birthday || '',
  phoneNumber: props.userInfo.phoneNumber || '', // 补全：电话
  extraConfig: props.userInfo.extraConfig || '',   // 补全：额外配置
  links: [] as SocialLink[]
})

// --- 逻辑处理 ---
// 解析社交链接展示
const parsedLinks = computed<SocialLink[]>(() => {
  if (!props.userInfo.socialLinks) return []
  try {
    return JSON.parse(props.userInfo.socialLinks)
  } catch (e) {
    return []
  }
})

// 进入编辑模式
const startEdit = () => {
  editForm.value = {
    gender: props.userInfo.gender || '未知',
    mood: props.userInfo.mood || '',
    bio: props.userInfo.bio || '',
    birthday: props.userInfo.birthday ? props.userInfo.birthday.split('T')[0] : '',
    address: props.userInfo.address || '',
    phoneNumber: props.userInfo.phoneNumber || '', // 补全初始值
    extraConfig: props.userInfo.extraConfig || '',   // 补全初始值
    links: parsedLinks.value.length > 0 ? [...parsedLinks.value] : [{ platform: '', url: '' }]
  }
  isEditing.value = true
}

// 动态添加社交链接行
const addLinkRow = () => {
  editForm.value.links.push({ platform: '', url: '' })
}

// 移除社交链接行
const removeLinkRow = (index: number) => {
  editForm.value.links.splice(index, 1)
}

// 提交保存
const handleSave = async () => {
  loading.value = true
  try {
    // 过滤掉空的链接
    const validLinks = editForm.value.links.filter(l => l.platform && l.url)
    
    const payload = {
      gender: editForm.value.gender,
      mood: editForm.value.mood,
      bio: editForm.value.bio,
      address: editForm.value.address,
      birthday: editForm.value.birthday,
      phoneNumber: editForm.value.phoneNumber,      // 提交新字段
      extraConfig: editForm.value.extraConfig,      // 提交新字段
      socialLinks: JSON.stringify(validLinks) 
    }

    await request.patch('/User/update-profile', payload)
    
    isEditing.value = false
    emit('updateSuccess') 
    alert('太初档案同步成功！')
  } catch (error: any) {
    console.error(error)
    alert(error.response?.data?.message || '更新失败，请检查网络')
  } finally {
    loading.value = false
  }
}

const formatDate = (dateStr?: string) => {
  if (!dateStr) return '--'
  return new Date(dateStr).toLocaleDateString()
}
</script>

<template>
  <div class="profile-card">
    <template v-if="!isEditing">
      <div class="mood-header" v-if="userInfo.mood">
        <span class="label"># 当前心情</span>
        <p class="mood-val">“{{ userInfo.mood }}”</p>
      </div>

      <h3>个人档案</h3>
      <ul class="info-list">
        <li><span>性别</span> <strong>{{ userInfo.gender || '未知' }}</strong></li>
        <li><span>年龄</span> <strong>{{ userInfo.age || '--' }} <small>岁</small></strong></li>
        <li><span>生日</span> <strong>{{ formatDate(userInfo.birthday) }}</strong></li>
        <li><span>星座</span> <strong>{{ userInfo.zodiac || '未知' }}</strong></li>
        <li><span>生肖</span> <strong>{{ userInfo.chineseZodiac || '未知' }}</strong></li>
        <li><span>常驻地</span> <strong>{{ userInfo.address || '未知' }}</strong></li>
        <li v-if="userInfo.phoneNumber"><span>联系方式</span> <strong>{{ userInfo.phoneNumber }}</strong></li>
        <li><span>加入时间</span> <strong>{{ formatDate(userInfo.createdAt) }}</strong></li>
      </ul>

      <div class="bio-box" v-if="userInfo.bio">
        <span class="label">个人介绍</span>
        <p>{{ userInfo.bio }}</p>
      </div>

      <div class="bio-box" v-if="userInfo.extraConfig">
        <span class="label">额外配置信息</span>
        <pre class="config-text">{{ userInfo.extraConfig }}</pre>
      </div>

      <div class="social-box" v-if="parsedLinks.length > 0">
        <span class="label">社交阵列</span>
        <div class="links-grid">
          <a v-for="link in parsedLinks" :key="link.platform" :href="link.url" target="_blank" class="tag">
            {{ link.platform }}
          </a>
        </div>
      </div>

      <div class="action-group">
        <button class="edit-btn" @click="startEdit">编辑资料</button>
        <button class="outline-btn danger" @click="emit('logout')">退出登录</button>
      </div>
    </template>

    <template v-else>
      <div class="edit-container">
        <h3>修改资料</h3>
        
        <div class="input-group">
          <label>性别</label>
          <input v-model="editForm.gender" placeholder="例如：男 / 女 / 隐藏" />
        </div>

        <div class="input-group">
          <label>心情</label>
          <input v-model="editForm.mood" placeholder="现在的感悟..." />
        </div>

        <div class="input-row">
          <div class="input-group flex-1">
            <label>常驻地</label>
            <input v-model="editForm.address" placeholder="位面坐标" />
          </div>
          <div class="input-group flex-1">
              <label>生日</label>
              <input type="date" v-model="editForm.birthday" />
          </div>
        </div>

        <div class="input-group">
          <label>联系电话</label>
          <input v-model="editForm.phoneNumber" placeholder="输入联系方式" />
        </div>

        <div class="input-group">
          <label>个人介绍</label>
          <textarea v-model="editForm.bio" rows="3" placeholder="简单的自我介绍..."></textarea>
        </div>

        <div class="input-group">
          <label>额外配置 (Extra Config)</label>
          <textarea v-model="editForm.extraConfig" rows="2" placeholder="输入 JSON 或其他备注配置..."></textarea>
        </div>

        <div class="input-group">
          <label>社交链接 (JSON 阵列)</label>
          <div v-for="(link, index) in editForm.links" :key="index" class="link-edit-row">
            <input v-model="link.platform" placeholder="平台(如B站)" class="short" />
            <input v-model="link.url" placeholder="URL地址" class="long" />
            <button @click="removeLinkRow(index)" class="del-row">×</button>
          </div>
          <button @click="addLinkRow" class="add-row-btn">+ 添加更多链接</button>
        </div>

        <div class="btn-footer">
          <button class="save-btn" @click="handleSave" :disabled="loading">
            {{ loading ? '同步中...' : '保存修改' }}
          </button>
          <button class="cancel-btn" @click="isEditing = false">取消</button>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.profile-card {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.02);
}

.label { font-size: 0.8rem; color: #888; display: block; margin-bottom: 5px; }

.mood-header { margin-bottom: 20px; padding: 12px; background: #f8fbff; border-radius: 8px; border-left: 4px solid #24292f; }
.mood-val { font-style: italic; color: #333; margin: 0; }

.info-list { list-style: none; padding: 0; margin-bottom: 20px; }
.info-list li { display: flex; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid #f6f8fa; font-size: 0.9rem; }

.bio-box { margin-bottom: 20px; }
.bio-box p { background: #f9f9f9; padding: 10px; border-radius: 6px; font-size: 0.9rem; line-height: 1.5; color: #555; }
.config-text { background: #272822; color: #f8f8f2; padding: 10px; border-radius: 6px; font-size: 0.8rem; overflow-x: auto; }

.social-box { margin-bottom: 25px; }
.links-grid { display: flex; gap: 8px; flex-wrap: wrap; }
.tag { padding: 4px 12px; background: #24292f; color: #fff; border-radius: 20px; font-size: 0.8rem; text-decoration: none; }

.edit-container h3 { margin-bottom: 20px; }
.input-row { display: flex; gap: 15px; }
.flex-1 { flex: 1; }
.input-group { margin-bottom: 15px; }
.input-group label { display: block; font-size: 0.85rem; margin-bottom: 6px; font-weight: 600; }
.input-group input, .input-group textarea {
  width: 100%; padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 0.9rem; transition: all 0.2s;
}
.input-group input:focus, .input-group textarea:focus { border-color: #24292f; outline: none; box-shadow: 0 0 0 3px rgba(36, 41, 47, 0.1); }

.link-edit-row { display: flex; gap: 5px; margin-bottom: 5px; }
.link-edit-row .short { width: 80px; }
.link-edit-row .long { flex: 1; }
.del-row { background: none; border: none; color: #cf222e; cursor: pointer; font-size: 1.2rem; }
.add-row-btn { background: none; border: 1px dashed #ddd; width: 100%; padding: 8px; cursor: pointer; color: #666; border-radius: 4px; font-size: 0.8rem; }

.btn-footer { display: flex; gap: 10px; margin-top: 20px; }
.save-btn { flex: 1; background: #24292f; color: #fff; border: none; padding: 10px; border-radius: 6px; cursor: pointer; font-weight: 600; }
.save-btn:disabled { background: #888; }
.cancel-btn { flex: 1; background: #eee; border: none; padding: 10px; border-radius: 6px; cursor: pointer; }

.edit-btn { width: 100%; background: #24292f; color: #fff; border: none; padding: 10px; border-radius: 6px; font-weight: 600; cursor: pointer; margin-bottom: 10px; }
.outline-btn.danger { width: 100%; background: transparent; border: 1px solid #cf222e; padding: 8px; border-radius: 6px; cursor: pointer; color: #cf222e; }
</style>