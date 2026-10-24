using System.Windows;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class RangedPage
{
    private void EditKitgun()=>CreateKitgunDialog()?.ShowDialog();
    internal Window? CreateKitgunDialog()
    {
        var definitions=RangedKitguns.Data.Weapons.Where(d=>d.Category==category).ToArray();
        var definition=definitions.FirstOrDefault(d=>d.WeaponId==Selected?.Id)??definitions[0];
        var current=definition.WeaponId==Selected?.Id?kitgun??RangedKitguns.Default(definition):RangedKitguns.Default(definition);
        var chamber=new ComboBox{ItemsSource=definitions,DisplayMemberPath="Chamber",SelectedItem=definition};
        var grip=new ComboBox{ItemsSource=definition.Grips.Keys.ToArray(),SelectedItem=current.Grip};
        var loader=new ComboBox{ItemsSource=definition.Loaders.Keys.ToArray(),SelectedItem=current.Loader};
        var pax=new ComboBox{DisplayMemberPath="Name",SelectedValuePath="Id",ItemsSource=new[]{new ArcaneChoice("None","없음"),new ArcaneChoice("PaxCharge","팍스 차지"),new ArcaneChoice("PaxSoar","팍스 소어"),new ArcaneChoice("PaxBolt","팍스 볼트"),new ArcaneChoice("PaxSeeker","팍스 시커"),new ArcaneChoice("ResidualBoils","레지듀얼 보일스"),new ArcaneChoice("ResidualMalodor","레지듀얼 말로도어"),new ArcaneChoice("ResidualShock","레지듀얼 쇼크"),new ArcaneChoice("ResidualViremia","레지듀얼 바이레미아")},SelectedValue=current.Arcane};
        var rank=new ComboBox{ItemsSource=Enumerable.Range(0,4).ToArray(),SelectedIndex=current.ArcaneRank};
        KitgunAssembly Parts()=>new((string)grip.SelectedItem,(string)loader.SelectedItem,(string)pax.SelectedValue,rank.SelectedIndex);
        var preview=Text("");
        void Update()
        {
            if(grip.SelectedItem is not string g||loader.SelectedItem is not string l)return;
            var w=RangedKitguns.Assemble(definition,Parts());var a=RangedEngine.Attacks(w)[0];
            var timing=Parts().Arcane=="PaxCharge"?$"충전 대기 {RangedEngine.N(w.Raw,"Reload")/(1+.125*(Parts().ArcaneRank+1)):0.##}초 · 초당 {RangedEngine.N(w.Raw,"ReloadRate"):0}발 회복":$"재장전 {RangedEngine.N(w.Raw,"Reload"):0.##}초";
            preview.Text=$"치명타 {RangedEngine.N(a,"CritChance"):P1} · 치명타 배수 {RangedEngine.N(a,"CritMultiplier"):0.##}×\n상태 이상 {RangedEngine.N(a,"StatusChance"):P1}\n탄창 {RangedEngine.N(w.Raw,"Magazine"):0} · {timing}";
        }
        var dialog=new Window{Title="키트건 조립",Width=480,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var owner=Window.GetWindow(this);if(owner is not null){dialog.FontFamily=owner.FontFamily;dialog.FontSize=owner.FontSize;if(owner.IsVisible)dialog.Owner=owner;}
        dialog.SetResourceReference(Control.BackgroundProperty,"SurfaceBrush");dialog.SetResourceReference(Control.ForegroundProperty,"TextBrush");
        var body=Stack(Text(category=="primary"?"주무기 키트건 조립":"보조무기 키트건 조립",20),Text("도금 완료 기준"),Field("챔버",chamber),Field("그립",grip),Field("로더",loader),Field("키트건 아케인",pax),Field("아케인 랭크",rank),Text("팍스 볼트는 발동을 기록하며, 외부 버프의 입력값을 자동으로 바꾸지 않습니다."),preview);
        body.Margin=new(20);var buttons=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};buttons.Children.Add(Button("취소",()=>dialog.Close()));buttons.Children.Add(Button("적용",()=>{var assembly=Parts();weapon.SelectedItem=weapon.Items.Cast<Weapon>().Single(w=>w.Id==definition.WeaponId);kitgun=assembly;var updated=RangedEngine.Resolve(Capture() with{Kitgun=kitgun});var oldAttack=attack.SelectedIndex;loading=true;attack.ItemsSource=RangedEngine.Attacks(updated).Select((a,i)=>$"{i+1}. {RangedEngine.S(a,"AttackName")}").ToArray();attack.SelectedIndex=Math.Min(Math.Max(0,oldAttack),RangedEngine.Attacks(updated).Length-1);loading=false;dialog.Close();RefreshConditionControls();Refresh();}));body.Children.Add(buttons);
        chamber.SelectionChanged+=(_,_)=>{definition=(KitgunDefinition)chamber.SelectedItem;var previousGrip=grip.SelectedItem as string;var previousLoader=loader.SelectedItem as string;grip.ItemsSource=definition.Grips.Keys.ToArray();loader.ItemsSource=definition.Loaders.Keys.ToArray();grip.SelectedItem=previousGrip is not null&&definition.Grips.ContainsKey(previousGrip)?previousGrip:RangedKitguns.Default(definition).Grip;loader.SelectedItem=previousLoader is not null&&definition.Loaders.ContainsKey(previousLoader)?previousLoader:RangedKitguns.Default(definition).Loader;Update();};
        grip.SelectionChanged+=(_,_)=>Update();loader.SelectionChanged+=(_,_)=>Update();pax.SelectionChanged+=(_,_)=>Update();rank.SelectionChanged+=(_,_)=>Update();Update();var surface=new Border{Child=body};surface.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");dialog.Content=surface;return dialog;
    }
}

