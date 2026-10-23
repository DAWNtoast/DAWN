using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

public static class BattleIconMeshExport
{
    private static readonly List<string> Icons=new List<string>();
    private static string Q(string s)=>"\""+s.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
    private static string F(float x)=>x.ToString("0.######",CultureInfo.InvariantCulture);
    private static int C(float x)=>(int)Math.Round(Math.Max(0,Math.Min(1,x))*255f,MidpointRounding.AwayFromZero);
    private static void Export(string key,string group,string kind,string id,bool debuff,MaskableGraphic icon,int width=128,int height=128)
    {
        icon.rectTransform.rect=new Rect(-width*.5f,-height*.5f,width,height);
        var mesh=new VertexHelper();
        icon.GetType().GetMethod("OnPopulateMesh",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(icon,new object[]{mesh});
        if(mesh.vertices.Count==0 || mesh.triangles.Count==0) throw new Exception("Empty icon: "+key);
        var s=new StringBuilder("{\"key\":").Append(Q(key)).Append(",\"group\":").Append(Q(group)).Append(",\"kind\":").Append(Q(kind));
        s.Append(",\"statusId\":").Append(Q(id)).Append(",\"isDebuff\":").Append(debuff?"true":"false").Append(",\"width\":").Append(width).Append(",\"height\":").Append(height);
        s.Append(",\"vertices\":[");
        for(int i=0;i<mesh.vertices.Count;i++)
        {
            if(i>0)s.Append(',');var v=mesh.vertices[i];
            s.Append('[').Append(F(v.position.x+width*.5f)).Append(',').Append(F(height*.5f-v.position.y)).Append(',').Append(C(v.color.r)).Append(',').Append(C(v.color.g)).Append(',').Append(C(v.color.b)).Append(',').Append(C(v.color.a)).Append(']');
        }
        s.Append("],\"triangles\":[");
        for(int i=0;i<mesh.triangles.Count;i++) {if(i>0)s.Append(',');var t=mesh.triangles[i];s.Append('[').Append(t[0]).Append(',').Append(t[1]).Append(',').Append(t[2]).Append(']');}
        s.Append("]}");Icons.Add(s.ToString());
    }
    public static void Main(string[] args)
    {
        string root=args[0], output=args[1];
        string status=File.ReadAllText(Path.Combine(root,"UnityClient/Assets/_Project/Core/Runtime/CoreBattleStatusPresentation.cs"));
        var pairs=new SortedDictionary<string,string>();
        foreach(Match m in Regex.Matches(status,@"\b(?:Add|Flag|AddBatches)\(result,\s*""([^""]+)""[\s\S]*?CoreBattleStatusKind\.(\w+)")) pairs[m.Groups[1].Value+":"+m.Groups[2].Value]=m.Groups[2].Value;
        foreach(Match m in Regex.Matches(status,@"\bAddBurn\(result,\s*""([^""]+)""")) pairs[m.Groups[1].Value+":Fire"]="Fire";
        foreach(Match m in Regex.Matches(status,@"\bAddWeakness\(result,\s*""([^""]+)""")) pairs[m.Groups[1].Value+":Weakness"]="Weakness";
        foreach(string k in new[]{"Fire","Arcane","Curse","Tide","Thunder","Blood"}) pairs["special_doctrine_"+k.ToLowerInvariant()+":"+k]=k;
        foreach(var pair in pairs)
        {
            string id=pair.Key.Split(':')[0];var kind=(CoreBattleStatusKind)Enum.Parse(typeof(CoreBattleStatusKind),pair.Value);
            foreach(bool debuff in new[]{false,true}) {var icon=new BattleStatusIconGraphic();icon.Configure(kind,id,debuff);Export("status:"+id+":"+kind+":"+(debuff?"debuff":"buff"),"status",kind.ToString(),id,debuff,icon);}
        }
        foreach(BattleCommandIconKind kind in Enum.GetValues(typeof(BattleCommandIconKind))) {var icon=new BattleCommandIconGraphic();icon.Configure(kind);Export("command:"+kind,"command",kind.ToString(),"",false,icon);}
        foreach(bool settings in new[]{false,true})
        {
            var icon=new BattleHeaderIconGraphic();icon.Configure(settings,new Color32(235,208,160,255));
            Export("header:"+(settings?"Settings":"Deck"),"header",settings?"Settings":"Deck","",false,icon);
        }
        // CardFaceLayout.FrameSize is 280 x 480; deck pile and flight both retain this aspect.
        var card=new BattleCardFlightBackGraphic();card.Configure(Color.white);Export("cardback:Default","cardback","Default","",false,card,280,480);
        foreach(CoreNavalFieldKind kind in Enum.GetValues(typeof(CoreNavalFieldKind)))
        {
            var icon=new NavalFieldGraphic();var border=NavalFieldGraphic.Tint(kind);var fill=border;fill.a=.23f;
            icon.Configure(new NavalTacticalBoardView(),new[]{new NavalFieldVisualCell(new VoyagePoint(0,0),kind,fill,border,true,kind==CoreNavalFieldKind.Tide?1:0,0)});
            icon.SetMotion(.7f,null);
            Export("field:"+kind,"field",kind.ToString(),"",false,icon,84,84);
        }
        string metadata="{\"coordinateSystem\":\"top-left image coordinates; colors rgba 0..255\",\"method\":\"Unmodified production C# OnPopulateMesh invoked using export-only Unity math and VertexHelper compatibility types\",\"fieldSampling\":\"0.7 seconds, centered 75 pixel cell, zero camera rotation, tide points right, fill alpha 0.23\",\"headerTheme\":\"gray_gull #EBD0A0\",\"statusPairs\":"+pairs.Count+",\"iconCount\":"+Icons.Count+"}";
        File.WriteAllText(output,"{\"meta\":"+metadata+",\"icons\":["+string.Join(",",Icons)+"]}",new UTF8Encoding(false));
        Console.WriteLine("Exported "+Icons.Count+" meshes ("+pairs.Count+" status variants x 2 colors) to "+output);
    }
}
