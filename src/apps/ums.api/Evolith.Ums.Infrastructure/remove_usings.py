import os
import re

search_dirs = [
    '/Users/beyondnet/Source/ums/src/apps/ums.api/Evolith.Ums.Infrastructure',
]

for d in search_dirs:
    for root, dirs, files in os.walk(d):
        for file in files:
            if file.endswith('.cs'):
                filepath = os.path.join(root, file)
                with open(filepath, 'r') as f:
                    content = f.read()

                new_content = re.sub(r'using Ums\.Infrastructure\.Persistence\.Outbox;\n?', '', content)
                new_content = re.sub(r'using Ums\.Infrastructure\.HealthChecks;\n?', '', new_content)

                if new_content != content:
                    with open(filepath, 'w') as f:
                        f.write(new_content)
                    print(f"Updated {filepath}")
