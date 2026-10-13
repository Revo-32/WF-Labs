using TennoForge.Core;

internal static class CombatCoverageAudit
{
    internal record Row(string Id,string Name,bool Supported,string[] Reasons);
    internal static object Run()
    {
        var c=Catalog.LoadMelee();var stances=StanceCatalog.Load();var rows=new List<Row>();var defects=new List<string>();
        var sampleZaws=ZawCatalog.Parts.Strikes.SelectMany(s=>new[]{"Peye","Seekalla"}.Select(g=>ZawCatalog.Weapon(ZawCatalog.Id(s.Id,g,"Jai"))));
        foreach(var w in c.Weapons.Where(w=>!ZawCatalog.IsZaw(w.Id)).Concat(sampleZaws))
        {
            var build=new Build(w.Id,new ModSlot?[8],new(Enemy:new(1e9,500)),c.Version,5,Capacity:CapacityCatalog.Load().Default(w.Id,null),Melee:new(),Encounter:new(EncounterMode.FiveSeconds));
            try
            {
                var r=CombatTimeline.Calculate(c,build,stances,1);
                rows.Add(new(w.Id,w.Name,r.Unsupported.Length==0,r.Unsupported));
                if(r.Points.Any(p=>!double.IsFinite(p.DirectDamage+p.StatusDamage+p.SnapshotDps+p.WithoutCoDps)||p.Health<0||p.SnapshotDps+1e-8<p.WithoutCoDps))defects.Add(w.Id+": 시간 결과 범위 오류");
            }
            catch(ArgumentException ex){rows.Add(new(w.Id,w.Name,false,[ex.Message]));}
            catch(Exception ex){defects.Add(w.Id+": "+ex.Message);}
        }
        return new{version=CombatTimeline.Version,scope="무모드 일반 공격·단일 대상·5초·표본1. 모든 조합/효과의 정확성 증명이 아니라 전체 무기 경로의 유한값·지원 구분 검사.",supported=rows.Count(r=>r.Supported),unsupported=rows.Count(r=>!r.Supported),weapons=rows,defects};
    }
}
