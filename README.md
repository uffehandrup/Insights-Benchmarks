# XFlow Insights Microservice

A lightweight microservice for managing workflow insights using event sourcing with Marten and PostgreSQL.

## Overview

This microservice is responsible for:
- Receiving and storing workflow completion events
- Maintaining a read model projection of workflow details
- Providing efficient access to workflow information

Built with event sourcing principles for high consistency and auditability.

## Prerequisites

- .NET 8.0 SDK
- PostgreSQL 12+
- Docker & Docker Compose (optional, for containerized setup)

## Docker Compose usage

The `docker-compose.yml` is split into:

- **Default services (always-on infra):** `event-store`, `eventstoredb`, `pgadmin`
- **Optional API service:** profile `app`
- **On-demand benchmark runners:** profile `benchmarks`

### Start only infrastructure

```bash
docker compose up -d
```

### Start API in container (networked like production)

```bash
docker compose --profile app up -d insights-api
```

### Run benchmarks only when you choose

PostgreSQL benchmark:

```bash
docker compose --profile benchmarks run --rm benchmark-postgresql
```

EventStoreDB benchmark:

```bash
docker compose --profile benchmarks run --rm benchmark-eventstoredb
```

Both benchmark projects (one after another):

```bash
docker compose --profile benchmarks run --rm benchmark-postgresql && \
docker compose --profile benchmarks run --rm benchmark-eventstoredb
```

This avoids benchmarks running automatically when opening/running normal compose startup.