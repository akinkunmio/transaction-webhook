# Transaction Webhook Service

## Overview
A minimal .NET webhook service that validates incoming transaction payloads and stores them in PostgreSQL. Includes request validation, HMAC signature support, and a health endpoint.

## Brief explanation
This service receives POST webhooks at `/webhooks/transactions`, validates payload fields and timestamps, and stores transactions in a PostgreSQL database. It uses a lean .NET 10 minimal API with a repository layer for database interaction, keeping implementation small and easy to maintain. Security is handled through HMAC validation and configuration-driven connection strings. Health checks are exposed for operational readiness.

## Assumptions
- PostgreSQL is the production database.
- `appsettings.json` or `POSTGRES_CONNECTION` provide DB credentials.
- Webhook consumers can sign requests using the shared secret.

## Decision justification
- Use PostgreSQL by default because the service is designed for persisted transaction data and the repo already targets Postgres schema.
- Keep the API minimal and dependency-light to reduce runtime surface area and simplify deployment.

## Rejected alternative
- Avoided serverless Lambda for the main webhook endpoint because transaction persistence and consistent database connectivity are better suited to a long-running container or service.

## Failure scenario
If the database is unavailable, webhook requests fail fast and return an error rather than silently dropping data.

## In-Memory Database (Development/Testing)

### Brief explanation
The service automatically uses an in-memory SQLite database when running in Development mode or when `UseInMemoryDatabase` is configured. This eliminates the dependency on a running PostgreSQL instance for local development and testing. The in-memory database is initialized at startup with the same schema as production, persists across multiple requests within a single app session, and is automatically cleaned up when the app shuts down.

### Assumptions
- Development mode is enabled via `ASPNETCORE_ENVIRONMENT=Development` or `UseInMemoryDatabase=true` in configuration.
- In-memory data is transient and lost on app restart; this is acceptable for dev/test scenarios only.
- All clients connecting to the dev instance share the same in-memory database (single-threaded access pattern within a session).

### Decision justification
- Use shared-mode SQLite in-memory database (`mode=memory&cache=shared`) because it allows transaction isolation while staying lightweight and fast for local iteration.
- Activate automatically in Development mode because it reduces friction—developers can run `dotnet run` without setting up PostgreSQL infrastructure locally.

### Rejected alternative
- Avoided a separate test database fixture because the shared in-memory SQLite connection initialized at app startup meets dev/test needs and keeps the code simple.

### Failure scenario
If the SQLite in-memory connection is lost or disposed prematurely (e.g., early app shutdown), subsequent requests fail with a `SqliteException` rather than silently losing data, making the issue visible for debugging.

## AWS / Cloud
- Hosting: deploy as a container to ECS Fargate for a simple, managed runtime with auto-scaling and isolated networking.
- PostgreSQL: use Amazon RDS for PostgreSQL with multi-AZ in production and automated backups.
- Secrets/config: store DB credentials and webhook secret in AWS Secrets Manager or AWS Systems Manager Parameter Store, and read them through environment variables.
- Logging/monitoring: send structured logs to Amazon CloudWatch Logs and use CloudWatch Alarms on error rates, latency, and RDS health.
- Webhook security: require HMAC signatures, verify the payload before processing, use HTTPS, and rotate shared secrets regularly.

## CI/CD
1. Build the .NET app: `dotnet build src/src.csproj`
2. Run tests: `dotnet test tests/tests.csproj`
3. Build deployable artifact: create a Docker image for the service.
4. Push to registry: push the image to Amazon ECR.
5. Deploy to AWS: update ECS service/task definition with the new image and use a deployment pipeline to roll out changes.
