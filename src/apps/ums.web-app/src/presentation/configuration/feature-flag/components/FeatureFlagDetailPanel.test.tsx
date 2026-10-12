import React from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FeatureFlagDetailPanel } from './FeatureFlagDetailPanel';
import type { FeatureFlag } from '@domain/configuration/models/feature-flag.model';

vi.mock('@app/configuration/hooks/use-feature-flag', () => ({
  useActivateFlag: () => ({ mutate: vi.fn(), isPending: false }),
  useDeactivateFlag: () => ({ mutate: vi.fn(), isPending: false }),
  useArchiveFlag: () => ({ mutate: vi.fn(), isPending: false }),
  useAddFeatureFlagCriteria: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useRemoveFeatureFlagCriteria: () => ({ mutate: vi.fn(), isPending: false }),
}));

const mockFlag: FeatureFlag = {
  featureFlagId: '11111111-1111-1111-1111-111111111111',
  systemSuiteId: '22222222-2222-2222-2222-222222222222',
  systemSuiteCode: 'SUITE-01',
  systemSuiteName: 'Suite Alpha',
  flagCode: 'ADVANCED_REPORTING',
  flagType: 'Boolean',
  flagTargets: 'role:ADMIN',
  status: 'Inactive',
  linkedResourceType: 'Role',
  linkedResourceId: '33333333-3333-3333-3333-333333333333',
  rolloutPercentage: null,
  criteria: [],
};

describe('FeatureFlagDetailPanel', () => {
  it('renders semantic suite data and hides technical ids from the visible panel', () => {
    render(<FeatureFlagDetailPanel flag={mockFlag} />);

    expect(screen.getByText('ADVANCED_REPORTING')).toBeInTheDocument();
    expect(screen.getByText('Suite Alpha (SUITE-01)')).toBeInTheDocument();
    expect(screen.getByText('Role')).toBeInTheDocument();
    expect(screen.queryByText(mockFlag.featureFlagId)).not.toBeInTheDocument();
    expect(screen.queryByText(mockFlag.systemSuiteId)).not.toBeInTheDocument();
    expect(screen.queryByText(mockFlag.linkedResourceId ?? '')).not.toBeInTheDocument();
  });
});
