/* eslint-disable no-console */
/**
 * logger.ts — Minimal production logger.
 *
 * In production (NODE_ENV=production): debug/info suppressed, only warn/error enabled
 * In development: all levels active with formatted output
 */
type LogLevel = 'debug' | 'info' | 'warn' | 'error';

const LEVEL_WEIGHT: Record<LogLevel, number> = {
  debug: 0,
  info: 1,
  warn: 2,
  error: 3,
};

let currentMinLevel: LogLevel = import.meta.env.DEV ? 'debug' : 'warn';

export function setLoggerConfig(config: { minLevel: LogLevel }) {
  currentMinLevel = config.minLevel;
}

function createLogger(level: LogLevel) {
  const prefix = `[UMS:${level.toUpperCase()}]`;

  return (message: string, ...args: unknown[]) => {
    if (LEVEL_WEIGHT[level] < LEVEL_WEIGHT[currentMinLevel]) {
      return;
    }
    const logFn =
      level === 'error'
        ? console.error
        : level === 'warn'
          ? console.warn
          : level === 'info'
            ? console.info
            : console.debug;
    logFn(`${new Date().toISOString()} ${prefix} ${message}`, ...args);
  };
}

export const logger = {
  debug: createLogger('debug'),
  info: createLogger('info'),
  warn: createLogger('warn'),
  error: createLogger('error'),
};
