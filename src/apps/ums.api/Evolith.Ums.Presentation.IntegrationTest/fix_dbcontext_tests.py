import os
import re

search_dir = '/Users/beyondnet/Source/ums/src/apps/ums.api/Evolith.Ums.Presentation.IntegrationTest'

for root, dirs, files in os.walk(search_dir):
    for file in files:
        if file.endswith('.cs'):
            filepath = os.path.join(root, file)
            with open(filepath, 'r') as f:
                content = f.read()

            # Match new UmsPlatformDbContext(options, tenantContext)
            # using regex
            new_content = re.sub(
                r'new UmsPlatformDbContext\(([^,]+),\s*([^)]+)\)',
                r'new UmsPlatformDbContext(\1, \2, new Moq.Mock<MassTransit.IPublishEndpoint>().Object)',
                content
            )

            if new_content != content:
                with open(filepath, 'w') as f:
                    f.write(new_content)
                print(f"Updated {filepath}")
