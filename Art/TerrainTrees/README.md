# Terrain tree kit

Eight flat-shaded mesh assets: Oak Large/Medium/Small, Pine Large/Medium/Small, Birch, DeadTree.
FBX exports: `Assets/_Game/Environment/Trees`. Origin at ground level; meters; Y-up FBX export. Meshes use solid material colors, no textures or external texture dependencies.

Unity: allow import and script compilation, then use **Tools > Terrain Trees > Create Prefabs**. The scoped importer creates shared URP Lit materials with metallic and smoothness zero. Existing prefabs are preserved. Prefabs get a simple trunk capsule collider. Drag FBX models directly into scenes if prefabs are unnecessary.

`Individual/` contains one separate Blender file per tree, with one scene object at the origin. `Trees_Library.blend` also contains the complete library. `Forest_Demo.blend` is the staged forest scene; `Forest_Preview.png` is its Blender render, not a Unity screenshot. `Trees_Review.png` shows the re-imported FBX models. `mesh_stats.json` records geometry counts. Unity import and appearance must be checked in the editor; no LODs or wind animation are included.

Rebuild with Blender in background mode using `Tools/TerrainTrees/build_trees.py`. Rebuilding overwrites generated FBX and Blender outputs.
