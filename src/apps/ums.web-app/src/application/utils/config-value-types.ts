export type ConfigValueType = 'boolean' | 'number' | 'string' | 'json';

export function inferConfigValueType(value: string): ConfigValueType {
  if (value === 'true' || value === 'false') {
    return 'boolean';
  }

  if (!isNaN(Number(value)) && value.trim() !== '') {
    return 'number';
  }

  const trimmed = value.trim();
  if (
    (trimmed.startsWith('[') && trimmed.endsWith(']')) ||
    (trimmed.startsWith('{') && trimmed.endsWith('}'))
  ) {
    return 'json';
  }

  return 'string';
}

export function formatBooleanValue(value: string): boolean {
  return value === 'true';
}

export function parseValueByType(value: string, type: ConfigValueType): string {
  if (type === 'boolean') {
    return value === 'true' ? 'true' : 'false';
  }
  if (type === 'number') {
    return value;
  }
  return value;
}

export function parseJsonValue<T>(value: string): T | null {
  try {
    return JSON.parse(value) as T;
  } catch {
    return null;
  }
}

export function stringifyJsonValue<T>(value: T): string {
  return JSON.stringify(value);
}
