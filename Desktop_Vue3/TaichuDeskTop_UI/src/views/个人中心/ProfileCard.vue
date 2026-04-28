<script setup lang="ts">
import { ref, computed } from 'vue'
import request from '../../utils/request'
import type { UserInfo } from '../../api/auth'


// 1. 定义接口
interface SocialLink {
  platform: string
  url: string
}

const props = defineProps<{
  userInfo: UserInfo // 直接复用，不用再写一遍大括号里的内容了
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
  birthday: props.userInfo.birthday || '', // 补上这一行
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
    birthday: props.userInfo.birthday ? props.userInfo.birthday.split('T')[0] : '', // 处理后端传来的日期格式
    address: props.userInfo.address || '',
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
      socialLinks: JSON.stringify(validLinks) // 转回字符串存入数据库
    }

    await request.patch('/User/update-profile', payload)
    
    isEditing.value = false
    emit('updateSuccess') // 通知父组件刷新 Store 数据
    alert('资料更新成功！')
  } catch (error) {
    console.error(error)
    alert('更新失败，请检查网络')
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
        <li><span>加入时间</span> <strong>{{ formatDate(userInfo.createdAt) }}</strong></li>
      </ul>

      <div class="bio-box" v-if="userInfo.bio">
        <span class="label">个人介绍</span>
        <p>{{ userInfo.bio }}</p>
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

        <div class="input-group">
          <label>常驻地</label>
          <input v-model="editForm.address" placeholder="位面坐标" />
        </div>

        <div class="input-group">
            <label>生日</label>
            <input type="date" v-model="editForm.birthday" />
        </div>

        <div class="input-group">
          <label>个人介绍</label>
          <textarea v-model="editForm.bio" rows="3"></textarea>
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

/* 心情样式 */
.mood-header { margin-bottom: 20px; padding: 12px; background: #f8fbff; border-radius: 8px; border-left: 4px solid #24292f; }
.mood-val { font-style: italic; color: #333; margin: 0; }

.info-list { list-style: none; padding: 0; margin-bottom: 20px; }
.info-list li { display: flex; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid #f6f8fa; font-size: 0.9rem; }

.bio-box { margin-bottom: 20px; }
.bio-box p { background: #f9f9f9; padding: 10px; border-radius: 6px; font-size: 0.9rem; line-height: 1.5; color: #555; }

/* 社交标签样式 */
.social-box { margin-bottom: 25px; }
.links-grid { display: flex; gap: 8px; flex-wrap: wrap; }
.tag { padding: 4px 12px; background: #24292f; color: #fff; border-radius: 20px; font-size: 0.8rem; text-decoration: none; }

/* 编辑模式表单 */
.edit-container h3 { margin-bottom: 20px; }
.input-group { margin-bottom: 15px; }
.input-group label { display: block; font-size: 0.85rem; margin-bottom: 6px; font-weight: 600; }
.input-group input, .input-group textarea {
  width: 100%; padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 0.9rem;
}

.link-edit-row { display: flex; gap: 5px; margin-bottom: 5px; }
.link-edit-row .short { width: 80px; }
.link-edit-row .long { flex: 1; }
.del-row { background: none; border: none; color: #cf222e; cursor: pointer; font-size: 1.2rem; }
.add-row-btn { background: none; border: 1px dashed #ddd; width: 100%; padding: 5px; cursor: pointer; color: #666; border-radius: 4px; font-size: 0.8rem; }

.btn-footer { display: flex; gap: 10px; margin-top: 20px; }
.save-btn { flex: 1; background: #24292f; color: #fff; border: none; padding: 10px; border-radius: 6px; cursor: pointer; }
.cancel-btn { flex: 1; background: #eee; border: none; padding: 10px; border-radius: 6px; cursor: pointer; }

/* 原有按钮 */
.edit-btn { width: 100%; background: #24292f; color: #fff; border: none; padding: 10px; border-radius: 6px; font-weight: 600; cursor: pointer; margin-bottom: 10px; }
.outline-btn.danger { width: 100%; background: transparent; border: 1px solid #cf222e; padding: 8px; border-radius: 6px; cursor: pointer; color: #cf222e; }
</style>