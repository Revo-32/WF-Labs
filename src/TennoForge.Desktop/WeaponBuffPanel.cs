using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using TennoForge.Core;

namespace TennoForge.Desktop;

public partial class MainWindow
{
    private readonly CheckBox roarEnabled=new(){Content="로어"},eclipseEnabled=new(){Content="이클립스"};
    private readonly TextBox roarPercent=new(){Text="50"},eclipsePercent=new(){Text="30"};
    private FrameworkElement? weaponBuffPanel;
    private FrameworkElement CreateWeaponBuffPanel()
    {
        var body=new StackPanel();
        var heading=new Grid{Margin=new(0,10,0,6)};
        heading.ColumnDefinitions.Add(new());heading.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        heading.Children.Add(T("받는 버프",15));
        var units=T("실제 증가율 (%)",13);Grid.SetColumn(units,1);heading.Children.Add(units);body.Children.Add(heading);
        void Row(string id,CheckBox enabled,TextBox percent,string help)
        {
            var row=new Grid{Margin=new(0,0,0,6)};
            row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new(112)});
            enabled.VerticalAlignment=VerticalAlignment.Center;enabled.Margin=new(0);row.Children.Add(enabled);
            CompactInput(percent,32);percent.Margin=new(0);percent.TextAlignment=TextAlignment.Right;
            percent.ToolTip=help;enabled.ToolTip=help;Grid.SetColumn(percent,1);row.Children.Add(percent);
            AutomationProperties.SetName(enabled,enabled.Content+" 적용");AutomationProperties.SetName(percent,enabled.Content+" 실제 피해 증가율 (%)");
            void Toggle(){percent.IsEnabled=enabled.IsChecked==true;Recalculate();}
            enabled.Checked+=(_,_)=>Toggle();enabled.Unchecked+=(_,_)=>Toggle();percent.IsEnabled=enabled.IsChecked==true;
            percent.TextChanged+=(_,_)=>{if(enabled.IsChecked==true)Recalculate();};
            WatchEquationSource(row,"buff",()=>(id,enabled.Content.ToString()!));body.Children.Add(row);
        }
        Row("roar",roarEnabled,roarPercent,"게임에 표시된 로어 피해 증가율입니다. 팩션 피해 모드와 더해지며, 상태 피해에는 두 번 적용됩니다.");
        Row("eclipse",eclipseEnabled,eclipsePercent,"솔라 이클립스의 실제 무기 피해 증가율입니다. 어빌리티 위력을 다시 곱하지 않습니다. 가산 컨오 피해에는 곱하지 않고, 컨테이전·글레이브 폭발에는 두 번 적용합니다.");
        var note=T("선택한 교전 동안 유지",13);note.Margin=new(0,0,0,10);body.Children.Add(note);
        return weaponBuffPanel=body;
    }
    private WeaponBuffs? ReadWeaponBuffs()
    {
        var buffs=new WeaponBuffs(roarEnabled.IsChecked==true?Number(roarPercent,"로어 증가율")/100:0,eclipseEnabled.IsChecked==true?Number(eclipsePercent,"이클립스 증가율")/100:0);
        buffs.Validate();return buffs.Active?buffs:null;
    }
    private void ApplyWeaponBuffs(WeaponBuffs? buffs)
    {
        roarEnabled.IsChecked=buffs?.Roar>0;eclipseEnabled.IsChecked=buffs?.Eclipse>0;
        roarPercent.Text=((buffs?.Roar>0?buffs.Roar:.5)*100).ToString(CultureInfo.InvariantCulture);
        eclipsePercent.Text=((buffs?.Eclipse>0?buffs.Eclipse:.3)*100).ToString(CultureInfo.InvariantCulture);
    }
}
