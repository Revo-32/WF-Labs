namespace TennoForge.Core;

/// <summary>Explicit active buff magnitudes for the Contagion projectile scenario.
/// Values are coefficients (+350% = 3.5), not ability strength or cast timing.</summary>
public record ContagionDamageBuffs(double VexArmor=0,double Deathbringer=0,double Amp=0,double RiftTorrent=0,
    double LethalLevitation=0,double VoidStrike=0,double Eclipse=0,double DeathGate=0)
{
    public double WeaponAdditive=>VexArmor+Deathbringer+Amp+RiftTorrent+LethalLevitation;
    public double MeleeAdditive=>WeaponAdditive+VoidStrike;
    public double SeparateMultiplier=>Math.Pow((1+Eclipse)*(1+DeathGate),2);
    public bool Active=>Values.Any(x=>x>0);
    public double[] Values=>[VexArmor,Deathbringer,Amp,RiftTorrent,LethalLevitation,VoidStrike,Eclipse,DeathGate];
    public void Validate(){foreach(var value in Values)MeleeEngine.Range(value,0,1000,"컨테이전의 실제 외부 피해 버프");}
    public string Description=>$"컨테이전 외부 버프: 근접 피해 가산+{MeleeAdditive:P0}, 무기 피해 가산+{WeaponAdditive:P0}, 별도 배수×{SeparateMultiplier:0.###}. 보이드 스트라이크는 근접 브라켓에 한 번, 이클립스·데스 게이트는 각각 두 번 곱합니다.";
}
