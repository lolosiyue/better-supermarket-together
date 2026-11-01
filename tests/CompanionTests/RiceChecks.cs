using BetterSupermarketTogether;

internal static class RiceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var gate=new CompanionRicePolicy();gate.Initialize(100,0);
        check(!gate.Due(1299,1) && gate.Due(1300,1),"rice break cannot occur before 20 minutes");
        gate.Initialize(300,1);
        check(gate.Due(1300,1),"repeated ticks do not reroll the rice cooldown");
        gate.Ate(1300,1,1);
        check(!gate.Due(3700,1) && !gate.Due(3699,2) && gate.Due(3700,2),"one rice unit per day and up to 40-minute cooldown");
        gate.RetryLater(3700);
        check(!gate.Due(3819,2) && gate.Due(3820,2),"missing stock or idle opportunity retries no sooner than two minutes");
        check(CompanionRicePolicy.Idle(0,1,0,false,false,0,false),"native resting state 1 can take a rice break");
        check(!CompanionRicePolicy.Idle(0,1,0,false,false,0,true) && !CompanionRicePolicy.Idle(2,0,0,false,false,0,false) &&
            !CompanionRicePolicy.Idle(0,0,1,false,false,0,false),"walking, assigned jobs and carried goods block rice breaks");
        check(!CompanionRicePolicy.Idle(0,-1,0,true,false,0,false) && CompanionRicePolicy.Idle(0,-1,0,true,true,0,false),"only known idle waits can be interrupted for rice");
        check(CompanionRicePolicy.IsRice(3,"3_Rice") && CompanionRicePolicy.IsRice(33,"33_RiceBasmati(Clone)") &&
            CompanionRicePolicy.IsRice(34,"34_RiceLongGrain"),"three rice products match verified native asset IDs and names");
        check(!CompanionRicePolicy.IsRice(27,"27_RiceHat") && !CompanionRicePolicy.IsRice(20,"20_ChickenWithRice") &&
            !CompanionRicePolicy.IsRice(3,"Unknown") && !CompanionRicePolicy.IsRice(0,null),"non-rice products and changed catalog identities are excluded");
        int stock=2,calls=0;
        check(CompanionRicePolicy.ConsumeOne(()=>stock,()=>{calls++;stock--;}) && stock==1 && calls==1,"one snack decrements exactly one stocked unit");
        stock=0;calls=0;
        check(!CompanionRicePolicy.ConsumeOne(()=>stock,()=>{calls++;stock--;}) && calls==0,"empty shelf cannot be consumed or made negative");
        stock=1;calls=0;
        check(!CompanionRicePolicy.ConsumeOne(()=>stock,()=>{calls++;}) && stock==1 && calls==1,"unconfirmed native decrement is not reported as eating");
        check(!CompanionRicePolicy.ConsumeOne(()=>stock,()=>throw new InvalidOperationException("before decrement")) && stock==1,"failure before native decrement preserves stock");
        check(CompanionRicePolicy.ConsumeOne(()=>stock,()=>{stock--;throw new InvalidOperationException("after decrement");}) && stock==0,"post-decrement RPC failure counts the consumed unit without retrying");
    }
}
