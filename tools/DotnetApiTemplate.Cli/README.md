# dotnet-api-template

Scaffolds a clean-architecture ASP.NET Core Web API with JWT user management, an invite-by-email
flow over Kafka, a transactional outbox, EF Core on PostgreSQL, Redis, a migrator job, background
workers and a Docker Compose stack.

```bash
dotnet tool install --global ProfmcdanDotnetApiTemplate.Cli
dotnet-api-template new --project-name Acme.Billing --allow-grpc
```

The project template ships inside the tool, so that is the only thing to install.

The name is applied across the board — the solution, every project, every assembly and every
namespace become `Acme.Billing.*`. The CLI also writes `.env` with a freshly generated JWT signing
key and database password, and makes the first commit.

| Option | Alias | Default |
| --- | --- | --- |
| `--project-name` | `-n` | required |
| `--output` | `-o` | current directory |
| `--allow-grpc` | `-g` | off |
| `--kafka-topic-prefix` | `-k` | lower-cased project name |
| `--api-port` | `-p` | `5080` |
| `--database-name` | `-d` | `appdb` |
| `--no-tests` `--no-docs` `--no-env` `--no-git` `--dry-run` | | off |

Run `dotnet-api-template new --help` for the full list, or `dotnet-api-template info` to check
your machine has what a generated project needs.

Full documentation: <https://github.com/profmcdan/dotnet-api-template>
