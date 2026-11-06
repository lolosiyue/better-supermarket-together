using System.Collections.Generic;
using UnityEngine;
namespace BetterSupermarketTogether;
internal sealed class CompanionIdlePose
{
    private readonly int _kind;
    private readonly float _started,_duration;
    private float _stop=-1;
    private readonly Vector3? _lookTarget;
    internal bool IsNod => _kind==3;
    // Social reactions may run during work; they stop only when hands are needed.
    internal bool IsSocial => _kind==3 || _kind>=5 && _kind<=7;
    internal CompanionIdlePose(int kind,float now,Vector3? lookTarget=null){_kind=kind;_started=now;_lookTarget=lookTarget;_duration=kind==3?1.6f:kind==4?2.8f:kind==1?3.2f:kind==5?2.6f:kind==6?2.4f:kind==7?2f:kind==8?3.2f:5f;}
    internal void Stop(float now){if(_stop<0)_stop=now;}
    internal bool Apply(float now,CompanionPoseBridge bridge,Dictionary<string,Transform> human,Transform root)
    {
        float elapsed=now-_started;
        if(elapsed>=_duration || _stop>=0 && now-_stop>=.3f)return false;
        float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.65f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((_duration-elapsed)/.65f));
        if(_stop>=0)weight*=1-Mathf.SmoothStep(0,1,(now-_stop)/.3f);
        var head=human["head"];var hips=human["hips"];
        float height=Vector3.Distance(head.position,(human["leftFoot"].position+human["rightFoot"].position)*.5f);
        var up=root.up;var forward=root.forward;var right=root.right;
        if(_kind==5)
        {
            // Wave: look at the player and wave the right hand beside the face.
            head.rotation=Quaternion.AngleAxis(TargetYaw(head,forward,up,35)*weight,up)*head.rotation;
            Vector3 hand=head.position-up*height*.02f+right*height*.17f+forward*height*.08f+right*height*.05f*Mathf.Sin(elapsed*9);
            bridge.PoseHand(true,hand,weight);
            head.rotation=Quaternion.AngleAxis(-6*weight,forward)*head.rotation;
        }
        else if(_kind==6)
        {
            // Shy: meet the player's eyes, then look away and down.
            float p=Mathf.Clamp01(elapsed/_duration),away=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.6f,p));
            float yaw=TargetYaw(head,forward,up,35);
            yaw=Mathf.Lerp(yaw,(yaw>=0?-1:1)*22,away);
            head.rotation=Quaternion.AngleAxis(yaw*weight,up)*head.rotation;
            head.rotation=Quaternion.AngleAxis(16*away*weight,right)*head.rotation;
            head.rotation=Quaternion.AngleAxis(8*weight,forward)*head.rotation;
        }
        else if(_kind==7)
        {
            // Curious head tilt toward the player.
            float tilt=Mathf.Sin(Mathf.Clamp01(elapsed/_duration)*Mathf.PI);
            head.rotation=Quaternion.AngleAxis(TargetYaw(head,forward,up,35)*weight,up)*head.rotation;
            head.rotation=Quaternion.AngleAxis(-14*tilt*weight,forward)*head.rotation;
        }
        else if(_kind==8)
        {
            // Yawn: hand over the mouth, head tips back.
            float bump=Mathf.Sin(Mathf.Clamp01(elapsed/_duration)*Mathf.PI);
            bridge.PoseHand(true,head.position+forward*height*.09f-up*height*.035f+right*height*.01f,weight);
            head.rotation=Quaternion.AngleAxis(-14*bump*weight,right)*head.rotation;
        }
        else if(_kind==3)
        {
            if(_lookTarget.HasValue)
            {
                Vector3 delta=_lookTarget.Value-head.position;
                float yaw=Mathf.Clamp(Vector3.SignedAngle(forward,Vector3.ProjectOnPlane(delta,up),up),-30,30);
                head.rotation=Quaternion.AngleAxis(yaw*weight,up)*head.rotation;
            }
            head.rotation=Quaternion.AngleAxis(Mathf.Sin(Mathf.Clamp01(elapsed/_duration)*Mathf.PI)*12*weight,right)*head.rotation;
        }
        else if(_kind==4)
        {
            // Small shoulder/arm release, without pausing the native worker.
            float lift=Mathf.Sin(elapsed/_duration*Mathf.PI)*weight;
            bridge.PoseHand(false,human["leftHand"].position+up*height*.045f*lift+right*height*.025f,weight);
            bridge.PoseHand(true,human["rightHand"].position+up*height*.045f*lift-right*height*.025f,weight);
            head.rotation=Quaternion.AngleAxis(5*lift,right)*head.rotation;
        }
        else if(_kind==1)
        {
            float yaw=Mathf.Sin(elapsed/_duration*2*Mathf.PI)*24*weight;
            head.rotation=Quaternion.AngleAxis(yaw,up)*head.rotation;
            if(human.TryGetValue("chest",out var chest))chest.rotation=Quaternion.AngleAxis(yaw*.2f,up)*chest.rotation;
        }
        else if(_kind==0)
        {
            bridge.PoseHand(false,head.position+up*height*.28f-right*height*.16f+forward*height*.04f,weight);
            bridge.PoseHand(true,head.position+up*height*.28f+right*height*.16f+forward*height*.04f,weight);
            head.rotation=Quaternion.AngleAxis(-6*weight,right)*head.rotation;
        }
        else
        {
            float stroke=.5f-.5f*Mathf.Cos(Mathf.Clamp01(elapsed/_duration)*4*Mathf.PI);
            Vector3 center=hips.position+forward*height*.14f-up*height*(.04f+.12f*stroke);
            bridge.PoseHand(false,center-right*height*.14f,weight);
            bridge.PoseHand(true,center+right*height*.14f,weight);
            head.rotation=Quaternion.AngleAxis(12*weight,right)*head.rotation;
        }
        return true;
    }

    private float TargetYaw(Transform head,Vector3 forward,Vector3 up,float limit)
    {
        if(!_lookTarget.HasValue)return 0;
        return Mathf.Clamp(Vector3.SignedAngle(forward,Vector3.ProjectOnPlane(_lookTarget.Value-head.position,up),up),-limit,limit);
    }
}
