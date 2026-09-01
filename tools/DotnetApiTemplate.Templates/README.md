# ProfmcdanDotnetApiTemplate.Templates

A `dotnet new` template for a clean-architecture ASP.NET Core Web API: JWT user management with an
invite-by-email flow over Kafka, a transactional outbox, EF Core on PostgreSQL, Redis, a migrator
job, background workers and a Docker Compose stack.

```bash
dotnet new install ProfmcdanDotnetApiTemplate.Templates
dotnet new cleanapi -n Acme.Billing --allow-grpc --kafka-topic-prefix billing
```

The name is applied across the board — the solution, every project, every assembly and every
namespace.

Prefer a single command that also generates your secrets and makes the first commit? Install the
CLI instead:

```bash
dotnet tool install --global ProfmcdanDotnetApiTemplate.Cli
dotnet-api-template new --project-name Acme.Billing --allow-grpc
```

Full documentation: <https://github.com/profmcdan/dotnet-api-template>
