import os
import re

search_dir = '/Users/beyondnet/Source/ums/src/apps/ums.api/Evolith.Ums.Infrastructure/Persistence'

for root, dirs, files in os.walk(search_dir):
    for file in files:
        if file.endswith('.cs'):
            filepath = os.path.join(root, file)
            with open(filepath, 'r') as f:
                content = f.read()

            new_content = re.sub(
                r'([_]?dbContext)\.OutboxMessages\.AddRange\(OutboxMessageFactory\.CreateFromAggregate\((.*?)\)\);',
                r'await \1.PublishDomainEventsAsync(\2.DomainEvents.GetUncommittedChanges(), cancellationToken);',
                content
            )

            if new_content != content:
                with open(filepath, 'w') as f:
                    f.write(new_content)
                print(f"Updated {filepath}")
