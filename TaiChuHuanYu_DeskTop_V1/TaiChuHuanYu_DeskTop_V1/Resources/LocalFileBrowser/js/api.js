// 与 C# 通信的封装
export function postMessage(cmd, data) {
    const msg = { cmd, ...data };
    window.chrome.webview.postMessage(msg);
}

// 常用请求
export function getFileList() {
    postMessage('GET_FILE_LIST', {});
}
export function getFileContent(path) {
    postMessage('GET_FILE_CONTENT', { path });
}
export function saveFileContent(path, content) {
    postMessage('SAVE_FILE_CONTENT', { path, content });
}
export function createNote(title, path) {
    postMessage('CREATE_NOTE', { title, path });
}
export function createFolder(path) {
    postMessage('CREATE_FOLDER', { path });
}
export function openLink(path) {
    postMessage('OPEN_LINK', { path });
}
export function getBacklinks(path) {
    postMessage('GET_BACKLINKS', { path });
}
export function insertImage(currentDocPath, fileName, base64Data) {
    postMessage('INSERT_IMAGE', { currentDocPath, fileName, base64Data });
}