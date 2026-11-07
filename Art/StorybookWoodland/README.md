# Storybook woodland

15 flat-shaded assets matching the supplied woodland concept: ancient oak, young oak, tall pine, short spruce, birch, dead tree, broad bush, round bush, berry bush, tall/medium/small grass, reeds, wildflowers, fern.

- `Individual/`: separate editable Blender file for each model, exactly one mesh object at origin. Materials are embedded; no textures required.
- `Assets/_Game/Environment/StorybookWoodland/`: individual FBX exports in meters, Y up, for Unity. Use/extract the imported solid-color materials with URP Lit, Metallic 0 and Smoothness 0.
- `Woodland_Library.blend`: arranged model library.
- `Woodland_Diorama.blend`: composed woodland with path, stream, rocks and studio camera/lighting.
- `Woodland_Preview.png`: actual Blender Cycles render of the diorama.
- `manifest.json`: geometry counts. `validation.json`: separate-file and FBX round-trip checks.

Materials use roughness 1 and metallic 0. No wind animation, rig or LODs. The FBX files were checked by reimport into Blender; Unity editor appearance has not been verified. The diorama's soil, path, water and rocks are scene dressing and are saved in its Blender scene.

Rebuild using Blender background mode with `Tools/build_woodland.py`. This overwrites this kit's generated files. Previous tree kits remain separate.
