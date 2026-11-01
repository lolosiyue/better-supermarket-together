// The regression exercises the real save-load prefix and capacity code; these
// minimal host/array boundaries do not pretend to be a Unity gameplay test.
public class NPC_Manager {
    public bool isServer,initialEmployeesSpawnIsFinished;
    public int maxEmployees;
    public string[] hiredEmployeesData;
    public int[] priorityArray;
    public UnityEngine.GameObject[] employeesArray;
    public string[] NetworkhiredEmployeesData { set=>hiredEmployeesData=value; }
    public int[] NetworkpriorityArray { set=>priorityArray=value; }
    public T GetComponent<T>() where T:class=>null;
}
public class EmployeesDataGeneration { public void UpdateValuesInEmployeesTab(){} }
public class SaveBehaviour { public UnityEngine.GameObject NPCManager; }
namespace UnityEngine {
    public class GameObject {
        readonly object component;
        public GameObject(object value=null){component=value;}
        public T GetComponent<T>() where T:class=>component as T;
        public static bool operator ==(GameObject a,GameObject b)=>ReferenceEquals(a,b);
        public static bool operator !=(GameObject a,GameObject b)=>!ReferenceEquals(a,b);
        public override bool Equals(object value)=>ReferenceEquals(this,value);
        public override int GetHashCode()=>base.GetHashCode();
    }
}
namespace Mirror { public static class NetworkServer { public static bool active; } public static class NetworkClient { public static bool active, isConnected; } }
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch:Attribute { public HarmonyPatch(Type type,string name){} } }
namespace BetterSupermarketTogether {
    internal class BetterSupermarketPlugin {
        internal static bool IsHost=>Mirror.NetworkServer.active;
        internal static TestLogger Log=new TestLogger();
    }
    internal class TestLogger { internal void LogInfo(string message){} }
}
