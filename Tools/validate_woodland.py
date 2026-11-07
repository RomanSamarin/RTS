import bpy,os,json
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..')); ART=os.path.join(ROOT,'Art/StorybookWoodland')
report={}
for name in sorted(os.listdir(os.path.join(ART,'Individual'))):
 if not name.endswith('.blend'):continue
 bpy.ops.wm.open_mainfile(filepath=os.path.join(ART,'Individual',name))
 objects=list(bpy.context.scene.objects)
 assert len(objects)==1 and objects[0].type=='MESH',name
 assert objects[0].location.length<.001,name
 assert not objects[0].data.validate(),name
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT,'Assets/_Game/Environment/StorybookWoodland',name.replace('.blend','.fbx')))
 objects=list(bpy.context.scene.objects)
 assert len(objects)==1 and not objects[0].data.validate(),name
 report[name]={'single_object':True,'origin_at_ground':True,'fbx_reimport':True,'triangles':sum(len(p.vertices)-2 for p in objects[0].data.polygons)}
with open(os.path.join(ART,'validation.json'),'w') as f:json.dump(report,f,indent=2)
assert len(report)==15
print('VALIDATED_15_MODELS',flush=True)
