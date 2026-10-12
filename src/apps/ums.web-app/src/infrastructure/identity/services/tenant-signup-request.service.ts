import { httpClient } from '@infra/http/httpClient';
import { logger } from '@app/utils/logger';
import {
  ApproveTenantSignupResponseSchema,
  TenantSignupRequestListSchema,
  type ApproveTenantSignupResponse,
  type TenantSignupRequest,
} from '@domain/identity/schemas/tenant-signup-request.schema';

const tenantSignupRequestService = {
  getPending: async (): Promise<TenantSignupRequest[]> => {
    const { data } = await httpClient.get('/tenants/signup-requests');
    const parsed = TenantSignupRequestListSchema.safeParse(data);
    if (!parsed.success) {
      logger.error('Invalid tenant signup request response shape', data);
      throw new Error('Invalid tenant signup request response shape');
    }
    return parsed.data;
  },

  approve: async (tenantSignupRequestId: string): Promise<ApproveTenantSignupResponse> => {
    const { data } = await httpClient.post(
      `/tenants/signup-requests/${tenantSignupRequestId}/approve`
    );
    const parsed = ApproveTenantSignupResponseSchema.safeParse(data);
    if (!parsed.success) {
      logger.error('Invalid tenant signup approval response shape', data);
      throw new Error('Invalid tenant signup approval response shape');
    }
    return parsed.data;
  },
};

export default tenantSignupRequestService;
