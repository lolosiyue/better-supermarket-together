using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BetterSupermarketTogether;

// Local visual overlay: bowl in the left hand, three small spoonfuls, then lower the hands.
internal sealed class CompanionRicePose : IDisposable
{
    private GameObject _props;
    private Transform _bowl,_spoon;
    private readonly List<UnityEngine.Object> _owned=new List<UnityEngine.Object>();
    private readonly CompanionPoseBridge _bridge;
    private readonly Dictionary<string,Transform> _human;
    private readonly Transform _root;
    private readonly float _height;
    internal CompanionRicePose(CompanionPoseBridge bridge,Dictionary<string,Transform> human,Transform root,int product)
    {
        _bridge=bridge;_human=human;_root=root;
        _height=Vector3.Distance(human["head"].position,(human["leftFoot"].position+human["rightFoot"].position)*.5f);
        try
        {
            _props=new GameObject("DeepSeek rice break");_props.SetActive(false);
            var shader=GraphicsSettings.currentRenderPipeline!=null?Shader.Find("Universal Render Pipeline/Unlit"):Shader.Find("Unlit/Color");
            if(shader==null)throw new InvalidOperationException("No rice prop shader");
            var ceramic=Material(shader,product==33?new Color(.28f,.58f,.72f):product==34?new Color(.66f,.77f,.56f):new Color(.93f,.88f,.77f));
            var rice=Material(shader,new Color(.98f,.97f,.90f));
            var wood=Material(shader,new Color(.48f,.28f,.13f));
            _bowl=new GameObject("Rice bowl").transform;_bowl.SetParent(_props.transform,false);
            var mesh=BowlMesh();_owned.Add(mesh);
            _bowl.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            _bowl.gameObject.AddComponent<MeshRenderer>().sharedMaterial=ceramic;
            Primitive("White rice",PrimitiveType.Sphere,_bowl,new Vector3(0,.018f,0),new Vector3(.164f,.055f,.164f),rice);
            _spoon=new GameObject("Wooden spoon").transform;_spoon.SetParent(_props.transform,false);
            Primitive("Handle",PrimitiveType.Cube,_spoon,new Vector3(0,0,.045f),new Vector3(.009f,.007f,.09f),wood);
            Primitive("Spoon",PrimitiveType.Sphere,_spoon,new Vector3(0,0,.102f),new Vector3(.027f,.008f,.038f),wood);
            Primitive("Bite",PrimitiveType.Sphere,_spoon,new Vector3(0,.006f,.102f),new Vector3(.021f,.012f,.025f),rice);
            _bowl.localScale=_spoon.localScale=Vector3.one*_height;
            _props.SetActive(true);
        }
        catch { Dispose();throw; }
    }
    internal void Apply(float seconds)
    {
        if(_props==null)return;
        float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(seconds/.7f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((8-seconds)/.7f));
        Vector3 up=_root.up,forward=_root.forward,right=_root.right;
        Transform head=_human["head"],chest=_human.ContainsKey("chest")?_human["chest"]:_human["spine"];
        Vector3 bowl=chest.position+forward*(_height*.22f)-up*(_height*.12f)-right*(_height*.07f);
        float phase=Mathf.Clamp01((seconds-.8f)/6.3f)*3;
        float lift=.5f-.5f*Mathf.Cos((phase-Mathf.Floor(phase))*2*Mathf.PI);
        Vector3 mouth=head.position+up*(_height*.03f)+forward*(_height*.08f);
        Vector3 rightGoal=Vector3.Lerp(bowl+up*(_height*.08f)+forward*(_height*.11f),mouth+forward*(_height*.11f),lift);
        _bridge.PoseHand(false,bowl-up*(_height*.052f),weight);
        _bridge.PoseHand(true,rightGoal,weight);
        // Follow the solved hand rather than an unreachable desired target.
        _bowl.position=_human["leftHand"].position+up*(_height*.052f);
        _bowl.rotation=Quaternion.LookRotation(forward,up);
        _spoon.position=_human["rightHand"].position;
        _spoon.rotation=Quaternion.LookRotation(-forward,up);
        head.rotation=Quaternion.AngleAxis(5*weight*(1-lift),right)*head.rotation;
    }
    private Material Material(Shader shader,Color color)
    {
        var material=new Material(shader);_owned.Add(material);
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Color"))material.SetColor("_Color",color);
        if(material.HasProperty("_Cull"))material.SetInt("_Cull",(int)CullMode.Off);
        return material;
    }
    private static void Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent,false);
        obj.transform.localPosition=position;obj.transform.localScale=scale;
        var collider=obj.GetComponent<Collider>();if(collider!=null) {collider.enabled=false;UnityEngine.Object.Destroy(collider);}
        obj.GetComponent<Renderer>().sharedMaterial=material;
    }
    private static Mesh BowlMesh()
    {
        // Closed profile: foot, outer wall, rounded lip and inner wall.
        var profile=new[] {new Vector2(0,-.05f),new Vector2(.035f,-.05f),new Vector2(.07f,-.025f),new Vector2(.09f,.018f),
            new Vector2(.087f,.025f),new Vector2(.08f,.018f),new Vector2(.055f,-.025f),new Vector2(0,-.035f)};
        const int sides=24;var vertices=new Vector3[profile.Length*sides];var triangles=new int[(profile.Length-1)*sides*6];int t=0;
        for(int r=0;r<profile.Length;r++)for(int s=0;s<sides;s++)
        {
            float angle=s*Mathf.PI*2/sides;vertices[r*sides+s]=new Vector3(Mathf.Cos(angle)*profile[r].x,profile[r].y,Mathf.Sin(angle)*profile[r].x);
            if(r==0)continue;
            int a=(r-1)*sides+s,b=(r-1)*sides+(s+1)%sides,c=r*sides+s,d=r*sides+(s+1)%sides;
            triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;
        }
        var mesh=new Mesh {name="Rice bowl",vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    public void Dispose()
    {
        if(_props!=null) { _props.SetActive(false);UnityEngine.Object.Destroy(_props);_props=null; }
        foreach(var obj in _owned)if(obj!=null)UnityEngine.Object.Destroy(obj);_owned.Clear();
    }
}
