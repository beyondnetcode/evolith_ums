import os

search_dir = '/Users/beyondnet/Source/ums/src/apps/ums.api/Evolith.Ums.Presentation.IntegrationTest'

for root, dirs, files in os.walk(search_dir):
    for file in files:
        if file.endswith('.cs'):
            filepath = os.path.join(root, file)
            with open(filepath, 'r') as f:
                content = f.read()

            new_content = content.replace("Object))", "Object)")

            if new_content != content:
                with open(filepath, 'w') as f:
                    f.write(new_content)
                print(f"Fixed {filepath}")
