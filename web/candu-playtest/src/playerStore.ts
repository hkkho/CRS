import type { RunSave } from './runSave';

export interface StoredRun { key: string; owner: string; run: RunSave }

/** IndexedDB keeps large replay logs off the synchronous rendering path. */
export class PlayerStore {
  private database?: Promise<IDBDatabase>;
  private open(): Promise<IDBDatabase> {
    return this.database ??= new Promise((resolve, reject) => {
      const request = indexedDB.open('candu-player-v1', 1);
      request.onupgradeneeded = () => request.result.createObjectStore('runs', { keyPath: 'key' });
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => { this.database = undefined; reject(new Error('Browser storage is unavailable.')); };
    });
  }
  async put(owner: string, run: RunSave): Promise<void> {
    const db = await this.open();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction('runs', 'readwrite');
      tx.objectStore('runs').put({ key: `${owner}:${run.id}`, owner, run } satisfies StoredRun);
      tx.oncomplete = () => resolve();
      tx.onerror = tx.onabort = () => reject(new Error('Could not save locally. Browser storage may be full.'));
    });
  }
  async list(owner: string): Promise<RunSave[]> {
    const db = await this.open();
    return new Promise((resolve, reject) => {
      const request = db.transaction('runs').objectStore('runs').getAll();
      request.onsuccess = () => resolve((request.result as StoredRun[]).filter(r => r.owner === owner)
        .map(r => r.run).sort((a, b) => b.savedAt.localeCompare(a.savedAt)));
      request.onerror = () => reject(new Error('Could not read local saves.'));
    });
  }
}
