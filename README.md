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