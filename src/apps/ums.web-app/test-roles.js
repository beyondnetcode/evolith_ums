const fetch = require('node-fetch');
async function test() {
  const q = `query RolesBySystemSuite($systemSuiteId: UUID!) {
    rolesBySystemSuite(systemSuiteId: $systemSuiteId) {
      roleId
      tenantId
      systemSuiteId
      parentRoleId
      code
      value
      description
      hierarchyLevel
      promotionOrder
      isActive
    }
  }`;
  try {
    const res = await fetch('https://localhost:7114/graphql', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Tenant-Id': '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      },
      body: JSON.stringify({
        query: q,
        variables: { systemSuiteId: '44c0b33f-e626-4e70-84d1-6d820c94f20a' },
      }),
    });
    const text = await res.text();
    console.log(text);
  } catch (e) {
    console.error(e);
  }
}
test();
