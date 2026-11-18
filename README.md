# OpoStudy

Aplicación móvil (.NET MAUI) para repasar y practicar el temario de oposiciones mediante tests con aclaraciones y supuestos prácticos corregidos por IA.

> Estado: **pre-alpha** — proyecto personal y didáctico.

Oposición inicial: **Programador Informático (C1) — Diputación de Málaga**.

## Stack
- App: .NET 10 MAUI (Android primero, iOS después)
- API: ASP.NET Core 10 (Minimal APIs) + Worker, orquestado en local con .NET Aspire
- Datos: PostgreSQL 17 + EF Core 10
- Auth: Firebase Authentication (Google, Facebook, correo)
- IA (solo supuestos prácticos): proveedor configurable (Gemini / Claude)
- Observabilidad: OpenTelemetry + Aspire Dashboard
- Despliegue: Docker Compose en VPS

## Flujo de trabajo
Gitflow (`main`, `develop`, `feature/*`, `release/*`, `hotfix/*`). Todo cambio entra por Pull Request con CI en verde.

## Secretos
Este repositorio es **público**. Nunca se commitean secretos:
- Local: `dotnet user-secrets`
- CI: GitHub Actions secrets
- Servidor: Docker secrets

Tras clonar, activa el hook anti-secretos (requiere [gitleaks](https://github.com/gitleaks/gitleaks)):

```bash
git config core.hooksPath .githooks
```
