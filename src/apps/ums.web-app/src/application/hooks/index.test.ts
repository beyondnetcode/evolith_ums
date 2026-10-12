import { describe, it, expect } from 'vitest';
import * as hooks from '@app/hooks';

describe('hooks index', () => {
  it('exports useTreeNodes', () => {
    expect(hooks.useTreeNodes).toBeDefined();
    expect(typeof hooks.useTreeNodes).toBe('function');
  });

  it('exports useEntityList', () => {
    expect(hooks.useEntityList).toBeDefined();
    expect(typeof hooks.useEntityList).toBe('function');
  });

  it('exports useStatusMapper', () => {
    expect(hooks.useStatusMapper).toBeDefined();
    expect(typeof hooks.useStatusMapper).toBe('function');
  });

  it('exports useFormValidation', () => {
    expect(hooks.useFormValidation).toBeDefined();
    expect(typeof hooks.useFormValidation).toBe('function');
  });

  it('exports useDragResize', () => {
    expect(hooks.useDragResize).toBeDefined();
    expect(typeof hooks.useDragResize).toBe('function');
  });

  it('exports useFormFields', () => {
    expect(hooks.useFormFields).toBeDefined();
    expect(typeof hooks.useFormFields).toBe('function');
  });

  it('exports useFocusTrap', () => {
    expect(hooks.useFocusTrap).toBeDefined();
    expect(typeof hooks.useFocusTrap).toBe('function');
  });

  it('exports useNotifiedMutation', () => {
    expect(hooks.useNotifiedMutation).toBeDefined();
    expect(typeof hooks.useNotifiedMutation).toBe('function');
  });

  it('exports useInlineEdit', () => {
    expect(hooks.useInlineEdit).toBeDefined();
    expect(typeof hooks.useInlineEdit).toBe('function');
  });

  it('exports useLocalOverrides', () => {
    expect(hooks.useLocalOverrides).toBeDefined();
    expect(typeof hooks.useLocalOverrides).toBe('function');
  });

  it('exports useResetOnChange', () => {
    expect(hooks.useResetOnChange).toBeDefined();
    expect(typeof hooks.useResetOnChange).toBe('function');
  });

  it('exports useStatusLabel', () => {
    expect(hooks.useStatusLabel).toBeDefined();
    expect(typeof hooks.useStatusLabel).toBe('function');
  });
});
