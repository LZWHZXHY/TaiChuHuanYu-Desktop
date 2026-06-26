import { useCos } from './useCos'

export function useEditorImageUpload(currentNoteID:any,updateNoteContent:Function, emit:Function){
    const { uploadFile, progress:cosProgress, isUploading:isUploadingImage} = useCos();

    const handleImageProcess = async(editor:any, view:any,file:File,pos?:number)=>{
        if(!file.type.startsWith('image/'))return; //类型拦截
        const placeholderId = `spirit_img_loading_${Date.now()}`;

        try{
            const{schema} = view.state;
            const placeholderNode = schema.nodes.image.create({                                                                                                         //生成一个占位图
                src: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg"/>',align: 'center',width: '100%',alt: placeholderId  
            });

            let tr = pos ? view.state.tr.insert(pos, placeholderNode) : view.state.tr.replaceSelectionWith(placeholderNode);
            view.dispatch(tr);                                                                                                                                          //提交事物
            const result = await uploadFile(file, 'lingmai');

            view.state.doc.descendants((node:any,nodePos:number) =>{
                if(node.type.name === 'image' && node.attrs.alt === placeholderId){
                    const realImageNode = schema.nodes.image.create({src:result.url,align:'center',width:'100%',caption:''});
                    const replaceTr = view.state.tr.replaceWith(nodePos, nodePos + node.nodeSize, realImageNode);
                    view.dispatch(replaceTr);
                    return false;
                }
            });

            const finalJson = editor.value?.getJSON();
            if (finalJson) {
                updateNoteContent(currentNoteID.value, finalJson);
                editor.value?.view.dom.dispatchEvent(new CustomEvent('change-content', {bubbles: true,detail: finalJson}));
                emit('change', finalJson);
            }

        }catch(err){
            view.state.doc.descendants((node: any, nodePos: number) => {
                if (node.type.name === 'image' && node.attrs.alt === placeholderId) {
                    const deleteTr = view.state.tr.delete(nodePos, nodePos + node.nodeSize);
                    view.dispatch(deleteTr);
                    return false; // break
                }
            });
            console.error('图片处理失败:', err);
        }
        
    }
    return {
        cosProgress,
        isUploadingImage,
        handleImageProcess
    };
    
}