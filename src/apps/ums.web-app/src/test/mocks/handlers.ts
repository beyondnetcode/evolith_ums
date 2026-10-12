import { http, HttpResponse } from 'msw';

export const handlers = [
  // Example REST interception
  http.get('*/api/v1/profiles/*', () => {
    return HttpResponse.json({
      id: 'mock-id',
      userId: 'mock-user',
      templates: [],
      permissions: [],
      status: 'Active',
    });
  }),
];
