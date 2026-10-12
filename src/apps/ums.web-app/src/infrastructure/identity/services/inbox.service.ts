import { httpClient } from '@infra/http/httpClient';
import {
  PendingUserSignupListSchema,
  PendingProfileRequestListSchema,
  type PendingUserSignup,
  type PendingProfileRequest,
} from '@domain/identity/schemas/inbox.schema';

export const inboxService = {
  getUserSignups: async (): Promise<PendingUserSignup[]> => {
    const { data } = await httpClient.get('/onboarding/inbox/user-signups');
    return PendingUserSignupListSchema.parse(data);
  },

  getProfileRequests: async (): Promise<PendingProfileRequest[]> => {
    const { data } = await httpClient.get('/onboarding/inbox/profile-requests');
    return PendingProfileRequestListSchema.parse(data);
  },

  activateUser: async (userAccountId: string): Promise<void> => {
    await httpClient.post(`/users/${userAccountId}/activate`);
  },

  denyUserSignup: async (userAccountId: string, reason?: string): Promise<void> => {
    await httpClient.post(`/users/${userAccountId}/deny-signup`, { reason });
  },

  approveProfileRequest: async (
    approvalRequestId: string,
    grantedRoleId: string,
    decisionReason?: string
  ): Promise<void> => {
    await httpClient.post(`/approval-requests/${approvalRequestId}/approve`, {
      grantedRoleId,
      decisionReason,
    });
  },

  rejectProfileRequest: async (
    approvalRequestId: string,
    decisionReason?: string
  ): Promise<void> => {
    await httpClient.post(`/approval-requests/${approvalRequestId}/reject`, { decisionReason });
  },
};

export default inboxService;
