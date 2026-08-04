<template>
  <form class="character-form" @submit.prevent="handleSubmit">
    <!-- 基本信息 -->
    <div class="form-section">
      <h3 class="section-label">基本信息</h3>

      <div class="form-row">
        <div class="form-group">
          <label>角色名称 <span class="required">*</span></label>
          <input v-model="form.name" placeholder="给角色取名" required />
        </div>
        <div class="form-group">
          <label>绰号/别名</label>
          <input v-model="form.nickname" placeholder="其他称呼" />
        </div>
      </div>

      <div class="form-row">
        <div class="form-group">
          <label>性别</label>
          <select v-model="form.gender">
            <option value="男">男</option>
            <option value="女">女</option>
            <option value="未知">未知</option>
            <option value="其他">其他</option>
          </select>
        </div>
        <div class="form-group">
          <label>年龄</label>
          <input v-model.number="form.age" type="number" placeholder="0" min="0" />
        </div>
      </div>

      <div class="form-row">
        <div class="form-group">
          <label>身高</label>
          <input v-model="form.height" placeholder="如：180cm" />
        </div>
        <div class="form-group">
          <label>体重</label>
          <input v-model="form.weight" placeholder="如：70kg" />
        </div>
      </div>
    </div>

    <!-- 外观与性格 -->
    <div class="form-section">
      <h3 class="section-label">外观与性格</h3>

      <div class="form-group">
        <label>外貌描述 <span class="required">*</span></label>
        <textarea v-model="form.appearance" rows="3" placeholder="描述角色的外貌特征..." required></textarea>
      </div>

      <div class="form-group">
        <label>服装</label>
        <textarea v-model="form.outfit" rows="2" placeholder="角色的穿着打扮..."></textarea>
      </div>

      <div class="form-group">
        <label>性格特征 <span class="required">*</span></label>
        <textarea v-model="form.personality" rows="3" placeholder="角色的性格特点..." required></textarea>
      </div>

      <div class="form-group">
        <label>背景故事 <span class="required">*</span></label>
        <textarea v-model="form.background" rows="4" placeholder="角色的身世背景..." required></textarea>
      </div>

      <div class="form-group">
        <label>能力/技能</label>
        <textarea v-model="form.abilities" rows="2" placeholder="特殊能力或技能..."></textarea>
      </div>
    </div>

    <!-- 标签与图集 -->
    <div class="form-section">
      <h3 class="section-label">标签与图集</h3>

      <div class="form-group">
        <label>标签</label>
        <div class="tag-input">
          <input
            v-model="tagInput"
            placeholder="输入标签，按回车添加"
            @keydown.enter.prevent="addTag"
          />
          <button type="button" class="add-btn" @click="addTag">+</button>
        </div>
        <div class="tag-list">
          <span v-for="tag in form.tags" :key="tag" class="tag-item">
            #{{ tag }}
            <button type="button" class="remove-tag" @click="removeTag(tag)">×</button>
          </span>
        </div>
      </div>

      <div class="form-group">
        <label>头像图 URL</label>
        <input v-model="form.avatar" placeholder="输入图片链接" />
      </div>

      <div class="form-group">
        <label>图集</label>
        <div class="tag-input">
          <input
            v-model="galleryInput"
            placeholder="输入图片链接"
            @keydown.enter.prevent="addGallery"
          />
          <button type="button" class="add-btn" @click="addGallery">+</button>
        </div>
        <div class="gallery-preview">
          <div v-for="(img, idx) in form.gallery" :key="idx" class="gallery-item">
            <img :src="img" alt="图集" />
            <button type="button" class="remove-gallery" @click="removeGallery(idx)">×</button>
          </div>
        </div>
      </div>
    </div>

    <!-- 发布设置 -->
    <div class="form-section">
      <h3 class="section-label">发布设置</h3>

      <div class="form-group">
        <label>状态</label>
        <select v-model="form.status">
          <option value="draft">存为草稿</option>
          <option value="published">直接发布</option>
        </select>
      </div>
    </div>

    <!-- 按钮 -->
    <div class="form-actions">
      <button type="button" class="btn-line" @click="router.back()">取消</button>
      <button type="submit" class="btn-line btn-submit" :disabled="loading">
        {{ loading ? '保存中...' : '保存角色' }}
      </button>
    </div>
  </form>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import type { StickmanCharacter, CreateStickmanDto } from '../stickman'

const props = defineProps<{
  initialData?: StickmanCharacter
}>()

const emit = defineEmits<{
  submit: [data: CreateStickmanDto]
}>()

const router = useRouter()
const loading = ref(false)
const tagInput = ref('')
const galleryInput = ref('')

const form = reactive<CreateStickmanDto & { id?: string }>({
  name: '',
  nickname: '',
  gender: '未知',
  age: undefined,
  height: '',
  weight: '',
  appearance: '',
  outfit: '',
  personality: '',
  background: '',
  abilities: '',
  tags: [],
  avatar: '',
  gallery: [],
  status: 'published',
})

onMounted(() => {
  if (props.initialData) {
    Object.assign(form, {
      id: props.initialData.id,
      name: props.initialData.name,
      nickname: props.initialData.nickname || '',
      gender: props.initialData.gender,
      age: props.initialData.age,
      height: props.initialData.height || '',
      weight: props.initialData.weight || '',
      appearance: props.initialData.appearance,
      outfit: props.initialData.outfit || '',
      personality: props.initialData.personality,
      background: props.initialData.background,
      abilities: props.initialData.abilities || '',
      tags: [...props.initialData.tags],
      avatar: props.initialData.avatar || '',
      gallery: [...props.initialData.gallery],
      status: props.initialData.status === 'draft' ? 'draft' : 'published',
    })
  }
})

function addTag() {
  const val = tagInput.value.trim()
  if (val && !form.tags.includes(val)) {
    form.tags.push(val)
    tagInput.value = ''
  }
}

function removeTag(tag: string) {
  form.tags = form.tags.filter(t => t !== tag)
}

function addGallery() {
  const val = galleryInput.value.trim()
  if (val && !form.gallery.includes(val)) {
    form.gallery.push(val)
    galleryInput.value = ''
  }
}

function removeGallery(idx: number) {
  form.gallery.splice(idx, 1)
}

function handleSubmit() {
  if (!form.name.trim()) return alert('请输入角色名称')
  if (!form.appearance.trim()) return alert('请输入外貌描述')
  if (!form.personality.trim()) return alert('请输入性格特征')
  if (!form.background.trim()) return alert('请输入背景故事')

  emit('submit', {
    ...form,
    nickname: form.nickname || undefined,
    height: form.height || undefined,
    weight: form.weight || undefined,
    outfit: form.outfit || undefined,
    abilities: form.abilities || undefined,
    avatar: form.avatar || undefined,
  })
}
</script>

<style scoped>
.character-form {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.form-section {
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 24px;
}

.form-section:last-of-type {
  border-bottom: none;
  padding-bottom: 0;
}

.section-label {
  font-size: 16px;
  font-weight: 400;
  letter-spacing: 0.2em;
  margin: 0 0 18px 0;
  color: var(--ink-black);
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.form-group {
  margin-bottom: 14px;
}

.form-group label {
  display: block;
  font-size: 13px;
  letter-spacing: 0.15em;
  margin-bottom: 4px;
  color: var(--ink-black);
}

.required {
  color: var(--cinnabar);
}

.form-group input,
.form-group select,
.form-group textarea {
  width: 100%;
  padding: 8px 14px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  color: var(--ink-black);
  font-family: var(--font-family);
  font-size: 14px;
  letter-spacing: 0.08em;
  transition: border-color 0.3s;
  outline: none;
}

.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
  border-color: var(--ink-black);
}

.form-group textarea {
  resize: vertical;
  min-height: 60px;
}

.tag-input {
  display: flex;
  gap: 8px;
}

.tag-input input {
  flex: 1;
}

.add-btn {
  padding: 8px 16px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  color: var(--ink-black);
  font-size: 18px;
  cursor: pointer;
  transition: border-color 0.3s;
}

.add-btn:hover {
  border-color: var(--ink-black);
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 8px;
}

.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 10px 2px 14px;
  border: 1px solid var(--line-raw);
  font-size: 12px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
}

.remove-tag {
  background: none;
  border: none;
  color: var(--ink-light);
  cursor: pointer;
  font-size: 16px;
  padding: 0 2px;
}

.remove-tag:hover {
  color: var(--cinnabar);
}

.gallery-preview {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 8px;
}

.gallery-item {
  position: relative;
  width: 72px;
  height: 72px;
  border: 1px solid var(--line-raw);
  overflow: hidden;
}

.gallery-item img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.remove-gallery {
  position: absolute;
  top: 4px;
  right: 4px;
  width: 20px;
  height: 20px;
  border: none;
  background: rgba(44, 42, 41, 0.75);
  color: #fff;
  cursor: pointer;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.remove-gallery:hover {
  background: var(--cinnabar);
}

.form-actions {
  display: flex;
  gap: 12px;
  padding-top: 20px;
  border-top: 1px solid var(--line-raw);
}

.btn-submit {
  flex: 1;
  border-color: var(--ink-black);
  background: var(--ink-black);
  color: var(--paper-card);
}

.btn-submit:hover {
  background: var(--paper-card);
  color: var(--ink-black);
}

.btn-submit:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

@media (max-width: 600px) {
  .form-row {
    grid-template-columns: 1fr;
  }

  .form-actions {
    flex-direction: column-reverse;
  }
}
</style>