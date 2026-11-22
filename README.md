# Waterless Sprinklers Fixed

A maintained version of Lorenzo's **Waterless Sprinklers** plugin for Rust servers running Oxide/uMod or Carbon.

Sprinklers can operate without a water connection. By default, the feature is available to every player, matching the intended configuration for Scavengers Haven servers.

## Improvements over 1.0.4

- Restores plugin-modified sprinklers when the plugin unloads.
- Reconciles sprinkler state after wire connections and disconnections.
- Applies the IO setting consistently to newly placed and existing sprinklers.
- Handles server-console command attempts without a null-reference exception.
- Validates configuration strings and reports the underlying configuration error.
- Resolves and validates the water item definition during server initialization.
- Safely checks every available IO input and output.
- Marks converted inventory items dirty so their skin change synchronizes and persists.
- Removes dead state parameters and unnecessary scaffolding.

## Installation

Copy `WaterlessSprinklers.cs` into:

- Oxide/uMod: `oxide/plugins/`
- Carbon: `carbon/plugins/`

The plugin creates `WaterlessSprinklers.json` in the framework's configuration directory.

## Default configuration

```json
{
  "Use permission": false,
  "Permission": "waterlesssprinklers.use",
  "PermissionAdmin": "waterlesssprinklers.admin",
  "Make sprinkler special command": "setsprinklerspecial",
  "Only turn on if there is no IO connected to sprinklers": false
}
```

With `Use permission` set to `false`, every player's sprinklers are waterless. Set it to `true` only if you want to restrict normal waterless sprinklers to players with `waterlesssprinklers.use`.

## Permission and command

- `waterlesssprinklers.use` — enables normal waterless sprinklers when permission mode is enabled.
- `waterlesssprinklers.admin` — allows `/setsprinklerspecial`.
- `/setsprinklerspecial` — marks one sprinkler stack in the administrator's main inventory or belt with the special skin ID. A deployed special sprinkler bypasses the normal use permission.

Native Rust administrators may also use the special-sprinkler command.

## Compatibility testing

Always compile and smoke-test the plugin on a staging server after Rust, Oxide, or Carbon updates. This plugin calls Rust game methods directly, so a game update can require corresponding source changes.

Version 1.1.0 was compiled, loaded, hot-reloaded, and console-command tested successfully on the Scavengers Haven TEST SERVER on 2026-11-22. Its generated default configuration was also verified to enable waterless mode for every player.

## License

MIT. See [LICENSE](LICENSE).
