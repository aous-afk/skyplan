# skyplan

**Cities Skylines 2 mod** — in-game drawing overlay for city planning.

Tracing paper over your city map, but inside CS2. Draw road networks, zoning, districts, and transit lines on top of the actual game map before committing anything.

## How it works

CS2 uses **Coherent GameFace** (a browser engine embedded in Unity) for all UI. Mod panels are HTML/CSS/JS — SVG drawing tools are a natural fit.

- **C# side** reads game state, registers the UI panel, and passes map metadata (bounds, tile size, camera position) to JS via Coherent bridge
- **JS side** renders an SVG canvas sized to match the game map, hosts the drawing tools, and sends geometry back to C# when needed (e.g. snapping to terrain)

## Architecture

```
skyplan/
  Mod.cs                    # IMod entry point — OnLoad / OnDispose
  Systems/
    DrawingSystem.cs        # GameSystemBase — exposes map data to UI
  UI/
    index.html              # Coherent GameFace panel root
    app.js                  # SVG drawing logic, bridge calls
    styles.css
  skyplan.csproj
```

## Map dimensions

| Thing | Value |
|---|---|
| Full terrain | 57,344 × 57,344 m |
| Heightmap | 4096 × 4096 px (~14 m/px) |
| Playable area (base) | 5×5 tiles — 2,560 × 2,560 m |
| Playable area (max) | 9×9 tiles with expansions |
| Tile size | 512 × 512 m |

SVG `viewBox` matches the selected playable area. 1 SVG unit = 1 m.

## Planned features

- [ ] SVG canvas overlay sized to current map's playable area
- [ ] Draw tools: freehand line, straight segment, rectangle, circle
- [ ] Layers: roads / zoning / transit / notes (toggle per layer)
- [ ] Snap to 512 m tile grid
- [ ] Named saved plans (stored as JSON in mod data folder)
- [ ] Export SVG for external use / printing

## Building & deploying (Linux / Proton)

```bash
# Build
dotnet build

# Deploy to CS2 mods folder
cp bin/Debug/net8.0/skyplan.dll \
  ~/.local/share/Steam/steamapps/compatdata/949230/pfx/drive_c/Users/steamuser/AppData/LocalLow/Colossal\ Order/Cities\ Skylines\ II/Mods/skyplan/

# Watch Unity log (no debugger available under Proton)
tail -f ~/.local/share/Steam/steamapps/compatdata/949230/pfx/drive_c/Users/steamuser/AppData/LocalLow/Colossal\ Order/Cities\ Skylines\ II/Player.log
```

## Prerequisites

- CS2 installed at default Steam path
- Game assemblies present in `Cities2_Data/Managed/`
- ECS source generators configured as `<Analyzer>` items in `.csproj`
- dotnet SDK 8+
