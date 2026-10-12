/* eslint-disable no-console */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { logger, setLoggerConfig } from './logger';

describe('logger', () => {
  const originalConsole = {
    debug: console.debug,
    info: console.info,
    warn: console.warn,
    error: console.error,
  };

  beforeEach(() => {
    console.debug = vi.fn();
    console.info = vi.fn();
    console.warn = vi.fn();
    console.error = vi.fn();
  });

  afterEach(() => {
    console.debug = originalConsole.debug;
    console.info = originalConsole.info;
    console.warn = originalConsole.warn;
    console.error = originalConsole.error;
    vi.restoreAllMocks();
  });

  describe('in production mode', () => {
    beforeEach(() => {
      setLoggerConfig({ minLevel: 'warn' });
    });

    it('suppresses debug messages', () => {
      logger.debug('test');
      expect(console.debug).not.toHaveBeenCalled();
    });

    it('suppresses info messages', () => {
      logger.info('test');
      expect(console.info).not.toHaveBeenCalled();
    });

    it('allows warn messages', () => {
      logger.warn('test');
      expect(console.warn).toHaveBeenCalled();
    });

    it('allows error messages', () => {
      logger.error('test');
      expect(console.error).toHaveBeenCalled();
    });
  });

  describe('in development mode', () => {
    beforeEach(() => {
      setLoggerConfig({ minLevel: 'debug' });
    });

    it('allows debug messages', () => {
      logger.debug('test');
      expect(console.debug).toHaveBeenCalled();
    });

    it('allows info messages', () => {
      logger.info('test');
      expect(console.info).toHaveBeenCalled();
    });
  });
});
