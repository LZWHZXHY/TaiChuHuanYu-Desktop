import { lingmaiApi } from '@/api/lingmai';

type SyncPayload = Parameters<typeof lingmaiApi.updateNoteContent>[1];
type ShowToast = (message: string, duration?: number) => void;
type Job = { noteId: string; payload: SyncPayload; showToast?: ShowToast; delay: number };
const pending = new Map<string, Job & { timer: ReturnType<typeof setTimeout> }>();
const inFlight = new Map<string, Promise<void>>();
const paused = new Set<string>();

// 保存失败（服务器冲突、5xx、断网）时自动重试，避免内容在用户不知情时丢失。
const RETRY_DELAY = 5000;
const MAX_RETRIES = 3;

function write(noteId: string, payload: SyncPayload, silentError = false) {
  const task = (inFlight.get(noteId) || Promise.resolve()).catch(() => {}).then(async () => {
    await lingmaiApi.updateNoteContent(noteId, payload, silentError);
  }).finally(() => { if (inFlight.get(noteId) === task) inFlight.delete(noteId); });
  inFlight.set(noteId, task);
  return task;
}

function failure(error: unknown) {
  const e = error as { friendlyMessage?: string; response?: { status?: number; data?: { message?: string } } };
  const status = e?.response?.status;
  return {
    // 409 保存冲突、服务器错误和网络中断值得再试；权限、笔记不存在、额度锁定等重试也没用。
    retryable: status === undefined || status === 409 || status >= 500,
    reason: e?.response?.data?.message || e?.friendlyMessage || '网络或服务器异常',
  };
}

function schedule(key: string, job: Job, wait: number, attempt = 0) {
  clearTimeout(pending.get(key)?.timer);
  const timer = setTimeout(async () => {
    if (paused.has(job.noteId)) return;
    pending.delete(key);
    try {
      await write(job.noteId, job.payload, true);
      job.showToast?.(attempt ? '☁️ 重试成功，已同步' : '☁️ 已自动同步', 1500);
    } catch (e) {
      console.error('同步失败', e);
      // 等待期间已有更新的修改排队，它会带上最新内容，不必重试这份旧内容。
      if (pending.has(key)) return;
      const { retryable, reason } = failure(e);
      if (retryable && attempt < MAX_RETRIES) {
        job.showToast?.(`⚠️ 保存没成功：${reason}，${RETRY_DELAY / 1000} 秒后自动重试`, 3000);
        schedule(key, job, RETRY_DELAY, attempt + 1);
      } else {
        job.showToast?.(`❌ 保存失败：${reason}。刷新前请先复制重要内容`, 5000);
      }
    }
  }, wait);
  pending.set(key, { ...job, timer });
}

function syncToCloud(noteId: string, payload: SyncPayload, showToast?: ShowToast, delay = 2000) {
  // 保留正文 1 秒、块 2 秒的现有节奏，并统一跟踪正在执行的写入。
  const key = noteId + (payload.rawContent ? ':content' : ':blocks');
  schedule(key, { noteId, payload, showToast, delay }, delay);
}

function resumeNote(noteId: string, discard = false) {
  paused.delete(noteId);
  for (const [key, job] of [...pending]) {
    if (job.noteId !== noteId) continue;
    clearTimeout(job.timer);
    pending.delete(key);
    if (!discard) schedule(key, job, job.delay);
  }
}

async function pauseNote(noteId: string) {
  paused.add(noteId);
  for (const job of pending.values()) if (job.noteId === noteId) clearTimeout(job.timer);
  await inFlight.get(noteId)?.catch(() => {});
}

function saveNow(noteId: string, payload: SyncPayload) {
  resumeNote(noteId, true);
  return write(noteId, payload);
}

export function useAutoSave() {
  return { syncToCloud, saveNow, pauseNote, resumeNote };
}
