namespace TennoForge.Core;

/// <summary>Direct-hit simulation. Proc damage is deliberately a separate event model.</summary>
internal static class MeleeTtkEngine
{
    internal sealed class TargetState(double health,double shields,double overguard)
    {
        internal double Health=health,Shields=shields,Overguard=overguard,ShieldGateUntil=-1;
        internal double StealthReadyAt=double.NaN;
        internal bool EnergyChannelUsed;
        internal void Hit(Calculation c,double critical,double time,bool statusTick=false,bool bypassArmor=false)
        {
            var setup=c.Melee!.Setup;var statuses=setup.Statuses??[];
            var magnetic=statuses.GetValueOrDefault(DamageType.Magnetic)>0?1.75+.25*statuses[DamageType.Magnetic]:1;
            var viral=statuses.GetValueOrDefault(DamageType.Viral)>0?1.75+.25*statuses[DamageType.Viral]:1;
            var raw=c.Damage.ToDictionary(p=>p.Key,p=>Math.Max(0,p.Value*critical));
            if(c.Melee.ExtraDamage>0)Health-=c.Melee.ExtraDamage*EnergyChannelState.TargetMultiplier(setup,c.ArmorReduction,c.Melee.IncomingDamageMultiplier);
            foreach(var type in raw.Keys.ToArray())raw[type]*=c.Melee.IncomingDamageMultiplier;
            if(Overguard>0)
            {
                var amount=raw.Sum(p=>p.Value*magnetic*(p.Key==DamageType.Void?1.5:1));
                var used=amount>0?Math.Min(1,Overguard/amount):1;Overguard=Math.Max(0,Overguard-amount);
                foreach(var key in raw.Keys.ToArray())raw[key]*=1-used;
            }
            var profile=MeleeRuleCatalog.Load().EnemyProfiles.Single(p=>p.Id==setup.Faction);
            foreach(var key in raw.Keys.ToArray())raw[key]*=profile.Multiplier(key);
            if(Shields>0)
            {
                var amount=raw.Where(p=>!DamageTypeRules.BypassesShields(p.Key)).Sum(p=>p.Value)*magnetic;
                var used=amount>0?Math.Min(1,Shields/amount):1;
                if(amount>=Shields)ShieldGateUntil=time+.1;
                Shields=Math.Max(0,Shields-amount);
                foreach(var key in raw.Keys.Where(k=>!DamageTypeRules.BypassesShields(k)).ToArray())raw[key]*=1-used;
            }
            var radial=setup.Attack.Contains("Slam")||setup.Attack.Contains("Explosion");
            var reduction=bypassArmor?1:1-c.ArmorReduction;
            Health-=raw.Sum(p=>p.Value*(DamageTypeRules.BypassesArmor(p.Key)?1:reduction)*viral*(!DamageTypeRules.BypassesShields(p.Key)&&!statusTick&&time<ShieldGateUntil?(radial?0:.05):1));
        }
    }
    internal static TtkResult Simulate(Calculation c,double health,int trials,uint seed,double horizon)
    {
        Validate(health,trials,seed,horizon);
        var setup=c.Melee!.Setup;var state=seed;
        double Next(){state^=state<<13;state^=state>>17;state^=state<<5;return state/4294967296d;}
        var times=new double[trials];var killed=0;var steps=(int)Math.Min(100000,Math.Floor(horizon/c.HitInterval));
        if((double)trials*steps*c.Melee.Projectiles*(c.DuplicateChance>0?2:1)>4000000)throw new ArgumentException("현재 타격 간격·대상 조건에서는 TTK 계산량이 한도를 넘습니다. 반복 간격 또는 분석 시간을 조정하세요.");
        for(var i=0;i<trials;i++)
        {
            var target=new TargetState(health,setup.Shields,setup.Overguard);times[i]=double.PositiveInfinity;
            for(var step=1;step<=steps;step++)
            {
                Strike(c,target,step*c.HitInterval,Next);
                if(target.Health<=1e-9){times[i]=step*c.HitInterval;killed++;break;}
            }
        }
        Array.Sort(times);double? Q(double p){var t=times[(int)Math.Ceiling(p*trials)-1];return double.IsFinite(t)?t:null;}
        return new(Q(.1),Q(.5),Q(.9),killed/(double)trials,trials,seed,horizon);
    }
    internal static StanceTtkResult Simulate(StanceCalculation c,double health,int trials,uint seed,double horizon)
    {
        Validate(health,trials,seed,horizon);
        if(c.Hits.Any(h=>MeleeStealth.NeedsContactCooldown(h.Calculation!)))throw new ArgumentException("은신 접촉 대기 3초의 적용 순서를 위해 개별 타격 시각이 필요합니다. 시간별 전투 경로에서 계산하세요.");
        if(c.Stance.Id=="High Noon"&&c.Combo.Key=="Neutral"&&c.Hits.Any(h=>MeleeStealth.Invisible(h.Calculation!.Melee!.Setup))&&c.Hits.Any(h=>MeleeRuleCatalog.Load().Weapon(h.Input!.WeaponId).Name!="Stropha"))throw new CombatRuleUnavailableException("하이 눈 정지 연사의 순간 은신 보너스 차단 시간은 위키 미기재입니다.");
        if(c.Duration is not {} duration)throw new ArgumentException("위키에 동작 시간이 없어 TTK를 확정할 수 없습니다. 동작별 타격 피해는 계산됩니다.");
        if(c.Stats.Limitations.Any(n=>n.Contains("타격별 CO 분포")))throw new ArgumentException("콤보 전체 CO 보정만 공개되어 있어 타격별 TTK 분포를 산출하지 않습니다.");
        var setup=c.Stats.Melee!.Setup;
        if(setup.Shields>0)throw new ArgumentException("개별 타격 시각이 없어 0.1초 실드 게이트를 반영한 스탠스 TTK는 확정할 수 없습니다. 단일 공격에서 비교하세요.");
        var cycles=(int)Math.Floor(horizon/duration);var completed=new int[trials];var killed=0;var state=seed;
        if(c.Combo.FinisherKind is not (null or "Ground"))cycles=Math.Min(1,cycles);
        if((double)trials*cycles*c.Hits.Sum(h=>h.Count*h.Calculation!.Melee!.Projectiles*(h.Calculation.DuplicateChance>0?2:1))>4000000)throw new ArgumentException("현재 콤보·대상 조건에서는 TTK 계산량이 한도를 넘습니다. 분석 시간을 조정하세요.");
        double Next(){state^=state<<13;state^=state>>17;state^=state<<5;return state/4294967296d;}
        for(var i=0;i<trials;i++)
        {
            var target=new TargetState(health,setup.Shields,setup.Overguard);completed[i]=int.MaxValue;
            for(var cycle=1;cycle<=cycles;cycle++)
            {
                foreach(var h in c.Hits)
                {
                    if(h.Input is {} input&&WeaponStanceMechanic.IsQuassus(input.WeaponId)&&MetronomeRules.OperaApplies(input.WeaponId,input.Melee!.Attack))
                    {
                        var count=MetronomeRules.Roll(h.Count*h.Calculation!.Melee!.Projectiles,Next);
                        var single=h.Calculation with {Melee=h.Calculation.Melee with {Projectiles=1}};
                        for(var n=0;n<count&&target.Health>1e-9;n++)Strike(single,target,cycle*duration,Next);
                    }
                    else for(var n=0;n<h.Count&&target.Health>1e-9;n++)Strike(h.Calculation!,target,cycle*duration,Next);
                    if(target.Health<=1e-9)break;
                }
                if(target.Health<=1e-9){completed[i]=cycle;killed++;break;}
            }
        }
        Array.Sort(completed);TimeBounds Q(double p){var cycle=completed[(int)Math.Ceiling(p*trials)-1];return cycle==int.MaxValue?new(null,null):new((cycle-1)*c.Duration,cycle*c.Duration);}
        return new(Q(.1),Q(.5),Q(.9),killed/(double)trials,trials,cycles*duration);
    }
    internal static void Strike(Calculation c,TargetState target,double time,Func<double> random)
    {
        if(!c.Melee!.HitConnects)return;
        var floor=Math.Floor(c.CriticalChance);var fraction=c.CriticalChance-floor;
        if(double.IsNaN(target.StealthReadyAt))target.StealthReadyAt=c.Melee!.Setup.Conditions?.GetValueOrDefault("StealthCooldown")??0;
        double Multiplier(double tier)
        {
            var value=MeleeStealth.AtTier(c,tier);
            if(MeleeStealth.Invisible(c.Melee!.Setup)&&!MeleeStealth.Blinded(c.Melee.Setup)&&c.Melee.Setup.Finisher is null or "Ground")value+= (time+1e-9>=target.StealthReadyAt?c.Melee.StealthReadyBonus:0)-c.Melee.StealthBonus;
            if(MeleeStealth.NeedsContactCooldown(c))target.StealthReadyAt=time+3;
            return value;
        }
        void Contact(double tier)
        {
            var actual=target.EnergyChannelUsed&&c.Melee!.ExtraDamage>0?c with {Melee=c.Melee with {ExtraDamage=0}}:c;
            if(actual.Melee!.ExtraDamage>0)target.EnergyChannelUsed=true;
            target.Hit(actual,Multiplier(tier),time);
        }
        var pellets=MetronomeRules.Roll(c.Melee!.Projectiles,random);
        for(var pellet=0;pellet<pellets&&target.Health>1e-9;pellet++)
        {
            var tier=floor+(random()<fraction?1:0);Contact(tier);
            if(tier==1&&c.DuplicateChance>0&&random()<c.DuplicateChance)
            {var extra=floor+(random()<fraction?1:0);Contact(extra);}
        }
    }
    private static void Validate(double health,int trials,uint seed,double horizon)
    {if(!double.IsFinite(health)||health<=0||health>1e12||trials is <1 or >10000||seed==0||!double.IsFinite(horizon)||horizon<=0||horizon>300)throw new ArgumentException("TTK 입력 범위 오류");}
}
