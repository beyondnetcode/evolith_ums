import re

file_path = "/Users/beyondnet/Source/ums/infra/local/compose/docker-compose.yml"
with open(file_path, "r") as f:
    content = f.read()

# Replace sqlserver-lite with postgres
sqlserver_block = """  sqlserver-lite:
    image: mcr.microsoft.com/azure-sql-edge:latest
    container_name: ums-sqlserver-lite
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "Your_password123"
      MSSQL_PID: "Developer"
    ports:
      - "1433:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql
    healthcheck:
      test:
        [
          "CMD-SHELL",
          "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"$${MSSQL_SA_PASSWORD}\" -Q \"SELECT 1\" || /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P \"$${MSSQL_SA_PASSWORD}\" -Q \"SELECT 1\"",
        ]
      interval: 15s
      timeout: 10s
      retries: 10
      start_period: 45s
    networks:
      - ums-network"""

postgres_block = """  postgres:
    image: postgres:15-alpine
    container_name: ums-postgres
    environment:
      POSTGRES_USER: "postgres"
      POSTGRES_PASSWORD: "Your_password123"
      POSTGRES_DB: "UmsDev"
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d UmsDev"]
      interval: 15s
      timeout: 10s
      retries: 10
      start_period: 15s
    networks:
      - ums-network"""

content = content.replace(sqlserver_block, postgres_block)

# Replace references to sqlserver-lite
content = content.replace("Server=sqlserver-lite,1433;Database=UmsDev;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False;", "Host=postgres;Port=5432;Database=UmsDev;Username=postgres;Password=Your_password123;")
content = content.replace("sqlserver-lite:", "postgres:")
content = content.replace("sqlserver_data:", "postgres_data:")
content = content.replace("Persistence__Provider: SqlServer", "Persistence__Provider: PostgreSql")
content = content.replace("Persistence__AggregateStoreMode: SqlServer", "Persistence__AggregateStoreMode: PostgreSql")
content = content.replace("Persistence__UseSqlServerIdentityStores: \"true\"", "Persistence__UsePostgreSqlIdentityStores: \"true\"")
content = content.replace("Persistence__UseSqlServerAuthorizationStores: \"true\"", "Persistence__UsePostgreSqlAuthorizationStores: \"true\"")
content = content.replace("Persistence__UseSqlServerConfigurationStores: \"true\"", "Persistence__UsePostgreSqlConfigurationStores: \"true\"")

with open(file_path, "w") as f:
    f.write(content)

print("Updated docker-compose.yml successfully.")
