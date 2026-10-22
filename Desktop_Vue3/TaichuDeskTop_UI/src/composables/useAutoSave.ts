import { lingmaiApi } from '@/api/lingmai';

type SyncPayload = Parameters<typeof lingmaiApi.updateNoteContent>[1];
type ShowToast = (message: string, duration?: number) => void;
const pending = new Map<string, { noteId: string; payload: SyncPayload; showToast?: ShowToast; delay: number; timer: ReturnType<typeof setTimeout> }>();
const inFlight = new Map<string, Promise<void>>();
const paused = new Set<string>();

function write(noteId: string, payload: SyncPayload) {
  const task = (inFlight.get(noteId) || Promise.resolve()).catch(() => {}).then(async () => {
    await lingmaiApi.updateNoteContent(noteId, payload);
  }).finally(() => { if (inFlight.get(noteId) === task) inFlight.delete(noteId); });
  inFlight.set(noteId, task);
  return task;
}

function syncToCloud(noteId: string, payload: SyncPayload, showToast?: ShowToast, delay = 2000) {
  // 保留正文 1 秒、块 2 秒的现有节奏，并统一跟踪正在执行的写入。
  const key = noteId + (payload.rawContent ? ':content' : ':blocks');
  clearTimeout(pending.get(key)?.timer);
  const timer = setTimeout(async () => {
    if (paused.has(noteId)) return;
    pending.delete(key);
    try {
      await write(noteId, payload);
      showToast?.('☁️ 已自动同步', 1500);
    } catch (e) {
      console.error('同步失败', e);
      showToast?.('❌ 同步失败', 2000);
    }
  }, delay);
  pending.set(key, { noteId, payload, showToast, delay, timer });
}

function resumeNote(noteId: string, discard = false) {
  paused.delete(noteId);
  for (const [key, job] of [...pending]) {
    if (job.noteId !== noteId) continue;
    clearTimeout(job.timer);
    pending.delete(key);
    if (!discard) syncToCloud(noteId, job.payload, job.showToast, job.delay);
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
