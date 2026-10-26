using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;
internal record RangedRivenDraft(string WeaponId,int Rank,string Cost,string? Polarity,string[] Stats,string[] Values,int[] Signs,
    RangedRivenWorkshopState? Session,RangedRivenRoll? Baseline,string Cycles,string Attempts,string? Locked,bool Preserve,int Duration,int Samples,int Section,bool DisplayedCost=false);
internal record RangedRivenWorkspace(string? Selected,int Destination,RangedRivenDraft[] Drafts);
internal sealed partial class RangedRivenEditor
{
    internal RangedRivenDraft CaptureDraft()=>new(weapon.Id,rank.SelectedIndex,cost.Text,(polarity.SelectedItem as Choice)?.Key,
        rows.Select(r=>(r.Stat.SelectedItem as Choice)?.Key??"").ToArray(),rows.Select(r=>r.Value.Text).ToArray(),rows.Select(r=>r.Sign.SelectedIndex).ToArray(),
        session,baseline,cycles.Text,attempts.Text,(locked.SelectedItem as Choice)?.Key,preserve.IsChecked==true,duration.SelectedIndex,samples.SelectedIndex,sections.SelectedIndex,true);
    internal void RestoreDraft(RangedRivenDraft draft)
    {
        if(draft.WeaponId!=weapon.Id||draft.Rank is <0 or >8||draft.Stats.Length!=4||draft.Values.Length!=4||draft.Signs.Length!=4||draft.Signs.Any(v=>v is <0 or >1)||draft.Duration is <0 or >3||draft.Samples is <0 or >2||draft.Section is <0 or >3)throw new ArgumentException("총기 리벤 작업 복원 형식을 확인하세요.");
        if(draft.Session is {} state){RangedRivenWorkshop.Validate(state);if(state.Current.WeaponId!=weapon.Id)throw new ArgumentException("저장된 작업 무기가 다릅니다.");}
        if(draft.Baseline is {} before){RangedRivens.Validate(before,requireMetadata:false);if(before.WeaponId!=weapon.Id)throw new ArgumentException("저장된 비교 무기가 다릅니다.");}
        session=draft.Session;baseline=draft.Baseline;applyingWorkshop=true;
        try
        {
            rank.SelectedIndex=draft.Rank;cost.Text=!draft.DisplayedCost&&int.TryParse(draft.Cost,out var baseDrain)?(baseDrain+draft.Rank).ToString():draft.Cost;polarity.SelectedItem=polarity.Items.Cast<Choice>().SingleOrDefault(c=>c.Key==draft.Polarity);
            for(int i=0;i<4;i++){rows[i].Stat.SelectedItem=rows[i].Stat.Items.Cast<Choice>().Single(c=>c.Key==draft.Stats[i]);rows[i].Value.Text=draft.Values[i];rows[i].Sign.SelectedIndex=draft.Signs[i];}
            cycles.Text=draft.Cycles;attempts.Text=draft.Attempts;preserve.IsChecked=draft.Preserve;duration.SelectedIndex=draft.Duration;samples.SelectedIndex=draft.Samples;
        }
        finally{applyingWorkshop=false;}
        Refresh();updatingControls=true;locked.SelectedItem=locked.Items.Cast<Choice>().SingleOrDefault(c=>c.Key==draft.Locked)??locked.Items[0];updatingControls=false;RefreshCosts();sections.Select(draft.Section);
    }
}
