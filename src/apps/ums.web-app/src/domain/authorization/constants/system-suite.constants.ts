/**
 * system-suite.constants.ts — Domain constants for the SystemSuite aggregate.
 */

export const SYSTEM_SUITE_STATUSES = ['Active', 'Maintenance', 'Deprecated'] as const;
export type SystemSuiteStatusType = (typeof SYSTEM_SUITE_STATUSES)[number];

export const SYSTEM_SUITE_PAGE_SIZE = 20 as const;
