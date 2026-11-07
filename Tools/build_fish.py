import bpy, math, os, json
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
OUT=os.path.join(ROOT,'Art/Fish'); EXPORT=os.path.join(ROOT,'Assets/_Game/Environment/Fish')
os.makedirs(OUT,exist_ok=True); os.makedirs(EXPORT,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def material(name,color):
 m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Roughness'].default_value=1; p.inputs['Metallic'].default_value=0
 return m
mats=[material(n,c) for n,c in [('Back_SlateBlue',(.115,.145,.18)),('UpperFlank',(.22,.24,.26)),('SilverFlank',(.34,.35,.35)),('PaleBelly',(.49,.46,.38)),('Fins_Charcoal',(.16,.145,.13)),('Eye_Black',(.012,.014,.013)),('GillShadow',(.21,.22,.21)),('Mouth',(.12,.105,.085))]]
verts=[]; faces=[]; slots=[]
def mesh(v,f,mi):
 offset=len(verts); verts.extend(v); faces.extend([tuple(offset+i for i in face) for face in f]); slots.extend(mi if isinstance(mi,list) else [mi]*len(f))
# Long axis X; nose left. Eight-sided cross sections, crisp longitudinal color bands.
# Length 0.64 m, substantial oval body tapering to a narrow tail wrist.
sections=[(-.30,.005,.009,.0),(-.27,.036,.039,.002),(-.225,.065,.067,.006),(-.165,.083,.083,.010),(-.07,.091,.090,.014),(.035,.086,.079,.012),(.13,.064,.058,.007),(.205,.032,.031,.004),(.24,.020,.022,.005)]
v=[]
for x,w,h,z in sections:
 for i in range(8):
  a=math.tau*i/8
  v.append((x,math.sin(a)*w,z+math.cos(a)*h))
f=[]; mi=[]
for j in range(len(sections)-1):
 for i in range(8):
  f.append(((j+1)*8+i,(j+1)*8+(i+1)%8,j*8+(i+1)%8,j*8+i))
  mi.append([0,1,2,3,3,2,1,0][i])
f.extend([tuple(range(8)),tuple(reversed(range(64,72)))]); mi.extend([2,4]); mesh(v,f,mi)
# Thin solid fins; both sides are explicitly modeled, no single-sided planes.
def fin(points,width=.002,slot=4):
 # points in X/Z with optional Y placement; thickness along Y.
 v=[(x,y-width,z) for x,y,z in points]+[(x,y+width,z) for x,y,z in points]; n=len(points)
 f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh(v,f,slot)
# Concave fork tail with a central crease and two pointed lobes.
fin([(.225,0,.026),(.305,0,.112),(.29,0,.034),(.267,0,.005),(.30,0,-.093),(.229,0,-.019)],.004)
fin([(-.075,0,.097),(.020,0,.166),(.047,0,.085)],.0028)
fin([(.075,0,-.054),(.125,0,-.106),(.145,0,-.042)],.002)
fin([(-.153,0,-.056),(-.145,0,-.11),(-.111,0,-.069)],.002)
# Swept-back pectoral fins on each side.
for side in (-1,1):
 pts=[(-.184,side*.068,-.018),(-.105,side*.093,-.054),(-.135,side*.072,-.071),(-.168,side*.068,-.059)]
 fin(pts,.002)
 # Angular gill plate, sitting flush over the flank and outlined only on its rear edge.
 mesh([(-.225,side*.049,.046),(-.183,side*.073,.061),(-.169,side*.085,.008),(-.188,side*.070,-.047),(-.225,side*.049,-.043)],[(0,1,2,3,4)] if side==1 else [(4,3,2,1,0)],2)
 mesh([(-.183,side*.074,.061),(-.169,side*.086,.008),(-.188,side*.071,-.047),(-.183,side*.072,-.049),(-.163,side*.085,.008),(-.179,side*.074,.060)],[(0,1,4,5),(1,2,3,4)],6)
# Small wedge-shaped mouth, separate upper/lower lips are suggested by the dark slit.
mesh([(-.302,-.009,.001),(-.279,-.025,-.007),(-.269,0,-.012),(-.279,.025,-.007),(-.302,.009,.001)],[(0,1,2,3,4)],7)
me=bpy.data.meshes.new('RiverFish_Mesh'); me.from_pydata(verts,[],faces); me.update()
for m in mats: me.materials.append(m)
for p,idx in zip(me.polygons,slots): p.material_index=idx
fish=bpy.data.objects.new('RiverFish',me); bpy.context.collection.objects.link(fish)
eyes=[]
for side in (-1,1):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.0068,location=(-.245,side*.048,.026))
 eye=bpy.context.object; eye.name='Eye'; eye.scale=(1,.45,1); eye.data.materials.append(mats[5]); eyes.append(eye)
bpy.ops.object.select_all(action='DESELECT')
for o in [fish]+eyes:o.select_set(True)
bpy.context.view_layer.objects.active=fish; bpy.ops.object.join()
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
assert not fish.data.validate()
bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT,'RiverFish.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'RiverFish.blend'))
with open(os.path.join(OUT,'mesh_info.json'),'w') as f:json.dump({'vertices':len(fish.data.vertices),'triangles':sum(len(p.vertices)-2 for p in fish.data.polygons),'length_m':float(fish.dimensions.x),'textures':False},f,indent=2)
# Presentation is a separate file, keeping the deliverable .blend limited to one fish.
ground=material('Studio',(.40,.38,.34))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.115)); bpy.context.object.data.materials.append(ground)
bpy.ops.object.camera_add(location=(-.48,-1.1,.31)); cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,.02))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=.79; bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-.3,-.5,.9)); bpy.context.object.data.energy=32; bpy.context.object.data.size=.85
bpy.ops.object.light_add(type='AREA',location=(.4,.5,.5)); bpy.context.object.data.energy=15; bpy.context.object.data.size=.7
sc=bpy.context.scene; sc.world=bpy.data.worlds.new('StudioWorld'); sc.world.color=(.28,.28,.28); sc.render.engine='CYCLES'; sc.cycles.samples=48; sc.render.resolution_x=1600; sc.render.resolution_y=1000; sc.render.resolution_percentage=100
sc.render.filepath=os.path.join(OUT,'RiverFish_Preview.png'); bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'RiverFish_PreviewScene.blend')); bpy.ops.render.render(write_still=True)
print('FISH_COMPLETE',flush=True)
