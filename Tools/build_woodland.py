"""Solid-color low-poly woodland kit and staged diorama. Blender background entry point."""
import bpy, bmesh, random, math, os, json
from mathutils import Vector
R=random.Random(951)
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
ART=os.path.join(ROOT,'Art/StorybookWoodland'); FBX=os.path.join(ROOT,'Assets/_Game/Environment/StorybookWoodland')
IND=os.path.join(ART,'Individual')
for p in (ART,FBX,IND):os.makedirs(p,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(n,c):
 m=bpy.data.materials.new(n); m.diffuse_color=(*c,1); m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*c,1); bs.inputs['Roughness'].default_value=1; bs.inputs['Metallic'].default_value=0
 return m
bark=mat('Woodland_Bark',(.29,.145,.058)); barklight=mat('Woodland_WoodCut',(.65,.38,.14)); white=mat('Woodland_Birch',(.78,.74,.57)); dark=mat('Woodland_BirchMarks',(.17,.15,.115))
leaf=[mat('Woodland_Leaves_'+str(i),c) for i,c in enumerate([(.29,.40,.055),(.37,.47,.075),(.43,.52,.095),(.23,.34,.045),(.34,.43,.065)])]
needle=[mat('Woodland_Needles_'+str(i),c) for i,c in enumerate([(.16,.29,.065),(.23,.35,.075),(.28,.39,.09)])]
berry=mat('Woodland_Berries',(.66,.10,.065)); cream=mat('Woodland_Petals',(.94,.79,.43)); pollen=mat('Woodland_Pollen',(.83,.46,.075))
rockm=[mat('Woodland_Stone_'+str(i),c) for i,c in enumerate([(.34,.32,.29),(.42,.39,.35),(.47,.44,.39)])]
parts=[]; models={}; stats={}
def mesh(n,v,f,m):
 me=bpy.data.meshes.new(n); me.from_pydata(v,[],f); me.update(); me.materials.append(m)
 bm=bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(me); bm.free()
 o=bpy.data.objects.new(n,me); bpy.context.collection.objects.link(o); parts.append(o); return o
def tube(points,radii,m,n=7):
 # Parallel-transport frames prevent corkscrewed or pinched branch rings.
 v=[]; f=[]; prev=None
 for j,p in enumerate(points):
  t=(Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])).normalized()
  if prev is None: u=t.cross(Vector((0,1,0))).normalized()
  else: u=(prev-t*prev.dot(t)).normalized()
  w=t.cross(u).normalized(); prev=u
  for i in range(n):
   a=i*math.tau/n; v.append(Vector(p)+radii[j]*(math.cos(a)*u+math.sin(a)*w))
 for j in range(len(points)-1):
  for i in range(n):a=j*n+i; b=j*n+(i+1)%n; f.append((a,b,b+n,a+n))
 f.extend([tuple(reversed(range(n))),tuple(range((len(points)-1)*n,len(points)*n))]); return mesh('Branch',v,f,m)
def blob(p,s,m,sub=2):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=p)
 o=bpy.context.object; o.name='FacetedFoliage'; o.scale=s; o.rotation_euler.z=R.random()*math.tau
 for v in o.data.vertices:v.co*=R.uniform(.92,1.075)
 o.data.materials.append(m); parts.append(o); return o
def blade(base,tip,width,m):
 a=Vector(base); b=Vector(tip); d=b-a; side=Vector((-d.y,d.x,0))
 if side.length<.001:side=Vector((1,0,0))
 side.normalize(); mid=a+d*.48; ridge=mid+Vector((0,0,width*.25)); low=mid-Vector((0,0,width*.13))
 return mesh('LeafBlade',[a,mid+side*width,ridge,b,mid-side*width,low],[(0,1,2),(1,3,2),(3,4,2),(4,0,2),(0,5,1),(1,5,3),(3,5,4),(4,5,0)],m)
def finish(name):
 global parts
 bpy.context.view_layer.update(); v=[]; f=[]; ids=[]; mats=[]
 for p in parts:
  off=len(v); v.extend([p.matrix_world@x.co for x in p.data.vertices])
  for face in p.data.polygons:
   f.append(tuple(off+i for i in face.vertices)); m=p.data.materials[face.material_index]
   if m not in mats:mats.append(m)
   ids.append(mats.index(m))
 for p in parts:bpy.data.objects.remove(p,do_unlink=True)
 parts=[]; o=mesh(name,v,f,mats[0]); parts=[]
 o.data.materials.clear()
 for m in mats:o.data.materials.append(m)
 for face,i in zip(o.data.polygons,ids):face.material_index=i
 assert not o.data.validate(),name
 bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
 bpy.ops.export_scene.fbx(filepath=os.path.join(FBX,name+'.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
 bpy.ops.wm.save_as_mainfile(filepath=os.path.join(IND,name+'.blend'),copy=True)
 stats[name]={'vertices':len(v),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'materials':len(mats)}
 models[name]=o; bpy.context.collection.objects.unlink(o)
 print('ASSET_SAVED',name,flush=True); return o
def roots(h,r,m):
 for i in range(5):
  a=i*math.tau/5+.2
  tube([(math.cos(a)*r*2.8,math.sin(a)*r*2.8,.015),(math.cos(a)*r*1.4,math.sin(a)*r*1.4,h*.05),(0,0,h*.15)],[.012,r*.40,r*.65],m,5)
def tree(name,kind,h):
 r=h*(.078 if kind=='oak' else .045 if kind=='dead' else .03)
 stem=[(0,0,0),(-h*.023,0,h*.15),(h*.018,h*.007,h*.37),(-h*.028,0,h*.62),(h*.016,.02,h*.93)]
 tube(stem,[r*1.25,r,r*.77,r*.48,.016],white if kind=='birch' else bark); roots(h,r,white if kind=='birch' else bark)
 if kind in ('oak','young','birch','dead'):
  count=7 if kind=='oak' else 5 if kind=='young' else 7
  for i in range(count):
   a=i*2.4+.35; z=h*((.43+(i%3)*.115) if kind=='oak' else .38+i*.065)
   reach=h*(R.uniform(.28,.41) if kind=='oak' else R.uniform(.22,.29) if kind=='young' else R.uniform(.13,.22))
   end=Vector((math.cos(a)*reach,math.sin(a)*reach,z+h*.18)); mid=Vector((end.x*.55,end.y*.55,z+h*.035))
   tube([(0,0,z-h*.12),mid,end],[r*.65,r*.40,r*.13],white if kind=='birch' else bark)
   if kind=='dead':
    tip=end+Vector((math.cos(a+.2)*h*.04,math.sin(a+.2)*h*.04,h*.14)); tube([end,tip],[r*.13,.009],bark)
    tube([mid,mid+Vector((math.cos(a+.8)*h*.15,math.sin(a+.8)*h*.15,h*.16))],[r*.23,.008],bark)
   else:
    s=h*(.20 if kind=='oak' else .16 if kind=='young' else .115)
    blob(end,(s,s*.89,s*(.66 if kind=='oak' else .75 if kind=='young' else 1.12)),leaf[i%5])
    if kind=='oak':blob(end+Vector((s*.55,-s*.26,-s*.15)),(s*.68,s*.64,s*.49),leaf[(i+1)%5])
  if kind!='dead':blob((0,0,h*.85),(h*.22 if kind=='oak' else h*.15,h*.19 if kind=='oak' else h*.13,h*.13),leaf[2])
  if kind=='birch':
   for i in range(9):
    z=.3+i*h*.067; rr=r*(1-z/h*.65); a=i*2.4
    blob((math.cos(a)*rr,math.sin(a)*rr,z),(.065,.018,.026),dark,1)
 else:
  # Branch-like skirts with an alternating jagged silhouette, rather than stacked cones.
  tiers=5 if kind=='pine' else 4
  for j in range(tiers):
   z=h*(.22+j*.145); rad=h*(.27-j*.045) if kind=='pine' else h*(.34-j*.068)
   n=12; v=[]
   for ring in range(3):
    for i in range(n):
     a=i*math.tau/n+j*.57; rr=rad*[1,.64,.045][ring]*(R.uniform(.9,1.1))
     zz=z+[(-.045 if i%2==0 else .035)*h,.055*h,.24*h][ring]
     v.append((math.cos(a)*rr,math.sin(a)*rr,zz))
   f=[]
   for ring in range(2):
    for i in range(n):a=ring*n+i; b=ring*n+(i+1)%n; f.extend([(a,b,b+n),(a,b+n,a+n)])
   f.extend([tuple(reversed(range(n))),tuple(range(24,36))]); mesh('Needles',v,f,needle[j%3])
 finish(name)
for spec in [('Oak_Ancient','oak',5.6),('Oak_Young','young',4.1),('Pine_Tall','pine',6.4),('Spruce_Short','spruce',3.2),('Birch','birch',5.1),('DeadTree','dead',4.5)]:tree(*spec)
def bush(name,kind):
 for i in range(11 if kind!='round' else 16):
  a=i*2.4; rad=R.uniform(.15,.60); z=R.uniform(.28,.57)
  p=Vector((math.cos(a)*rad,math.sin(a)*rad,z))
  if kind=='round':blob(p*.73,(.28,.26,.29),leaf[i%5],1)
  else:
   blob(p,(.32,.30,.28),leaf[i%5],1)
   for k in range(3):
    aa=a+k*2; blade(p,p+Vector((math.cos(aa)*.43,math.sin(aa)*.43,.04)),.16,leaf[(i+k)%5])
  if kind=='berry' and i%2==0:
   for k in range(3):blob(p+Vector((-.12+k*.09,-.24,.19)),(.065,.065,.065),berry,1)
 finish(name)
for n,k in [('Bush_Broad','broad'),('Bush_Round','round'),('Bush_Berry','berry')]:bush(n,k)
def grass(name,h,count):
 for i in range(count):
  a=i*2.4; r=R.uniform(.02,.18)*h
  base=Vector((math.cos(a)*r,math.sin(a)*r,0)); height=h*R.uniform(.58,1)
  blade(base,base+Vector((math.cos(a)*height*.46,math.sin(a)*height*.46,height)),h*.065,leaf[i%5])
 finish(name)
for n,h,c in [('Grass_Tall',.85,9),('Grass_Medium',.5,7),('Grass_Small',.28,5)]:grass(n,h,c)
for i in range(7):
 a=i*2.4; p=Vector((math.cos(a)*.23,math.sin(a)*.23,0)); h=R.uniform(.95,1.5)
 tube([p,p+Vector((.07,.02,h))],[.016,.01],leaf[0],5)
 if i<4:tube([p+Vector((.07,.02,h*.83)),p+Vector((.07,.02,h*1.02))],[.046,.041],bark,7)
 blade(p,p+Vector((math.cos(a)*.45,math.sin(a)*.45,h*.77)),.07,leaf[1])
finish('Reeds')
for i in range(5):
 a=i*2.4; end=Vector((math.cos(a)*.23,math.sin(a)*.23,R.uniform(.3,.65)))
 tube([(0,0,0),end],[.012,.009],leaf[0],5)
 for k in range(5):
  aa=k*math.tau/5; blob(end+Vector((math.cos(aa)*.085,math.sin(aa)*.085,0)),(.072,.05,.02),cream,1)
 blob(end+Vector((0,0,.02)),(.045,.045,.025),pollen,1)
 blade((0,0,.04),(end.x*1.4,end.y*1.4,end.z*.6),.06,leaf[1])
finish('Wildflowers')
for i in range(7):
 a=i*math.tau/7; length=R.uniform(.55,.85); d=Vector((math.cos(a),math.sin(a),0)); cross=Vector((-d.y,d.x,0))
 for j in range(1,7):
  t=j/7; p=d*length*t+Vector((0,0,.42*math.sin(t*math.pi*.8)))
  for side in (-1,1):blade(p,p+cross*side*(1-t)*.22+d*.10,.055*(1-t)+.014,leaf[(i+j)%5])
finish('Fern')
with open(os.path.join(ART,'manifest.json'),'w') as f:json.dump(stats,f,indent=2)
# Library file, with named objects arranged for convenient inspection.
for i,o in enumerate(models.values()):bpy.context.collection.objects.link(o); o.location=((i%5)*7,(i//5)*9,0)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART,'Woodland_Library.blend'))
for o in models.values():bpy.context.collection.objects.unlink(o); o.location=(0,0,0)
def place(name,x,y,s=1,angle=None,z=0):
 src=models[name]; o=bpy.data.objects.new(name,src.data); bpy.context.collection.objects.link(o); o.location=(x,y,z); o.scale=(s,s,s); o.rotation_euler.z=R.random()*math.tau if angle is None else angle; return o
ground=mat('Diorama_Grass',(.32,.42,.085)); soil=mat('Diorama_Soil',(.29,.19,.095)); path=mat('Diorama_Path',(.65,.44,.21)); water=mat('Diorama_Water',(.075,.33,.43)); bank=mat('Diorama_Sand',(.57,.46,.27))
# Rounded polygon island with a solid cut-earth edge.
border=[(-8,-6),(-6,-7),(6,-7),(8,-5),(8,5),(6,7),(-6,7),(-8,5)]
v=[(x,y,z) for z in (-.7,0) for x,y in border]; n=len(border)
base=mesh('Island',v,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],soil)
base.data.materials.append(ground); base.data.polygons[1].material_index=1
def ribbon(name,fn,width,z,ma):
 v=[]
 for j in range(31):
  y=-6.9+j*13.8/30; x=fn(y)
  v.extend([(x-width,y,z),(x+width,y,z)])
 return mesh(name,v,[(i,i+1,i+3,i+2) for i in range(0,60,2)],ma)
river=lambda y:4.4+.65*math.sin(y*.49)
trail=lambda y:-.65+1.2*math.sin(y*.34)
ribbon('SandyRiverbank',river,1.15,.018,bank); ribbon('Creek',river,.8,.035,water); ribbon('WindingPath',trail,.64,.025,path)
# Hand-composed silhouettes: broad oak anchors the left, contrasting conifers behind.
for row in [('Oak_Ancient',-4.2,1.1,1.05,.4),('Oak_Young',-5.3,-3.0,.82,1),('Pine_Tall',1.5,4.4,.94,.8),('Spruce_Short',-5.8,4.1,1.05,.3),('Birch',.1,5.1,.88,1.6),('Birch',6.5,3.9,.8,.4),('DeadTree',-6,5.8,.72,1.1),('Oak_Young',-2,5.9,.85,2)]:place(*row)
for x,y in [(-2.9,-1.2),(-5.5,-4.6),(1.6,1.5),(-2.4,3.2),(6.5,1.6),(.9,-3.8)]:place('Bush_Berry' if x<0 else 'Bush_Broad',x,y,R.uniform(.7,1.0))
for i in range(30):
 x=R.uniform(-7.4,7.2); y=R.uniform(-6.3,6.2)
 if abs(x-river(y))<1.2 or abs(x-trail(y))<.95:continue
 place(R.choice(['Bush_Round','Fern']),x,y,R.uniform(.45,.8))
# Faceted rocks and small terraces complement the vegetation.
for x,y,s in [(2.5,3.2,1.1),(6.6,-1.8,.65),(-2.5,-2.3,.55),(-6.4,.3,.65),(2.6,-4.7,.4),(5.6,-4.2,.4)]:
 blob((x,y,s*.48),(s,s*.75,s*.63),R.choice(rockm),1)
for i in range(23):
 y=R.uniform(-6.3,6.2); side=R.choice([-1,1]); x=river(y)+side*R.uniform(.80,1.12); s=R.uniform(.15,.36)
 blob((x,y,s*.34),(s,s*.78,s*.55),R.choice(rockm),1)
 if i%4==0:place('Reeds',x+side*.15,y,.65)
for i in range(250):
 x=R.uniform(-7.5,7.5); y=R.uniform(-6.5,6.5)
 if abs(x-river(y))<1.13 or abs(x-trail(y))<.78:continue
 place(R.choices(['Grass_Tall','Grass_Medium','Grass_Small','Wildflowers'],[1,4,5,2])[0],x,y,R.uniform(.55,1.0))
# Sparse stones along the path and pale stream ripples.
foam=mat('StreamHighlights',(.37,.60,.60))
for i in range(14):
 y=R.uniform(-6.4,6.4); x=river(y)+R.uniform(-.55,.55)
 mesh('WaterFacet',[(x-.19,y,.04),(x+.23,y+.03,.04),(x+.12,y+.08,.04),(x-.21,y+.05,.04)],[(0,1,2,3)],foam)
studio=mat('Backdrop',(.71,.65,.52)); bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.73)); bpy.context.object.data.materials.append(studio)
bpy.ops.object.camera_add(location=(20,-30,23)); cam=bpy.context.object; cam.rotation_euler=(Vector((0,.4,1.4))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=24; bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-6,-10,16)); bpy.context.object.data.energy=2200; bpy.context.object.data.size=9; bpy.context.object.data.color=(1,.89,.71)
bpy.ops.object.light_add(type='AREA',location=(8,6,12)); bpy.context.object.data.energy=1300; bpy.context.object.data.size=10
bpy.ops.object.light_add(type='SUN'); bpy.context.object.rotation_euler=(.4,-.6,-.4); bpy.context.object.data.energy=1.2; bpy.context.object.data.angle=.15
sc=bpy.context.scene; sc.world=bpy.data.worlds.new('WoodlandSky'); sc.world.use_nodes=True; sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.48,.59,.75,1); sc.world.node_tree.nodes['Background'].inputs[1].default_value=.35
sc.render.engine='CYCLES'; sc.cycles.samples=64; sc.cycles.use_denoising=True; sc.render.resolution_x=1920; sc.render.resolution_y=1600; sc.render.resolution_percentage=100
sc.render.filepath=os.path.join(ART,'Woodland_Preview.png'); bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART,'Woodland_Diorama.blend')); bpy.ops.render.render(write_still=True)
print('WOODLAND_COMPLETE',len(models),flush=True)
