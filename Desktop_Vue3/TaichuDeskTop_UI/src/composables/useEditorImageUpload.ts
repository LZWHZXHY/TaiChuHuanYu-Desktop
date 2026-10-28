// useEditorImageUpload.ts
import { ref } from 'vue';
import { useCos } from './useCos'

/**
 * 上传前压缩：长边压到 1920px、WebP 质量 0.85。
 * 手机原图 4MB+ 压在 400KB 以内，上传时间通常能快 5~10 倍；压缩失败则回退原图。
 */
async function compressImage(file: File, maxDim = 1920, quality = 0.85): Promise<File> {
  if (file.size < 400 * 1024) return file;
  try {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, maxDim / Math.max(bitmap.width, bitmap.height));
    const w = Math.max(1, Math.round(bitmap.width * scale));
    const h = Math.max(1, Math.round(bitmap.height * scale));
    const canvas = document.createElement('canvas');
    canvas.width = w; canvas.height = h;
    const ctx = canvas.getContext('2d');
    if (!ctx) { bitmap.close?.(); return file; }
    ctx.drawImage(bitmap, 0, 0, w, h);
    bitmap.close?.();
    const blob: Blob | null = await new Promise(r => canvas.toBlob(r, 'image/webp', quality));
    if (!blob || blob.size >= file.size) return file;
    return new File([blob], file.name.replace(/\.[^.]+$/, '') + '.webp', { type: 'image/webp' });
  } catch {
    return file;
  }
}

export function useEditorImageUpload(onChange: (editor: any) => void) {
  const pendingUploads = ref(0), uploadError = ref('');
  const {
    uploadAndRegister,
    progress: cosProgress,
    isUploading: isUploadingImage,
  } = useCos();

  const handleImageProcess = async (editor: any, view: any, file: File, pos?: number) => {
    if (!file.type.startsWith('image/')) return; // 类型拦截
    pendingUploads.value++; uploadError.value = '';
    const placeholderId = `spirit_img_loading_${crypto.randomUUID()}`;

    try {
      const { schema } = view.state;
      // 插入占位图
      const placeholderNode = schema.nodes.image.create({
        src: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="960" height="540"></svg>',
        align: 'center',
        width: '60%',
        alt: placeholderId
      });

      let tr = pos !== undefined ? view.state.tr.insert(pos, placeholderNode) : view.state.tr.replaceSelectionWith(placeholderNode);
      view.dispatch(tr);

      // 🌟 走 uploadAndRegister：上传 + 登记 assets 表
      const asset = await uploadAndRegister(await compressImage(file), 'lingmai');
      if (!editor.value || editor.value.isDestroyed) return;

      // 替换占位节点为真实图片节点（带 assetId）
      view.state.doc.descendants((node: any, nodePos: number) => {
        if (node.type.name === 'image' && node.attrs.alt === placeholderId) {
          const realImageNode = schema.nodes.image.create({
            src: asset.url,
            align: 'center',
            width: '60%',
            caption: '',
            assetId: asset.assetId,   // 🌟 塞进去
          });
          const replaceTr = view.state.tr.replaceWith(nodePos, nodePos + node.nodeSize, realImageNode);
          view.dispatch(replaceTr);
          return false;
        }
      });

      onChange(editor.value);

    } catch (err) {
      if (!editor.value || editor.value.isDestroyed) return;
      uploadError.value = file.name + ' 上传失败，请重新选择文件重试。';
      // 上传或登记失败：删掉占位图
      view.state.doc.descendants((node: any, nodePos: number) => {
        if (node.type.name === 'image' && node.attrs.alt === placeholderId) {
          const deleteTr = view.state.tr.delete(nodePos, nodePos + node.nodeSize);
          view.dispatch(deleteTr);
          return false;
        }
      });
      console.error('图片处理失败:', err);
    } finally { pendingUploads.value--; }
  };


  // ========================================================================
  // 🌟 新增：PDF 处理
  // ========================================================================
  const handlePdfProcess = async (editor: any, view: any, file: File, pos?: number) => {
    if (file.type !== 'application/pdf') return;
    pendingUploads.value++; uploadError.value = '';

    try {
      const { schema } = view.state;
      const asset = await uploadAndRegister(file, 'lingmai');
      if (!editor.value || editor.value.isDestroyed) return;

      const pdfNode = schema.nodes.pdfEmbed.create({
        assetId: asset.assetId,
        url: asset.url,
        fileName: file.name,
        height: '1000px',
      });

      const tr = pos !== undefined
        ? view.state.tr.insert(pos, pdfNode)
        : view.state.tr.replaceSelectionWith(pdfNode);
      view.dispatch(tr);

      onChange(editor.value);
    } catch (err) {
      if (!editor.value || editor.value.isDestroyed) return;
      uploadError.value = file.name + ' 上传失败，请重新选择文件重试。';
      console.error('PDF 处理失败:', err);
    } finally { pendingUploads.value--; }
  };


  return {
    pendingUploads, uploadError,
    cosProgress,
    isUploadingImage,
    handleImageProcess,
    handlePdfProcess,   // 🌟 新增
  };
}