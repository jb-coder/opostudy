# 0001 — Setup del repositorio y del proyecto

- **Issue:** #1
- **Milestone:** v0.0.1
- **Estado:** implementada

## Objetivo
Dejar el repositorio listo para trabajar con Gitflow, CI y protección de secretos, con una solución .NET 10 que compila y pasa tests.

## Alcance
**Incluye:**
- Repo con `.gitignore` estricto, hook `pre-commit` con gitleaks, `.gitattributes`, `.editorconfig`.
- Solución `OpoStudy.slnx`, filtro `OpoStudy.Backend.slnf` (sin MAUI) y gestión central de paquetes (`Directory.Packages.props`).
- Proyectos: AppHost (Aspire + PostgreSQL), ServiceDefaults, Api, Worker, Domain, Infrastructure, Contracts, ContentImporter, Mobile (Android).
- API mínima: `/health`, `/alive`, `GET /api/v1/version`, ProblemDetails, OpenAPI + Scalar en Development.
- Tests: integración de la API (WebApplicationFactory) y arquitectura (NetArchTest).
- GitHub:
  - Workflows: CI (gitleaks, backend, Android), CodeQL y Dependabot.
  - Plantillas de issues y PR.
- ADR 0001.

**No incluye:**
- Modelo de datos ni EF Core (v0.1.0-alpha).
- Autenticación (v0.1.0-alpha).

## Criterios de aceptación
- [x] `dotnet build OpoStudy.Backend.slnf` sin warnings.
- [x] `dotnet test --solution OpoStudy.Backend.slnf` en verde.
- [x] `dotnet build src/OpoStudy.Mobile -f net10.0-android` compila.
- [x] El hook de gitleaks bloquea un token de prueba.
- [x] `.claude/` y `CLAUDE.md` aparecen como ignorados en `git status --ignored`.
