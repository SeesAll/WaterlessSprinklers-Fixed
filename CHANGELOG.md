# Changelog

## 1.3.0 - 2026-11-22

- Added `Waterless sprinklers enabled by default` so servers can make each TC opt-in or opt-out.
- Preserved saved v1.2 disabled-TC choices during upgrade.
- Added persistent enabled-TC overrides for servers whose default is off.
- Applied the configured default to sprinklers outside TC coverage.

## 1.2.0 - 2026-11-22

- Added `/sprinklers on`, `/sprinklers off`, and `/sprinklers status`.
- Restricted control to players authorized on the TC governing their current building privilege area.
- Scoped changes to sprinklers governed by that exact TC so separate farms remain independent.
- Persisted disabled TC areas across restarts and cleared them when the TC is destroyed or a new map begins.
- Kept manual TC auth, TCAuth, and DynamicCupShare compatible without hard plugin dependencies.

## 1.1.0 - 2026-11-22

- Fixed cleanup on plugin unload.
- Fixed inconsistent IO-connection handling.
- Added reconciliation for wire connect, wire clear, and cleared IO references.
- Added event-driven reconciliation for relevant user and group permission changes.
- Fixed server-console command null reference.
- Added configuration validation and useful error reporting.
- Added safe item-definition and IO-array checks.
- Added inventory dirty marking after applying the special skin.
- Kept waterless sprinklers enabled for all players by default.
- Removed dead and redundant code.
- Verified compilation, load, hot reload, console command handling, and generated defaults on the Scavengers Haven TEST SERVER.

## 1.0.4

- Upstream version reviewed as the basis for this maintained fork.
