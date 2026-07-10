import { postMessage, getFileList, createNote, createFolder } from './api.js';
import { initTree, renderTree, expandAll, refreshTree, loadFileContent } from './tree.js';
import { processContentAndLinks, renderBacklinks } from './links.js';
import { initEditor, displayFileContent, onSaveComplete, setMdFileList, onImageInserted } from './editor.js';
// 初始化各模块
initTree('fileTree');
initEditor('fileName', 'fileContent', 'editStatus', 'btnEditMode', 'btnSaveContent', 'btnCancelEdit');

// 监听来自 C# 的消息
window.chrome.webview.addEventListener('message', function(e) {
    const data = e.data;
    console.log('收到 C# 消息:', data);
    switch (data.cmd) {
        case 'FILE_LIST':
            renderTree(data.files);
            // 更新自动补全数据源
            setMdFileList(data.files);
            setTimeout(expandAll, 50);
            document.getElementById('statusBar').textContent = `共 ${(data.files || []).length} 个文件`;
            break;
        case 'FILE_CONTENT':
            displayFileContent(data.content, data.path);
            processContentAndLinks(data.content, data.path);
            document.getElementById('statusBar').textContent = `已加载: ${data.path}`;
            break;
        case 'BACKLINKS':
            renderBacklinks(data.links);
            break;
        case 'FILE_SAVED':
            onSaveComplete();
            document.getElementById('statusBar').textContent = `已保存: ${data.path}`;
            getFileList();
            break;
        case 'NOTE_CREATED':
            document.getElementById('statusBar').textContent = '笔记已创建，刷新列表...';
            getFileList();
            break;
        case 'FILE_CHANGED':
            getFileList();
            break;
        case 'IMAGE_INSERTED':
            onImageInserted(data.path, data.insertedText);
            document.getElementById('statusBar').textContent = `图片已插入: ${data.path}`;
            getFileList(); // 刷新文件列表以显示新图片
            break;    
        case 'ERROR':
            alert('错误: ' + data.message);
            document.getElementById('statusBar').textContent = '错误';
            break;
        default:
            console.log('未知命令:', data.cmd);
    }
});

// 处理 content-updated 事件（由 links 模块发出）
document.addEventListener('content-updated', function(e) {
    const { html, path } = e.detail;
    const contentDiv = document.getElementById('fileContent');
    contentDiv.innerHTML = html;
    // 绑定 wikilink 点击事件
    contentDiv.querySelectorAll('.wikilink').forEach(el => {
        el.addEventListener('click', function() {
            const linkPath = this.dataset.path;
            postMessage('OPEN_LINK', { path: linkPath });
        });
    });
});

// 绑定按钮事件
document.getElementById('btnNewNote').addEventListener('click', function() {
    const input = prompt('请输入笔记标题和路径（用 | 分隔，如 "我的笔记|10_Notes/子文件夹"）\n只输入标题则保存在根目录：');
    if (!input || !input.trim()) return;
    const parts = input.split('|').map(s => s.trim());
    const title = parts[0];
    const path = parts.length > 1 ? parts[1] : '';
    if (title) {
        createNote(title, path);
    }
});

document.getElementById('btnNewFolder').addEventListener('click', function() {
    const folderPath = prompt('请输入文件夹相对路径（例如 "10_Notes/子文件夹"）：');
    if (folderPath && folderPath.trim()) {
        createFolder(folderPath.trim());
        setTimeout(() => getFileList(), 100);
    }
});

document.getElementById('btnRefresh').addEventListener('click', function() {
    refreshTree();
    document.getElementById('statusBar').textContent = '正在刷新...';
});

document.getElementById('btnExpandAll').addEventListener('click', expandAll);

// 初始化加载文件列表
getFileList();
document.getElementById('statusBar').textContent = '正在加载文件列表...';