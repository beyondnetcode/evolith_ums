import {
  ArgumentsHost,
  Catch,
  type ExceptionFilter,
  ForbiddenException,
  HttpStatus
} from '@nestjs/common';
import { AuthorizationDeniedError } from '@evolith-ums/ums-sdk-authorization';

/**
 * Maps `AuthorizationDeniedError` (thrown by `UmsAuthGuard`) to a NestJS `ForbiddenException`
 * (HTTP 403) with a structured body matching the canonical SDK error shape.
 *
 * Registered automatically by `UmsSdkModule.forRoot()` as `APP_FILTER`. Replace with your own
 * `@Catch(AuthorizationDeniedError)` filter to customize the wire shape.
 */
@Catch(AuthorizationDeniedError)
export class AuthorizationDeniedFilter implements ExceptionFilter<AuthorizationDeniedError> {
  catch(error: AuthorizationDeniedError, host: ArgumentsHost): void {
    const decision = error.decision;
    const httpHost = host.switchToHttp();
    const response = httpHost.getResponse<{ status: (code: number) => { json: (body: unknown) => void } }>();
    response.status(HttpStatus.FORBIDDEN).json({
      statusCode: HttpStatus.FORBIDDEN,
      error: 'Forbidden',
      code: decision.errorCode ?? 'AUTH_UNKNOWN',
      message: decision.reason ?? 'Authorization denied.',
      primitive: decision.primitive,
      target: decision.target,
      graphRequestId: decision.graphRequestId
    });
    // Backstop: if the response object doesn't have status/json (shouldn't happen in Express),
    // rethrow as ForbiddenException so Nest's default exception flow takes over.
    if (typeof response?.status !== 'function') {
      throw new ForbiddenException(decision.reason ?? 'Authorization denied.');
    }
  }
}
