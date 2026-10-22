// useEditorImageUpload.ts
import { ref } from 'vue';
import { useCos } from './useCos'

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
        src: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg"/>',
        align: 'center',
        width: '60%',
        alt: placeholderId
      });

      let tr = pos !== undefined ? view.state.tr.insert(pos, placeholderNode) : view.state.tr.replaceSelectionWith(placeholderNode);
      view.dispatch(tr);

      // 🌟 走 uploadAndRegister：上传 + 登记 assets 表
      const asset = await uploadAndRegister(file, 'lingmai');
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