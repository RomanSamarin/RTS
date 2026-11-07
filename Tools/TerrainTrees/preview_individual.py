import bpy, os, json
from mathutils import Vector
root=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
art=os.path.join(root,'Art/TerrainTrees')
folder=os.path.join(art,'Individual')
report={}
for name in sorted(os.listdir(folder)):
    if not name.endswith('.blend'): continue
    bpy.ops.wm.open_mainfile(filepath=os.path.join(folder,name))
    objects=list(bpy.context.scene.objects)
    assert len(objects)==1,(name,len(objects))
    obj=objects[0]
    assert obj.location.length<.001
    assert not obj.data.validate(),name
    report[name]={'scene_objects':len(objects),'vertices':len(obj.data.vertices)}
bpy.ops.wm.read_factory_settings(use_empty=True)
for index,name in enumerate(sorted(report)):
    fbx=os.path.join(root,'Assets/_Game/Environment/Trees',name.replace('.blend','.fbx'))
    bpy.ops.import_scene.fbx(filepath=fbx)
    selected=list(bpy.context.selected_objects)
    assert len(selected)==1,(fbx,len(selected))
    obj=selected[0]
    assert not obj.data.validate(),fbx
    obj.location=((index%4)*7,(index//4)*9,0)
with open(os.path.join(art,'validation.json'),'w') as f: json.dump(report,f,indent=2)
bpy.ops.mesh.primitive_plane_add(size=200,location=(10,5,-.015))
mat=bpy.data.materials.new('Studio'); mat.diffuse_color=(.55,.55,.51,1); bpy.context.object.data.materials.append(mat)
bpy.ops.object.camera_add(location=(22,-34,23))
cam=bpy.context.object; cam.rotation_euler=(Vector((10,4,2.6))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=32; bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(1,-8,20)); bpy.context.object.data.energy=2800; bpy.context.object.data.size=12
bpy.ops.object.light_add(type='SUN'); bpy.context.object.rotation_euler=(.4,-.5,-.2); bpy.context.object.data.energy=2
sc=bpy.context.scene; sc.world=bpy.data.worlds.new('StudioWorld'); sc.world.color=(.4,.4,.4); sc.render.engine='CYCLES'; sc.cycles.samples=32
sc.render.resolution_x=1800; sc.render.resolution_y=1100; sc.render.resolution_percentage=100
sc.render.filepath=os.path.join(art,'Trees_Review.png'); bpy.ops.render.render(write_still=True)
print('VALIDATED 8 BLEND FILES AND 8 FBX FILES',flush=True)
