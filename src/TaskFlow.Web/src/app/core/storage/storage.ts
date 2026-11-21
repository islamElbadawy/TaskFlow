import { Injectable } from '@angular/core';

export abstract class AppStorage {
  abstract get(key: string): string | null;
  abstract set(key: string, value: string): void;
  abstract remove(key: string): void;
}

@Injectable()
export class LocalAppStorage extends AppStorage {
  override get(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  override set(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Ignore storage quota or privacy-mode errors.
    }
  }

  override remove(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      // Ignore storage access errors.
    }
  }
}

export class MemoryAppStorage extends AppStorage {
  private readonly store = new Map<string, string>();

  override get(key: string): string | null {
    return this.store.get(key) ?? null;
  }

  override set(key: string, value: string): void {
    this.store.set(key, value);
  }

  override remove(key: string): void {
    this.store.delete(key);
  }
}
