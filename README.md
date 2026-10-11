# Kyo Mod

A personal [tModLoader](https://github.com/tModLoader/tModLoader) mod for Terraria, built around a small
number of deeply-developed weapons rather than a broad content pack.

Each weapon upgrades through the vanilla boss progression and carries **two forms**: parallel playstyles
the player switches between, not a power ladder.

> **Status:** in active development, not released. Expect things to move.

> **Note:** some of these weapons are inspired by characters from *Wuthering Waves*.

## Weapons

### Crying Tides (melee)

A broadsword with a three-hit combo, a right-click skill, and an ultimate. Middle-click opens a panel to
switch form.

| Form | Plays like |
| --- | --- |
| **Spectro** | Ranged: beams and slash arcs that travel |
| **Havoc** | Close-range: builds stacks for burst |

Nine tiers: base → Eye of Cthulhu → Evil Boss → Skeletron → Wall of Flesh → Mechanical → Plantera →
Golem → Moon Lord.

### Bubbling Oblivion (magic)

A staff with a three-hit combo. **The ultimate *is* the form switch**, so changing form always costs
something and always has a spectacle attached.

| Form | Plays like |
| --- | --- |
| **Bubble** | Homing bolts, pastel palette, area control |
| **Void** | A black hole that collapses into a persistent damaging field |

Three tiers so far: base → Eye of Cthulhu → Evil Boss.

## Visuals

Most of the work in this mod is in the effects, and all of them are custom: hand-written HLSL shaders and
hand-built mesh geometry rather than stock Terraria dust and sprites.

Shader sources live in `docs/Shaders/`.

## Building

Clone into your tModLoader `ModSources` directory, then `dotnet build`, or build from tModLoader's own
**Workshop → Develop Mods** menu.

## Credits

Code and art by **Kyo** ([@TobyNguyen710](https://github.com/TobyNguyen710)).
