# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog (https://keepachangelog.com/en/1.1.0/) and this project adheres to Semantic Versioning.

## [1.0.6] - 2026-02-23
### Changed
- Expanded README documentation for `Lookup<T>` usage, including DTO property definitions and assignment patterns (GUID, nullable GUID, alternate key, and raw key expression).

## [1.0.1] - 2025-10-03
### Added
- Centralized `JsonSerializerOptions` with shared configuration and customization hooks.
- `DataverseJsonSerializerOptionsFactory` to build default serializer options.
- `IDataverseJsonSerializerOptionsConfigurator` interface for pluggable serializer customization.
- `AddDataverseJsonSerializerConfigurator` extension method.
- README documentation for JSON serialization customization.

### Changed
- `DataverseRepository<T>` now receives `JsonSerializerOptions` via DI instead of constructing internally.

## [1.0.0] - 2025-10-02
### Added
- Initial release with HTTP client, repository pattern, query builder, DTO base, execution context models, retry & logging helpers.
