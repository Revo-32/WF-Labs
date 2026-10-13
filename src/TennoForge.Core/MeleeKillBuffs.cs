namespace TennoForge.Core;

/// <summary>Kill buffs with different timer semantics; never pool them into one expiry.</summary>
public sealed class MeleeKillBuffs(bool pennant,bool berserker,bool galvanized=false)
{
    public const string PennantId="/Lotus/Weapons/Tenno/Melee/Swords/TnRailjackGreatKatana/TnRailJackGreatKatanaWeapon";
    public const string PennantSource="https://wiki.warframe.com/w/Pennant?oldid=2815686";
    public const string BerserkerSource="https://wiki.warframe.com/w/Berserker_Fury?oldid=2685881";
    private readonly List<double> pennantEnds=[];
    private double clock,berserkerEnd=double.PositiveInfinity,galvanizedEnd=double.PositiveInfinity;
    public int PennantStacks {get;private set;}
    public int BerserkerStacks {get;private set;}
    public int GalvanizedStacks {get;private set;}
    public double NextExpiry=>Math.Min(pennantEnds.DefaultIfEmpty(double.PositiveInfinity).Min(),Math.Min(berserkerEnd,galvanizedEnd));
    public void Advance(double time)
    {
        if(!double.IsFinite(time)||time<clock-1e-9)throw new ArgumentException("처치 버프 시각은 순서대로 진행해야 합니다.");
        time=Math.Max(clock,time);clock=time;
        pennantEnds.RemoveAll(end=>end<=time+1e-9);PennantStacks=pennantEnds.Count;
        // Berserker's 10-second timer loses ONE stack, then restarts.
        while(BerserkerStacks>0&&berserkerEnd<=time+1e-9){BerserkerStacks--;berserkerEnd+=10;}
        if(BerserkerStacks==0)berserkerEnd=double.PositiveInfinity;
        while(GalvanizedStacks>0&&galvanizedEnd<=time+1e-9){GalvanizedStacks--;galvanizedEnd+=20;}
        if(GalvanizedStacks==0)galvanizedEnd=double.PositiveInfinity;
    }
    public void Kill(double time,bool directHeavy,int combo)
    {
        if(combo is <1 or >12)throw new ArgumentException("처치 강공격의 콤보 배수는 1~12입니다.");
        Advance(time);
        if(pennant&&directHeavy){pennantEnds.Add(time+2+4*(combo-1));PennantStacks=pennantEnds.Count;}
        if(berserker){BerserkerStacks=Math.Min(2,BerserkerStacks+1);berserkerEnd=time+10;}
        if(galvanized){GalvanizedStacks=Math.Min(4,GalvanizedStacks+1);galvanizedEnd=time+20;}
    }
}
