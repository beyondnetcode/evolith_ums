import React from 'react';
import { PageShell } from '../layouts/PageShell';
import { MasterDetailLayout } from '../layouts/MasterDetailLayout';

export interface PageDashboardShellProps {
  /** Label for accessibility and resizing the splitter handle. */
  splitterLabel: string;
  /** Visual overlay container (typically containing form/action dialogs). */
  overlay?: React.ReactNode;
  /** Master panel slot (typically containing search criteria and lists/thumbnails). */
  master: React.ReactNode;
  /** Detail panel slot (typically containing the active item's tabs, overview, and actions). */
  detail: React.ReactNode;
}

export const PageDashboardShell: React.FC<PageDashboardShellProps> = React.memo(
  ({ splitterLabel, overlay, master, detail }) => {
    return (
      <PageShell>
        <MasterDetailLayout
          splitterLabel={splitterLabel}
          overlay={overlay}
          master={master}
          detail={detail}
        />
      </PageShell>
    );
  }
);

PageDashboardShell.displayName = 'PageDashboardShell';
