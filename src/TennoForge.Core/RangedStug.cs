using System.Text.Json;
using System.Text.Json.Nodes;
namespace TennoForge.Core;

public static class RangedStug
{
    public static bool Applies(ReferenceWeapon w)=>w.InternalId=="/Lotus/Weapons/Grineer/Pistols/GrineerCrossbow/GrineerGooGun";
    public static bool Launch(ReferenceWeapon w,int index)=>Applies(w)&&index is 0 or 2 or 7;
    public static JsonElement[] Append(ReferenceWeapon w,JsonElement[] attacks)
    {
        if(!Applies(w))return attacks;
        var result=attacks.ToList();
        foreach(var original in new[]{0,2})
        {
            var n=JsonNode.Parse(attacks[original].GetRawText())!;n["AttackIndex"]=result.Count+1;n["AttackName"]=original==0?"Embedded Blob Tick":"Incarnon Embedded Blob Tick";n["ShotType"]="AoE";n["Trigger"]="Semi-Auto";result.Add(JsonSerializer.SerializeToElement(n));
            n=JsonNode.Parse(attacks[original].GetRawText())!;n["AttackName"]=original==0?"Blob Launch":"Incarnon Blob Launch";n["ShotType"]="Projectile";n["Trigger"]="Auto";n.AsObject().Remove("Falloff");n.AsObject().Remove("Range");result[original]=JsonSerializer.SerializeToElement(n);
        }
        var charged=JsonNode.Parse(result[0].GetRawText())!;charged["AttackIndex"]=8;charged["AttackName"]="Fully Charged Blob";charged["Trigger"]="Charge";charged["ChargeTime"]=2;charged["AmmoCost"]=10;result.Add(JsonSerializer.SerializeToElement(charged));
        return result.ToArray();
    }
    public static JsonElement Attack(ReferenceWeapon w,JsonElement attack,int index,RangedConditions c)
    {
        if(!Applies(w))return attack;
        var form=index is 2 or 3 or 4 or 6;var limit=form?5:10;
        if(c.StugStacks<1||c.StugStacks>limit)throw new ArgumentException("스터그 구체 중첩 수가 형태별 한도를 벗어납니다.");
        var n=JsonNode.Parse(attack.GetRawText())!;var size=c.StugStacks;
        if(index is 5 or 6)n["Damage"]=new JsonObject{["Corrosive"]=(index==5?4:50)*Math.Min(limit-1,size)};
        if(index is 1 or 3)
        {
            n["Damage"]=new JsonObject{["Corrosive"]=index==1?75*size:200};
            double? radius=size==1?.3:index==1&&size==10?2.8:c.StugExplosionRadius??(c.AllowApproximations?.3+(2.8-.3)*(size-1)/9d:null);
            if(radius is {} value&&(!double.IsFinite(value)||value<=0||value>100))throw new ArgumentException("스터그 폭발 반경 시나리오 값 오류");
            if(radius is null&&(c.RadialDistance>0||c.GroupCount>1))throw new CombatRuleUnavailableException("위키는 스터그의 중간 중첩별 정확한 반경을 기재하지 않습니다. 폭발 중심의 단일 대상 외에는 중첩 폭발 반경을 시나리오 값으로 지정하세요.");
            if(radius is {} r){n["Range"]=r;n["Falloff"]=new JsonObject{["StartRange"]=0,["EndRange"]=r,["Reduction"]=.7};}
            else{n.AsObject().Remove("Range");n.AsObject().Remove("Falloff");}
        }
        return JsonSerializer.SerializeToElement(n);
    }
}

public record StugEmission(int Component,int Stacks,int Shot,int Target,bool Explosion);
public sealed class RangedStugState
{
    private sealed class Blob
    {
        public int Stacks,Shot,Target;public bool Form;public double End,Tick;
    }
    private readonly List<Blob> blobs=[];
    public double Next=>blobs.Select(b=>Math.Min(b.End,b.Tick)).DefaultIfEmpty(double.PositiveInfinity).Min();
    private static StugEmission Emit(Blob b,bool explosion)=>new(explosion?(b.Form?3:1):(b.Form?6:5),b.Stacks,b.Shot,b.Target,explosion);
    public StugEmission[] Embed(double time,int stacks,int shot,int target,bool form,bool merge)
    {
        var blob=merge?blobs.FirstOrDefault(b=>b.Target==target&&b.Form==form&&b.End>time+1e-9):null;
        var created=blob is null;
        if(created){blob=new(){Target=target,Form=form,Tick=time+1};blobs.Add(blob);}
        blob!.Stacks=Math.Min(form?5:10,blob.Stacks+stacks);blob.Shot=shot;blob.End=time+1.5;
        if(blob.Stacks==(form?5:10)){blobs.Remove(blob);return [Emit(blob,true)];}
        return created?[Emit(blob,false)]:[];
    }
    public StugEmission[] Advance(double time)
    {
        var result=new List<StugEmission>();
        foreach(var b in blobs.ToArray())
        {
            if(b.End<=time+1e-9){result.Add(Emit(b,true));blobs.Remove(b);}
            else if(b.Tick<=time+1e-9){result.Add(Emit(b,false));b.Tick+=1;}
        }
        return result.ToArray();
    }
}
