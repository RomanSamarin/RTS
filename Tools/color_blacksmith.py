import bpy,os,random,math
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
OUT=os.path.join(ROOT,'Art/BlackSmith'); os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath='C:/Users/Роман/Downloads/BlackSmith.blend')
random.seed(8)
def mat(name,c):
 m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1); p.inputs['Roughness'].default_value=.95
 return m
wood=[mat('Timber_WarmOak_'+str(i),(.23+i*.018,.115+i*.012,.047+i*.007)) for i in range(4)]
stone=[mat('Foundation_Sandstone_'+str(i),(.26+i*.022,.245+i*.019,.205+i*.016)) for i in range(5)]
slate=[mat('Roof_BlueSlate_'+str(i),(.067+i*.010,.088+i*.012,.12+i*.014)) for i in range(5)]
plaster=mat('Walls_WarmLime',(.66,.53,.35)); iron=mat('Iron_Charcoal',(.075,.082,.087)); gold=mat('Window_WarmAmber',(.70,.35,.075))
models=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in models:
 n=int(o.name.split('.')[-1]) if '.' in o.name else 0
 m=random.choice(wood)
 if n<=6 or 15<=n<=43 or 48<=n<=57 or 63<=n<=65 or 80<=n<=97 or 103<=n<=112 or 114<=n<=125: m=random.choice(stone)
 if n in [44,45,62,79,98]: m=plaster
 if n==100 or 126<=n<=174: m=random.choice(slate)
 if n in [67,69,70,71,72,73,74,75,76,77,78]: m=random.choice(wood)
 if n in [12]: m=iron
 if n in [13,14,17]: m=random.choice(wood)
 if n in [175,177,178,179,180]: m=random.choice(wood)
 # Shared source meshes are copied so individual tiles keep their assigned colors.
 o.data=o.data.copy(); o.data.materials.clear(); o.data.materials.append(m)
 for p in o.data.polygons: p.material_index=0
 if n==175:
  o.data.materials.append(iron)
  for p in o.data.polygons:
   if p.center.z<-.12: p.material_index=1
# Save the colored source with original geometry and scene setup.
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'BlackSmith_Colored.blend'))
# Separate presentation scene: reuse actual colored meshes.
scene=bpy.data.scenes.new('Reference_Preview'); bpy.context.window.scene=scene
for o in models: scene.collection.objects.link(o)
points=[o.matrix_world@Vector(c) for o in models for c in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3))); hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=(lo+hi)*.5; size=max(hi-lo)
bpy.ops.mesh.primitive_plane_add(size=size*200,location=(center.x,center.y,lo.z-.015))
bpy.context.object.data.materials.append(mat('Studio_Backdrop',(.115,.13,.12)))
bpy.ops.object.camera_add(location=center+Vector((1,-1.5,1.05))*size*2)
cam=bpy.context.object; cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=size*1.75; scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=center+Vector((-.6,-1,2))*size); bpy.context.object.data.energy=65*size*size; bpy.context.object.data.size=size*1.7; bpy.context.object.data.color=(1,.86,.69)
bpy.ops.object.light_add(type='AREA',location=center+Vector((1,1,1))*size); bpy.context.object.data.energy=22*size*size; bpy.context.object.data.size=size*2
scene.world=bpy.data.worlds.new('PreviewWorld'); scene.world.use_nodes=True; scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.26,.32,1); scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.render.resolution_x=1500; scene.render.resolution_y=1300; scene.render.resolution_percentage=100
scene.render.filepath=os.path.join(OUT,'BlackSmith_Colored_Preview.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'BlackSmith_Colored.blend'))
bpy.ops.render.render(write_still=True)
print('COLORED',len(models),'MESHES',flush=True)
