import { TestBed } from '@angular/core/testing';
import { AR } from '../../../i18n/ar';
import { EN } from '../../../i18n/en';
import { TranslationService } from './translation.service';
import { AppStorage, MemoryAppStorage } from '../storage/storage';
import { ThemeStore } from '../theme/theme.store';

describe('i18n and theme', () => {
  let storage: MemoryAppStorage;

  beforeEach(() => {
    storage = new MemoryAppStorage();
    TestBed.configureTestingModule({
      providers: [{ provide: AppStorage, useValue: storage }],
    });
  });

  it('has identical keys in English and Arabic dictionaries', () => {
    expect(Object.keys(AR).sort()).toEqual(Object.keys(EN).sort());
  });

  it('formats Arabic plural forms for 0, 1, 2, 3, 11, and 100', () => {
    expect(AR.commentsCount(0)).toBe('لا تعليقات بعد');
    expect(AR.commentsCount(1)).toBe('تعليق واحد');
    expect(AR.commentsCount(2)).toBe('تعليقان');
    expect(AR.commentsCount(3)).toContain('تعليقات');
    expect(AR.commentsCount(11)).toContain('تعليقًا');
    expect(AR.commentsCount(100)).toContain('تعليق');
  });

  it('switches language to Arabic with ar-EG digits and rtl direction', async () => {
    const i18n = TestBed.inject(TranslationService);
    await i18n.setLang('ar');
    TestBed.flushEffects();

    expect(i18n.lang()).toBe('ar');
    expect(i18n.dir()).toBe('rtl');
    expect(i18n.formatNumber(42)).toBe('٤٢');
    expect(storage.get('tf-lang')).toBe('ar');
  });

  it('persists theme preference', () => {
    const theme = TestBed.inject(ThemeStore);
    theme.setTheme('dark');
    TestBed.flushEffects();

    expect(theme.theme()).toBe('dark');
    expect(storage.get('tf-theme')).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');

    theme.setTheme('system');
    TestBed.flushEffects();
    expect(document.documentElement.dataset['theme']).toBeUndefined();
  });
});
