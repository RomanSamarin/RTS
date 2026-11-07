import bpy, math, random, os, json
from mathutils import Vector
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT = os.path.join(ROOT, 'Assets/_Game/Environment/Trees')
ART = os.path.join(ROOT, 'Art/TerrainTrees')
os.makedirs(OUT, exist_ok=True)
os.makedirs(ART, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
random.seed(47)
def material(name, color):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Roughness'].default_value=1; p.inputs['Metallic'].default_value=0
    return m
bark=material('Bark',(0.22,.12,.065)); white=material('BirchBark',(.72,.72,.61))
leaves=[material('Leaves'+str(i),c) for i,c in enumerate([(.26,.36,.09),(.36,.44,.13),(.18,.29,.07),(.31,.40,.10)])]
pine=[material('Pine'+str(i),c) for i,c in enumerate([(.10,.23,.09),(.17,.30,.12),(.22,.34,.13)])]
parts=[]
def segment(a,b,r1,r2,mat,verts=7):
    d=Vector(b)-Vector(a)
    bpy.ops.mesh.primitive_cone_add(vertices=verts,radius1=r1,radius2=r2,depth=d.length,location=(Vector(a)+Vector(b))/2)
    o=bpy.context.object; o.rotation_euler=d.to_track_quat('Z','Y').to_euler(); o.data.materials.append(mat); parts.append(o); return o
def crown(pos,scale,mat):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=pos)
    o=bpy.context.object
    for v in o.data.vertices:
        v.co *= random.uniform(.88,1.10)
    o.scale=scale; o.rotation_euler=(random.random()*.2,random.random()*.2,random.random()*6.28); o.data.materials.append(mat); parts.append(o)

def limb(points,radii,mat):
    vertices=[]; faces=[]; n=7
    for j,p in enumerate(points):
        tangent=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])
        q=tangent.to_track_quat('Z','Y')
        for i in range(n):
            a=i*math.tau/n
            # Keep ring orientation consistent to avoid twisting/pinched trunks.
            vertices.append(Vector(p)+Vector((math.cos(a)*radii[j],math.sin(a)*radii[j],0)))
    faces.append(tuple(reversed(range(n))))
    for j in range(len(points)-1):
        for i in range(n):
            k=j*n+i; nxt=j*n+(i+1)%n
            faces.append((k,nxt,nxt+n,k+n))
    faces.append(tuple(range((len(points)-1)*n,len(points)*n)))
    mesh=bpy.data.meshes.new('TaperedBranch'); mesh.from_pydata(vertices,[],faces); mesh.materials.append(mat); mesh.update()
    o=bpy.data.objects.new('Branch',mesh); bpy.context.collection.objects.link(o); parts.append(o)

def needles(z,r,height,phase,mat):
    # Irregular overlapping skirts with drooping tips and a narrow upper collar.
    n=12; vertices=[]; faces=[]
    variation=[random.uniform(.85,1.15) for _ in range(n)]
    for ring in range(3):
        for i in range(n):
            a=phase+i*math.tau/n
            rr=r*variation[i]*[1,.63,.07][ring]
            zz=z+[random.uniform(-.12,.07)*height,.38*height,height][ring]
            vertices.append((math.cos(a)*rr,math.sin(a)*rr,zz))
    for ring in range(2):
        for i in range(n):
            a=ring*n+i; b=ring*n+(i+1)%n
            faces.extend([(a,b,b+n),(a,b+n,a+n)])
    faces.extend([tuple(reversed(range(n))),tuple(range(2*n,3*n))])
    me=bpy.data.meshes.new('NeedleTier'); me.from_pydata(vertices,[],faces); me.materials.append(mat); me.update()
    o=bpy.data.objects.new('Needles',me); bpy.context.collection.objects.link(o); parts.append(o)
def tree(name,kind,h):
    global parts
    print('BUILD',name,flush=True)
    parts=[]
    thin=kind in ('birch','pine')
    radius=h*(.025 if thin else .043)
    limb([(0,0,0),(-h*.013,0,h*.13),(h*.013,h*.006,h*.37),(-h*.018,h*.018,h*.63),(h*.013,0,h*.93)],
         [radius*1.35,radius,radius*.76,radius*.44,.008],white if kind=='birch' else bark)
    if kind=='pine':
        for j in range(6):
            z=h*(.20+j*.126); r=h*(.24-j*.037)*random.uniform(.92,1.08)
            needles(z,r,h*.21,j*.71,pine[min(2,j//2)])
    elif kind=='dead':
        for i in range(6):
            a=i*2.4; z=h*(.28+i*.085); reach=h*random.uniform(.15,.25)
            end=(math.cos(a)*reach,math.sin(a)*reach,z+h*.23)
            mid=(end[0]*.65,end[1]*.65,z+h*.07)
            tip=(end[0]*1.12,end[1]*1.12,end[2]+h*.09)
            limb([(0,0,z),mid,end,tip],[radius*.52,radius*.35,radius*.15,.005],bark)
            fork=(mid[0]+h*.08*math.cos(a+.7),mid[1]+h*.08*math.sin(a+.7),mid[2]+h*.16)
            limb([mid,fork],[radius*.2,.005],bark)
    else:
        count=5 if kind=='oak' else 6
        for i in range(count):
            a=i*2.4+random.uniform(-.2,.2)
            z=h*(random.uniform(.58,.72) if kind=='oak' else .48+i*.065)
            r=h*(random.uniform(.17,.23) if kind=='oak' else random.uniform(.09,.14))
            p=(math.cos(a)*r,math.sin(a)*r,z)
            mid=(p[0]*.55,p[1]*.55,z-h*.13)
            limb([(0,0,z-h*.28),mid,p],[radius*.60,radius*.37,.018],white if kind=='birch' else bark)
            s=h*(random.uniform(.20,.245) if kind=='oak' else .12)
            crown(p,(s,s*.92,s*(.77 if kind=='oak' else 1.35)),leaves[0 if i%3 else 3])
        crown((h*.025,0,h*.79 if kind=='oak' else h*.89),(h*.24 if kind=='oak' else h*.12,h*.23 if kind=='oak' else h*.13,h*.19),leaves[3])
        if kind=='birch':
            for i in range(8):
                z=.45+i*h*.075
                # Small dark bark patches, not rings floating around the trunk.
                a=i*2.4; r=radius*(1-.5*z/h)
                crown((math.cos(a)*r,math.sin(a)*r,z),(.018,.055,.025),bark)
    print('JOIN',name,len(parts),flush=True)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    vertices=[]; faces=[]; assignments=[]; mats=[]
    for part in parts:
        offset=len(vertices)
        vertices.extend([part.matrix_world@v.co for v in part.data.vertices])
        for poly in part.data.polygons:
            faces.append(tuple(offset+i for i in poly.vertices))
            mat=part.data.materials[poly.material_index]
            if mat not in mats: mats.append(mat)
            assignments.append(mats.index(mat))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    for mat in mats: mesh.materials.append(mat)
    for poly,idx in zip(mesh.polygons,assignments): poly.material_index=idx
    for part in parts: bpy.data.objects.remove(part,do_unlink=True)
    o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o)
    o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
    print('EXPORTED',name,flush=True)
    return o
spec=[('Oak_Large','oak',6),('Oak_Medium','oak',4.7),('Oak_Small','oak',3.3),('Pine_Large','pine',7),('Pine_Medium','pine',5.5),('Pine_Small','pine',4),('Birch','birch',6),('DeadTree','dead',5)]
models=[tree(*s) for s in spec]
# Each source file contains exactly one tree at the origin, including its materials.
individual=os.path.join(ART,'Individual'); os.makedirs(individual,exist_ok=True)
for model in models:
    print('SAVE INDIVIDUAL',model.name,flush=True)
    for other in models:
        if other != model: bpy.context.scene.collection.children[0].objects.unlink(other)
    bpy.context.view_layer.objects.active=model
    model.select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(individual,model.name+'.blend'),copy=True)
    for other in models:
        if other != model: bpy.context.scene.collection.children[0].objects.link(other)
stats={o.name: {'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)} for o in models}
with open(os.path.join(ART,'mesh_stats.json'),'w') as f: json.dump(stats,f,indent=2)
for i,o in enumerate(models): o.location=(i*5,0,0)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART,'Trees_Library.blend'))
for o in models: o.hide_render=True; o.hide_set(True)
ground=material('Ground',(.29,.36,.12)); dirt=material('Path',(.48,.34,.18))
segment((0,0,-.8),(0,0,-.08),15,15,ground,64)
# A winding polygon path across the clearing.
vs=[]
for j in range(21):
    y=-14+j*1.4; x=2*math.sin(y*.22)
    vs.extend([(x-.9,y,.01),(x+.9,y,.01)])
me=bpy.data.meshes.new('Path'); me.from_pydata(vs,[],[(i,i+1,i+3,i+2) for i in range(0,40,2)]); me.materials.append(dirt)
ob=bpy.data.objects.new('Path',me); bpy.context.collection.objects.link(ob)
for i in range(70):
    x=random.uniform(-12,12); y=random.uniform(-12,12)
    if x*x+y*y>155 or abs(x-2*math.sin(y*.22))<2.5: continue
    src=models[random.choices(range(8),[4,4,3,4,4,3,2,1])[0]]
    o=src.copy(); o.data=src.data; bpy.context.collection.objects.link(o); o.hide_render=False; o.hide_set(False); o.location=(x,y,0); o.rotation_euler.z=random.random()*math.tau
    s=random.uniform(.8,1.12); o.scale=(s,s,s)
bpy.ops.object.camera_add(location=(27,-34,30)); cam=bpy.context.object; cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=37; bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(-10,-8,20)); bpy.context.object.data.energy=2300; bpy.context.object.data.shape='DISK'; bpy.context.object.data.size=12
bpy.ops.object.light_add(type='SUN',location=(0,0,10)); bpy.context.object.rotation_euler=(.45,-.5,-.4); bpy.context.object.data.energy=2
sc=bpy.context.scene; sc.world.color=(.32,.32,.32); sc.render.engine='CYCLES'; sc.cycles.samples=32
sc.render.resolution_x=1600; sc.render.resolution_y=1200; sc.render.resolution_percentage=100
sc.render.image_settings.file_format='PNG'; sc.render.filepath=os.path.join(ART,'Forest_Preview.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART,'Forest_Demo.blend'))
bpy.ops.render.render(write_still=True)
print('TREE_KIT_COMPLETE',stats)
