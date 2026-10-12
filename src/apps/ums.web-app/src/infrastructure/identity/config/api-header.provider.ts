export interface ApiHeaderProvider {
  getHeaders: () => Record<string, string>;
}

let headerProvider: ApiHeaderProvider | null = null;

export function setApiHeaderProvider(provider: ApiHeaderProvider): void {
  headerProvider = provider;
}

export function getApiHeaders(): Record<string, string> {
  return headerProvider?.getHeaders() ?? {};
}
