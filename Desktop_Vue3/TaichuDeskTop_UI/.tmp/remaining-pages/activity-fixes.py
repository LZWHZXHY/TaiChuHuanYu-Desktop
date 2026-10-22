from pathlib import Path
import re
root=Path('src/views/活动中心/打卡中心')
p=root/'ActivityDetail.vue';s=p.read_text(encoding='utf-8-sig')
s=s.replace('v-if="!loading"','v-if="!loading && !loadError"',1)
s=s.replace('  <!-- 加载状态 -->\n  <div v-else', '  <div v-else-if="loadError" class="empty-state" role="alert">{{ loadError }} <button class="btn-primary" @click="loadData(activityId)">重新加载</button><RouterLink to="/activity/checkin">返回活动列表</RouterLink></div>\n  <!-- 加载状态 -->\n  <div v-else')
s=s.replace('const loading = ref(true);', "const loading = ref(true);\nconst loadError = ref('');\nconst checkingIn = ref(false);\nconst activityDays = ref(30);")
s=s.replace('    isOwner.value = data.owner', '    activityDays.value = data.days || 30;\n    isOwner.value = data.owner')
s=s.replace("    console.error('获取活动详情失败:', error);", "    throw error;")
start=s.index('// 保留原有规则');end=s.index('const fetchMembers',start)
s=s[:start]+'''// 按活动实际周期展示日历；不制造占位成员或虚构打卡记录。
const fillRecords = (records: RecordItem[] = []) => {
  const existing = new Map(records.map(record => [record.day, record]));
  return Array.from({ length: activityDays.value }, (_, index) => existing.get(index + 1) || {
    day: index + 1, isCompleted: false, isLate: false, text: '', image: ''
  });
};

'''+s[end:]
s=s.replace("      setPlaceholderMember('无成员');\n      console.warn('后端返回成员为空，已创建默认占位成员');", "      members.value = []; activeMember.value = {} as Member;")
s=s.replace("    setPlaceholderMember('加载失败');", "    throw error;")
s=s.replace('  loading.value = true;\n  try {\n    await Promise.all([\n      fetchActivityDetail(id),\n      refreshParticipation(id)\n    ]);', "  loading.value = true; loadError.value = '';\n  try {\n    await fetchActivityDetail(id);\n    await refreshParticipation(id);")
s=s.replace("    console.error('加载数据失败:', error);", "    loadError.value = '活动详情读取失败，请重试。';")
s=s.replace('days: stats.value.totalDays || 30','days: activityDays.value')
s=s.replace('    :day="modalDay"','    :day="modalDay"\n      :submitting="checkingIn"')
s=s.replace('  const { day, text, image } = payload;', '  if (checkingIn.value) return;\n  const { day, text, image } = payload;')
s=s.replace('  try {\n    await request.post(`/activities/${id}/checkin`', '  checkingIn.value = true;\n  try {\n    await request.post(`/activities/${id}/checkin`')
s=s.replace("    alert('打卡成功！');", "    showModal.value = false;\n    alert('打卡成功！');")
s=s.replace("const msg = error.response?.data?.message || error.message || '打卡失败，请重试';\n    alert(msg);\n  }", "const msg = error.response?.data?.message || error.message || '打卡失败，请重试';\n    alert(msg);\n  } finally { checkingIn.value = false; }")
s=s.replace(' · {{ log.time }}','').replace(",\n      time: `${Math.floor(Math.random() * 3) + 1}天前`",'')
s=s.replace('<button class="btn-outline"><i class="fas fa-share-alt"></i></button>','<RouterLink class="btn-outline" to="/activity/checkin">返回列表</RouterLink>')
# Native button semantics make the calendar and type selectors usable from a keyboard.
s=s.replace('<div v-for="cell in activeMember.records"','<button type="button" v-for="cell in activeMember.records"').replace('            {{ cell.day }}\n          </div>', '            {{ cell.day }}\n          </button>')
s=re.sub(r'<span\s+(v-for="type in typeOptions"[\s\S]*?)</span>',r'<button type="button" \1</button>',s)
p.write_text(s,encoding='utf-8')

p=root/'CheckInModal.vue';s=p.read_text(encoding='utf-8-sig')
s=s.replace('  day: number;','  day: number;\n  submitting?: boolean;',1).replace('ref, watch','ref, watch, onUnmounted')
s=s.replace('const text = ref(\'\');', "const text = ref('');\nconst fileInput = ref<HTMLInputElement | null>(null);\nconst clearPreview = () => { if (imagePreview.value.startsWith('blob:')) URL.revokeObjectURL(imagePreview.value); imagePreview.value = ''; };\nonUnmounted(clearPreview);")
s=s.replace('<input type="file"','<input ref="fileInput" type="file"').replace(':disabled="isUploading"', ':disabled="isUploading || submitting"')
s=s.replace(':disabled="!text.trim() || isUploading"', ':disabled="!text.trim() || isUploading || submitting"').replace(' 提交打卡',' {{ submitting ? \'保存中…\' : \'提交打卡\' }}',1)
s=s.replace('    imagePreview.value = URL.createObjectURL(file);','    clearPreview();\n    imagePreview.value = URL.createObjectURL(file);')
s=s.replace("      imagePreview.value = '';", '      clearPreview();').replace("  imagePreview.value = '';", '  clearPreview();')
s=re.sub(r"const (?:inputEl|input) = document.querySelector\('input\[type=\"file\"\]'\) as HTMLInputElement;\n\s*if \((?:inputEl|input)\) (?:inputEl|input).value = '';", "if (fileInput.value) fileInput.value.value = '';",s)
s=s.replace("const close = () => {\n  emit('update:visible', false);", "const close = () => {\n  if (props.submitting || isUploading.value) return;\n  emit('update:visible', false);\n};\nwatch(() => props.visible, (visible) => {\n  if (visible) return;")
s=s.replace("  if (fileInput.value) fileInput.value.value = '';\n};\n\nconst submit", "  if (fileInput.value) fileInput.value.value = '';\n});\n\nconst submit")
s=s.replace("const submit = () => {", "const submit = () => {\n  if (props.submitting) return;").replace('  close();\n};','};')
s=s.replace('<div class="modal-card">','<div class="modal-card" role="dialog" aria-modal="true" aria-label="填写打卡记录">').replace('class="close-btn"','aria-label="关闭打卡" class="close-btn"')
p.write_text(s,encoding='utf-8')

p=root/'CreateActivity.vue';s=p.read_text(encoding='utf-8-sig')
s=s.replace('ref, onMounted, watch','ref, onMounted, onUnmounted, computed')
s=s.replace('const { uploadFile, isUploading, progress } = useCos();','const { uploadFile, isUploading: uploading, progress: uploadProgress } = useCos();')
s=s.replace('const uploading = ref(false);\nconst uploadProgress = ref(0);','')
s=s.replace("const coverPreview = ref(''); // 用于本地预览", "const coverPreview = computed(() => form.value.cover);")
s=re.sub(r'// 监听上传进度[\s\S]*?// 从后端', '// 从后端',s)
s=s.replace('              @input="onUrlInput"','')
s=s.replace('  // 立即显示预览\n  coverPreview.value = URL.createObjectURL(file);\n  uploading.value = true;','')
s=s.replace("    coverPreview.value = '';\n",'').replace("  coverPreview.value = '';\n",'').replace('    uploading.value = false;\n','')
start=s.index('// 当用户手动输入 URL');end=s.index('// 提交表单',start);s=s[:start]+s[end:]
s=s.replace("    // 降级默认\n    typeOptions.value = [\n      { id: 1, name: '技术精进' },\n      { id: 2, name: '健康生活' },\n      { id: 3, name: '阅读写作' },\n      { id: 4, name: '运动健身' },\n      { id: 5, name: '其他' },\n    ];\n    form.value.typeId = typeOptions.value[0].id;", "    typeOptions.value = []; form.value.typeId = 0;")
s=s.replace('const submitting = ref(false);',"const submitting = ref(false);")
s=s.replace('<div v-else class="loading-types">','<button v-if="!loadingTypes && !typeOptions.length" type="button" class="btn-line" @click="fetchTypes">类型加载失败，重新加载</button>\n          <div v-if="loadingTypes" class="loading-types">')
s=s.replace('const handleSubmit = async () => {', 'const handleSubmit = async () => {\n  if (submitting.value) return;')
s=s.replace('    router.push(\'/activity\');',"    router.push('/activity/checkin');")
s=re.sub(r'<span\s+(v-for="type in typeOptions"[\s\S]*?)</span>',r'<button type="button" \1</button>',s)
s=s.replace('onMounted, onUnmounted, computed','onMounted, computed')
p.write_text(s,encoding='utf-8')
