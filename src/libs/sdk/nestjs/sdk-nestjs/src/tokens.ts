/**
 * DI tokens used by `UmsSdkModule` to register the validator, accessor and module options.
 * Consumers can override any of these in their own Nest module if they need a different impl.
 */
export const UMS_SDK_OPTIONS = Symbol('UMS_SDK_OPTIONS');
export const UMS_AUTH_GRAPH_ACCESSOR = Symbol('UMS_AUTH_GRAPH_ACCESSOR');
export const UMS_AUTHORIZATION_VALIDATOR = Symbol('UMS_AUTHORIZATION_VALIDATOR');
export const UMS_AUTHORIZATION_LOGGER = Symbol('UMS_AUTHORIZATION_LOGGER');
