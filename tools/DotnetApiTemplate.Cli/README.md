# dotnet-api-template

Scaffolds a clean-architecture ASP.NET Core Web API with JWT user management, an invite-by-email
flow over Kafka, a transactional outbox, EF Core on PostgreSQL, Redis, a migrator job, background
workers and a Docker Compose stack.

```bash
dotnet tool install --global DotnetApiTemplate.Cli
dotnet-api-template new --project-name Acme.Billing --allow-grpc
```

The project name is applied across the board — the solution, every project, every assembly and
every namespace.

Full documentation: <https://github.com/profmcdan/dotnet-api-template>
