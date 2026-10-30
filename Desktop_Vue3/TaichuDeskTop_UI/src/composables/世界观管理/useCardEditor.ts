// src/composables/世界观管理/useCardEditor.ts
import { ref, computed, unref, onScopeDispose, watch, type Ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useUserStore } from '@/stores/user'
import { useCardDraft } from './useCardDraft'
import { useWorldStore } from '@/stores/world'
import type { CreateCardPayload, UpdateCardPayload, CardSummary } from '@/api/types/world'
import { cardToForm, mergeCardForms, type CardForm } from './useCardForm'
import { serializeLayout } from '@/utils/worldLayout'

export function useCardEditor(
  form: Ref<CardForm>,
  resetForm: () => void,
  setFormData: (data: Partial<CardForm>) => void,
  checkQuota: () => Promise<boolean>,
  projectId: Ref<string> | string = ''
) {
  const route = useRoute()
  const store = useWorldStore()

  const draft = useCardDraft(form)
  watch(() => useUserStore().token, () => { loadVersion++; draft.suspend(); resetConflict(); resetForm(); loadError.value = true }, { flush: 'sync' })

  const saving = ref(false)
  const loading = ref(false)
  const loadError = ref(false)
  let loadVersion = 0
  const isCreating = ref(true)
  const conflicts = ref<(keyof CardForm)[]>([])
  const conflictChoices = ref<Partial<Record<keyof CardForm, 'local' | 'server'>>>({})
  const remoteForm = ref<CardForm | null>(null)
  let remoteVersion = ''
  /** 读进编辑器时服务器上的版本：没人同时改的话直接带着它保存，不用先把整个条目再读一遍 */
  let knownVersion = ''
  let mergeResult: CardForm | null = null
  function resetConflict() { conflicts.value = []; conflictChoices.value = {}; remoteForm.value = null; mergeResult = null }

  const snapshot = () => JSON.stringify(form.value)
  const savedSnapshot = ref(snapshot())
  const markClean = () => { savedSnapshot.value = snapshot() }
  const isDirty = computed(() => !loading.value && !loadError.value && snapshot() !== savedSnapshot.value)

  // 仅显式传入的条目决定编辑目标，阅读页路由不能覆盖新建表单。
  const currentCardId = ref<string>('')

  // 优先使用传入的 projectId，如果没有则从路由获取
  const routeProjectId = computed(() => unref(projectId) || String(route.params.projectId || route.params.id || ''))
  const routeCardId = computed(() => currentCardId.value || undefined)

  const startCreating = () => {
    loadVersion++
    draft.suspend()
    resetConflict()
    resetForm()
    isCreating.value = true
    currentCardId.value = ''
    loading.value = false
    loadError.value = false
    markClean()
    draft.bind(routeProjectId.value, '')
  }

  // ===== 加载卡片数据 =====
  const loadCardData = async (cardData?: Pick<CardSummary, 'id'> | null) => {
    const version = ++loadVersion
    draft.suspend()
    resetConflict()
    const cardId = cardData?.id
    if (!cardId) {
      knownVersion = ''
      startCreating()
      return
    }

    // ✅ 记住当前卡片 ID
    currentCardId.value = cardId
    isCreating.value = false
    loadError.value = false

    const pid = routeProjectId.value
    if (!pid) {
      throw new Error('缺少 projectId')
    }

    loading.value = true
    try {
      // 表单使用独立副本，未保存的图库、引用和时间线不能改动阅读缓存。
      const fullCard = JSON.parse(JSON.stringify(await store.fetchCardDetail(pid, cardId)))
      if (version !== loadVersion) return

      if (!fullCard) {
        throw new Error('卡片数据为空')
      }

      setFormData(cardToForm(fullCard))
      knownVersion = fullCard.updatedAt || ''
      markClean()
      draft.bind(pid, cardId)

    } catch (error) {
      if (version !== loadVersion) return
      console.error('加载卡片数据失败:', error)
      loadError.value = true
    } finally {
      if (version === loadVersion) loading.value = false
    }
  }

  function resolveConflicts() {
    if (!remoteForm.value || !mergeResult || conflicts.value.some(key => !conflictChoices.value[key])) return
    for (const key of conflicts.value) {
      if (conflictChoices.value[key] === 'server') Object.assign(mergeResult, { [key]: remoteForm.value[key] })
    }
    const baseline = JSON.stringify(remoteForm.value)
    form.value = JSON.parse(JSON.stringify(mergeResult))
    savedSnapshot.value = baseline
    draft.rebase(baseline)
    resetConflict()
  }
  async function prepareUpdate(version: number) {
    const latest = await store.fetchCardDetail(routeProjectId.value, currentCardId.value, true)
    if (version !== loadVersion) return false
    const remote = cardToForm(latest)
    let base: CardForm | null = null
    try { base = JSON.parse(draft.baseSnapshot.value || savedSnapshot.value) } catch { /* 旧草稿无有效基线时必须人工核对 */ }
    // 旧版草稿的基线没有别名和区块：当作和服务器一样，不算冲突
    if (base && !base.layout) base.layout = remote.layout
    if (base && !Array.isArray(base.aliases)) base.aliases = remote.aliases
    const result = mergeCardForms(base, form.value, remote)
    remoteVersion = latest.updatedAt
    if (result.conflicts.length) {
      remoteForm.value = remote; mergeResult = result.merged
      conflicts.value = result.conflicts; conflictChoices.value = {}
      return false
    }
    form.value = result.merged
    savedSnapshot.value = JSON.stringify(remote)
    draft.rebase(savedSnapshot.value)
    return true
  }

  // ===== 保存卡片 =====
  const handleSave = async (onSaved: (cardId: string) => void) => {
    if (saving.value || loading.value || loadError.value || draft.pending.value || conflicts.value.length) return
    if (!form.value.title.trim()) {
      ElMessage.warning('请输入标题')
      return
    }

    saving.value = true
    const version = loadVersion
    const pid = routeProjectId.value
    try {
      if (isCreating.value) {
        const canCreate = await checkQuota()
        if (version !== loadVersion) return
        if (!canCreate) {
          ElMessage.warning('当前世界卡片数量已达上限，请扩容')
          return
        }
      }

      // 保存前先核对服务器有没有别人的新修改。没恢复过旧草稿时直接带着读入时的版本号保存（省一次完整读取）；
      // 服务器发现版本对不上会拒绝（409），那时再读最新内容合并，没有冲突就自动重新保存一次。
      const fastPath = !isCreating.value && Boolean(knownVersion) && (!draft.baseSnapshot.value || draft.baseSnapshot.value === savedSnapshot.value)
      if (fastPath) remoteVersion = knownVersion
      else if (!isCreating.value && !await prepareUpdate(version)) return

      // 每次都按表单当前内容生成（合并了别人的修改后要重新生成）
      const buildPayload = (): CreateCardPayload => ({
        title: form.value.title.trim(),
        type: form.value.type,
        coverImage: form.value.coverImage,
        galleryImages: form.value.galleryImages,
        attributes: form.value.attributes.map(attribute => ({ ...attribute, value: String(attribute.value ?? '') })),
        description: form.value.description,
        content: JSON.stringify(form.value.content || {}),   // ✅ 对象 → 字符串
        tags: form.value.tags,
        contentBlocks: form.value.contentBlocks,
        timelineEvents: form.value.timelineEvents,
        relations: form.value.relations,
        aliases: form.value.aliases.map(a => a.trim()).filter(Boolean),
        layout: serializeLayout(form.value.layout),
      })

      if (isCreating.value) {
        const created = await store.createCard(pid, buildPayload())
        if (version !== loadVersion) return
        currentCardId.value = created.id
        isCreating.value = false
        ElMessage.success('已创建')
      } else {
        if (!currentCardId.value) throw new Error('缺少卡片 ID')
        // 未修改的字段不重写，避免把旧格式的内容或属性转换成表单默认值。
        const send = () => {
          const baseline: CardForm = JSON.parse(savedSnapshot.value)
          const payload = buildPayload()
          const changes: UpdateCardPayload = Object.fromEntries(Object.entries(payload)
            .filter(([key]) => JSON.stringify(form.value[key as keyof CardForm]) !== JSON.stringify(baseline[key as keyof CardForm])))
          return store.updateCard(currentCardId.value, { ...changes, expectedUpdatedAt: remoteVersion })
        }
        let saved
        try { saved = await send() }
        catch (error: any) {
          // 快速保存时撞上了别人的修改：读最新内容合并，没有冲突就再保存一次；有冲突就交给用户选
          if (!fastPath || error?.response?.status !== 409 || error?.response?.data?.code !== 'WORLD_CARD_CONFLICT') throw error
          if (!await prepareUpdate(version)) return
          saved = await send()
        }
        if (saved?.updatedAt) knownVersion = saved.updatedAt
        ElMessage.success('已更新')
      }

      if (version === loadVersion) { await draft.acceptSaved(); if (version !== loadVersion) return; markClean(); onSaved(currentCardId.value) }
    } catch (error: any) {
      console.error('保存失败:', error)
      if (version !== loadVersion) return
      if (error?.response?.status === 409 && error?.response?.data?.code === 'WORLD_CARD_CONFLICT') {
        ElMessage.warning('条目刚被其他页面更新，本地草稿已保留。再次保存时会核对最新内容。')
      } else if (error?.response?.data?.code === 'CARD_LIMIT_EXCEEDED') {
        ElMessage.warning({
          message: error.response.data.message || '卡片数量已达上限，请扩容',
          duration: 5000,
          showClose: true,
        })
      } else {
        ElMessage.error(error?.response?.data?.message || '保存失败，填写的内容已保留，请重试')
      }
    } finally {
      saving.value = false
    }
  }

  // ===== 删除卡片 =====
  const handleDelete = async (onDeleted: () => void) => {
    try {
      await ElMessageBox.confirm('确定删除吗？', '提示', { type: 'warning' })
      const cardId = currentCardId.value
      if (!cardId) {
        throw new Error('缺少卡片 ID')
      }
      await store.deleteCard(cardId)
      await draft.acceptSaved()
      markClean()
      ElMessage.success('已删除')
      onDeleted()
    } catch (error) {
      if (error !== 'cancel') console.error(error)
    }
  }

  onScopeDispose(() => { loadVersion++ })
  return {
    draft,
    conflicts, conflictChoices, remoteForm, resolveConflicts,
    isDirty,
    markClean,
    saving,
    loading,
    loadError,
    isCreating,
    routeProjectId,
    routeCardId,
    currentCardId,
    startCreating,
    loadCardData,
    handleSave,
    handleDelete,
  }
}
