import sys
import os

filepaths = [
    '/Users/beyondnet/Source/ums/src/apps/ums.api/Evolith.Ums.Presentation.IntegrationTest/Infrastructure/UmsApiWebApplicationFactory.cs',
    '/Users/beyondnet/Source/ums/src/apps/ums.api/Evolith.Ums.Presentation.IntegrationTest/Infrastructure/PostgreSqlWebApplicationFactory.cs'
]

for filepath in filepaths:
    if not os.path.exists(filepath): continue
    with open(filepath, 'r') as f:
        content = f.read()

    # Add MassTransit Test Harness
    if 'services.AddMassTransitTestHarness();' not in content:
        content = content.replace('services.AddDbContext<UmsPlatformDbContext>', 'services.AddMassTransitTestHarness();\n            services.AddDbContext<UmsPlatformDbContext>')

    if 'using MassTransit;' not in content:
        content = 'using MassTransit;\n' + content

    with open(filepath, 'w') as f:
        f.write(content)

    print(f"Updated {filepath}")
