import bpy,json,os
bpy.ops.wm.open_mainfile(filepath='C:/Users/Роман/Downloads/BlackSmith.blend')
rows=[]
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  rows.append({'name':o.name,'verts':len(o.data.vertices),'polys':len(o.data.polygons),'pos':list(o.location),'dimensions':list(o.dimensions),'materials':[m.name if m else None for m in o.data.materials]})
os.makedirs('Art/BlackSmith',exist_ok=True)
with open('Art/BlackSmith/inventory.json','w') as f: json.dump(rows,f,indent=2)
for r in rows:
 n=int(r['name'].split('.')[-1]) if '.' in r['name'] else 0
 if 56<=n<=120: print(r['name'],r['verts'],[round(x,2) for x in r['pos']],[round(x,2) for x in r['dimensions']],flush=True)
print('CAMERAS',[(o.name,list(o.location)) for o in bpy.context.scene.objects if o.type=='CAMERA'],flush=True)
