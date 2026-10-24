using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed class RangedRivenWorkbench : Grid
{
    // Internal catalog identity keeps old saved sessions readable; it is not an analysis input.
    private readonly WeaponSearchBox weapon=new(){Width=420};
    private readonly TextBlock context=new(){VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,20,0)};
    internal System.Action? ContextChanged;
    internal void UseEquippedIfEmpty(RangedRivenRoll roll){if(content.Content is RangedRivenEditor e&&e.IsEmpty)Set(roll);}
    internal string RivenClass=>weapon.SelectedItem is Weapon w?RangedEngine.ModCategory(RangedEngine.Weapons.Single(x=>x.Id==w.Id)):"Rifle";
    private readonly ComboBox destination=new(){ItemsSource=Enumerable.Range(1,8).Select(n=>$"슬롯 {n}").ToArray(),SelectedIndex=0,Width=110};
    private readonly ContentControl content=new();
    private readonly TextBlock message=new(){Visibility=Visibility.Collapsed,TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,12)};
    private readonly Dictionary<string,RangedRivenEditor> editors=[];
    private readonly Action<RangedRivenRoll,int> equip;
    private readonly string category;
    private readonly Func<RangedBuild> capture;
    internal RangedRivenWorkbench(string category,Action<RangedRivenRoll,int> equip,Func<RangedRivenRoll?> equipped,Func<RangedBuild> capture)
    {
        this.category=category;this.equip=equip;this.capture=capture;
        RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var toolbar=new WrapPanel{Margin=new(0,0,0,12)};toolbar.Children.Add(context);destination.Margin=new(12,0,12,0);toolbar.Children.Add(destination);
        void Action(string label,System.Action action){var button=new Button{Content=label,Margin=new(0,0,8,0),Padding=new(12,6,12,6)};button.Click+=(_,_)=>{try{action();}catch(Exception ex) when(ex is ArgumentException or JsonException or IOException or InvalidOperationException){message.Text=ex.Message;message.Visibility=Visibility.Visible;}};toolbar.Children.Add(button);}
        Action("분석 저장",Save);Action("불러오기",Load);Action("장착한 리벤 가져오기",()=>Set(equipped()??throw new ArgumentException("모딩 화면에 장착한 리벤이 없습니다.")));
        destination.SelectionChanged+=(_,_)=>{if(content.Content is RangedRivenEditor editor)editor.InvalidateComparison();};
        Children.Add(toolbar);message.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");Grid.SetRow(message,1);Children.Add(message);Grid.SetRow(content,2);Children.Add(content);
        weapon.ItemsSource=RangedEngine.Weapons.Where(w=>w.Category==category&&RangedEngine.N(w.Raw,"Disposition")>0&&w.WeaponType!="Exalted Weapon").Select(w=>new Weapon(w.Id,w.Name,w.EnglishName,[],0,1,0,1,w.WeaponType)).ToArray();
        weapon.SelectionChanged+=(_,_)=>{if(weapon.SelectedItem is not Weapon selected)return;message.Text="";if(!editors.TryGetValue(selected.Id,out var editor))editors[selected.Id]=editor=Create(RangedEngine.Weapons.Single(w=>w.Id==selected.Id),null);content.Content=editor;ContextChanged?.Invoke();};
        weapon.SelectedItem=weapon.Items.Cast<Weapon>().FirstOrDefault(w=>w.EnglishName==(category=="primary"?"Braton":"Lato"))??weapon.Items.Cast<Weapon>().First();
    }
    private RangedRivenEditor Create(ReferenceWeapon selected,RangedRivenRoll? roll)=>new(selected,roll,r=>equip(RivenBuildBinding.Bind(r,capture()),destination.SelectedIndex),()=>{},inline:true,capture:capture,destination:()=>destination.SelectedIndex);
    internal void RefreshContext()
    {
        context.SetResourceReference(TextBlock.ForegroundProperty,"MutedBrush");
        try{var build=capture();var target=RangedEngine.Weapons.Single(w=>w.Id==build.WeaponId);context.Text=RangedEngine.ModCategory(target)==RivenClass?$"DPS 비교 · {target.Name}":$"DPS 비교 · {target.Name} (리벤 종류가 다름)";}
        catch(ArgumentException){context.Text="DPS 비교 대상 · 모딩에서 무기 선택";}
    }
    internal void SelectClass(string kind)
    {
        if(kind==RivenClass)return;
        var existing=editors.Keys.FirstOrDefault(id=>RangedEngine.ModCategory(RangedEngine.Weapons.Single(w=>w.Id==id))==kind);
        var target=existing is null?weapon.Items.Cast<Weapon>().First(w=>RangedEngine.ModCategory(RangedEngine.Weapons.Single(x=>x.Id==w.Id))==kind):weapon.Items.Cast<Weapon>().Single(w=>w.Id==existing);
        weapon.SelectedItem=target;RefreshContext();
    }
    internal void Set(RangedRivenRoll roll)
    {
        RangedRivens.Validate(roll,requireMetadata:false);var selected=RangedEngine.Weapons.Single(w=>w.Id==roll.WeaponId);
        if(selected.Category!=category)throw new ArgumentException("현재 무기 구분과 리벤의 무기가 다릅니다.");
        foreach(var id in editors.Keys.Where(id=>RangedEngine.ModCategory(RangedEngine.Weapons.Single(w=>w.Id==id))==RangedEngine.ModCategory(selected)).ToArray())editors.Remove(id);
        editors[selected.Id]=Create(selected,roll);weapon.SelectedItem=weapon.Items.Cast<Weapon>().Single(w=>w.Id==roll.WeaponId);content.Content=editors[selected.Id];RefreshContext();message.Text="";message.Visibility=Visibility.Collapsed;ContextChanged?.Invoke();
    }
    private void Save()
    {
        var document=(content.Content as RangedRivenEditor)?.Export()??throw new ArgumentException("옵션과 수치를 확인한 뒤 저장하세요.");
        var dialog=new SaveFileDialog{Filter="WF Labs 총기 리벤|*.wfriven.json",FileName="WF-Labs-riven.wfriven.json"};
        if(dialog.ShowDialog()==true){File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(document,Catalog.Json));message.Text="리벤을 저장했습니다.";message.Visibility=Visibility.Visible;}
    }
    private void Load()
    {
        var dialog=new OpenFileDialog{Filter="WF Labs 총기 리벤|*.wfriven.json"};
        if(dialog.ShowDialog()!=true)return;
        var document=ParseDocument(File.ReadAllText(dialog.FileName));
        Set(document.Workshop.Current);((RangedRivenEditor)content.Content).Import(document);
    }
    internal static RangedRivenDocument ParseDocument(string json)
    {
        using var parsed=JsonDocument.Parse(json);
        var document=parsed.RootElement.TryGetProperty("workshop",out _)
            ?JsonSerializer.Deserialize<RangedRivenDocument>(json,Catalog.Json)??throw new ArgumentException("작업 파일이 비어 있습니다.")
            :new RangedRivenDocument(new(JsonSerializer.Deserialize<RangedRivenRoll>(json,Catalog.Json)??throw new ArgumentException("리벤 파일이 비어 있습니다.")));
        RangedRivenWorkshop.Validate(document.Workshop);
        if(document.Baseline is {} baseline)
        {
            RangedRivens.Validate(baseline,requireMetadata:false);
            if(baseline.WeaponId!=document.Workshop.Current.WeaponId)throw new ArgumentException("작업 파일의 리벤과 비교 기준 무기가 다릅니다.");
        }
        return document;
    }

    internal RangedRivenWorkspace CaptureWorkspace()=>new((weapon.SelectedItem as Weapon)?.Id,destination.SelectedIndex,editors.Values.Select(e=>e.CaptureDraft()).ToArray());
    internal void RestoreWorkspace(RangedRivenWorkspace workspace)
    {
        if(workspace.Destination is <0 or >7)throw new ArgumentException("복원할 리벤 슬롯을 확인하세요.");
        foreach(var draft in workspace.Drafts)
        {
            var selected=RangedEngine.Weapons.Single(w=>w.Id==draft.WeaponId&&w.Category==category);
            var editor=Create(selected,null);editor.RestoreDraft(draft);editors[selected.Id]=editor;
        }
        destination.SelectedIndex=workspace.Destination;
        if(workspace.Selected is {} id){weapon.SelectedItem=weapon.Items.Cast<Weapon>().Single(w=>w.Id==id);content.Content=editors[id];}
    }
    internal void PrepareRecoveryCheck(bool pending)=>((RangedRivenEditor)content.Content).PrepareRecoveryCheck(pending);
    internal Task<object> CheckWorkshop()=>((RangedRivenEditor)content.Content).CheckWorkshopAsync();
    internal void SelectSectionForCheck(int index)=>(content.Content as RangedRivenEditor)!.SelectSectionForCheck(index);
    internal void CheckAndEquip()
    {
        if(content.Content is not RangedRivenEditor editor)throw new InvalidOperationException("총기 리벤 작업실이 표시되지 않았습니다.");
        destination.SelectedIndex=3;editor.CheckAndApply();
    }
}
