using BetterSupermarketTogether;
using UnityEngine;
using System.Reflection;
int checks=0;
void Check(bool ok,string name) { if(!ok) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
void Button(bool expectedRun,bool expectedResult) {
 object[] args={true};
 bool run=(bool)typeof(CompanionButtonCapturePatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
 Check(run==expectedRun && (bool)args[0]==expectedResult,"button action capture");
}
Time.frameCount=1;
Check(!CompanionInputCapture.Blocked,"closed panel leaves gameplay input available");
SettingsPanelUI.CompanionOpen=true;
Button(false,false);
object[] axis={4f};
typeof(CompanionAxisCapturePatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,axis);
Check((float)axis[0]==0,"chat suppresses movement axes");
SettingsPanelUI.CompanionOpen=false; Input.anyKey=true; Time.frameCount=2;
Check(CompanionInputCapture.Blocked,"closing click or held key cannot leak into game");
Input.anyKey=false; Time.frameCount=3;
Check(CompanionInputCapture.Blocked,"release edge remains captured");
Time.frameCount=4;
Button(true,true);
SettingsPanelUI.Typing=true;
Check(CompanionInputCapture.Blocked,"other mod text fields also capture gameplay");
SettingsPanelUI.Typing=false; Time.frameCount=5;
Check(CompanionInputCapture.Blocked,"focus loss captures release frame");
Time.frameCount=6;
Check(!CompanionInputCapture.Blocked,"capture restores after focus loss");
var buttons=CompanionInputCapture.Targets(typeof(bool)).ToArray();
Check(buttons.Length==3 && buttons.All(x=>x.Name!="Unrelated"),"select button overloads and negative actions only");
Check(CompanionInputCapture.Targets(typeof(float)).Single().Name=="GetAxis","select scalar axis reads");
Check(CompanionInputCapture.Targets(typeof(Vector2)).Single().Name=="GetAxis2D","select vector axis reads");
Console.WriteLine($"{checks} input checks passed (Unity/Harmony stubs; no runtime acceptance).");
namespace BetterSupermarketTogether { internal static class SettingsPanelUI { internal static bool CompanionOpen,Typing; } }
namespace UnityEngine { public struct Vector2 { public static Vector2 zero=>default; } public static class Input { public static bool anyKey; } public static class Time { public static int frameCount; } }
namespace HarmonyLib { public class HarmonyPatch:Attribute {} public static class AccessTools { public static Type TypeByName(string name)=>typeof(Rewired.Player); } }
namespace Rewired { public class Player { public bool GetButton(string s)=>true; public bool GetButton(int i)=>true; public bool GetNegativeButton(int i)=>true; public bool Unrelated()=>true; public float GetAxis(string s)=>1; public Vector2 GetAxis2D(int x,int y)=>default; } }
