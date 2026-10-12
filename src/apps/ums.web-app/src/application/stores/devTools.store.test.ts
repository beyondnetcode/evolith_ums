import { describe, it, expect, beforeEach } from 'vitest';
import { useDevToolsStore } from './devTools.store';

describe('devTools.store', () => {
  beforeEach(() => {
    useDevToolsStore.setState({
      devUserId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      devLanguage: 'es',
    });
  });

  it('initializes with default state', () => {
    const state = useDevToolsStore.getState();

    expect(state.devUserId).toBe('3fa85f64-5717-4562-b3fc-2c963f66afa6');
    expect(state.devLanguage).toBe('es');
  });

  it('sets dev user id', () => {
    useDevToolsStore.getState().setDevUserId('new-user-id');

    const state = useDevToolsStore.getState();
    expect(state.devUserId).toBe('new-user-id');
  });

  it('sets dev language', () => {
    useDevToolsStore.getState().setDevLanguage('en');

    const state = useDevToolsStore.getState();
    expect(state.devLanguage).toBe('en');
  });

  it('has setDevUserId function', () => {
    const state = useDevToolsStore.getState();
    expect(typeof state.setDevUserId).toBe('function');
  });

  it('has setDevLanguage function', () => {
    const state = useDevToolsStore.getState();
    expect(typeof state.setDevLanguage).toBe('function');
  });
});
