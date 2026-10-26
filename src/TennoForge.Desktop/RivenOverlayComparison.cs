using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class RivenOverlay
{
    private static readonly Brush ComparisonAccent=new SolidColorBrush(Color.FromRgb(242,203,87));
    private static TextBlock CompareText(string text,double size=13,Brush? color=null)=>new(){Text=text,FontSize=size,LineHeight=size*1.4,TextWrapping=TextWrapping.Wrap,Foreground=color??Brushes.White,Margin=new(0,0,0,5)};
    private static FrameworkElement BuildComparison(RivenOcrResult result,bool right,bool automatic)
    {
        var panel=new StackPanel{Margin=new(14)};
        panel.Children.Add(CompareText("WF Labs · 리벤 "+(automatic?"자동 인식":"분석"),14,ComparisonAccent));
        panel.Children.Add(CompareText(right?"두 카드 접합 분석":"순환 옵션 비교",20));
        if(right)
        {
            for(int i=0;i<2;i++)
            {
                var card=result.Cards[i];var report=card.Report;
                if(i>0)panel.Children.Add(Divider());
                panel.Children.Add(CompareText(i==0?"왼쪽 옵션":"오른쪽 옵션",16,ComparisonAccent));
                if(!card.Corroborated||!report.Valid){panel.Children.Add(CompareText("인식 확인 필요 · 앱에서 수정",13,Brushes.Salmon));continue;}
                panel.Children.Add(CompareText($"지금 접합 가능 · {report.Recipes.Length}가지",14));
                foreach(var recipe in report.Recipes)
                {
                    panel.Children.Add(CompareText(recipe.Ingredients,12,Brushes.LightGray));
                    panel.Children.Add(CompareRated("→ "+recipe.Result,recipe.MinimumQuality,recipe.MaximumQuality));
                    panel.Children.Add(CompareText($"{recipe.Minimum:0.##}~{recipe.Maximum:0.##} · 대체 {(recipe.Negative?"음옵":"양옵")} {recipe.ReplacementOptions}종",11,Brushes.LightGray));
                }
                bool spliced=report.Options.Any(o=>report.Kind=="Melee"?RivenValues.IsSpliced(Enum.Parse<RivenStat>(o.Key)):RangedRivens.Attribute(o.Key).Spliced);
                if(report.Recipes.Length==0)panel.Children.Add(CompareText(spliced?"이미 접합된 옵션은 재접합을 예측하지 않습니다.":"접합 재료 두 옵션이 함께 없습니다.",12,Brushes.LightGray));
                var future=RivenScan.Opportunities(report);
                if(future.Length>0)
                {
                    panel.Children.Add(CompareText($"옵션 변경 후 가능 · {future.Length}가지",13,ComparisonAccent));
                    foreach(var item in future)
                    {
                        panel.Children.Add(CompareText(item.HeldName+(item.HeldNegative?" (음옵)":"")+" + "+item.MissingName+" 필요",11,Brushes.LightGray));
                        panel.Children.Add(CompareRated("→ "+item.ResultName,item.MinimumQuality,item.MaximumQuality));
                    }
                }
            }
            panel.Children.Add(CompareText("예상 등급: 보유 수율 유지 가정\n부족한 옵션 수치 미정 · 획득 확률 아님",11,Brushes.LightGray));
        }
        else
        {
            var table=new Grid();table.ColumnDefinitions.Add(new(){Width=new(1.05,GridUnitType.Star)});table.ColumnDefinitions.Add(new(){Width=new(1.3,GridUnitType.Star)});table.ColumnDefinitions.Add(new(){Width=new(1.3,GridUnitType.Star)});
            int row=0;
            void Cell(FrameworkElement element,int column)
            {element.Margin=new(column==0?0:12,8,0,6);Grid.SetColumn(element,column);Grid.SetRow(element,row);table.Children.Add(element);}
            table.RowDefinitions.Add(new(){Height=GridLength.Auto});Cell(CompareText("옵션",13,Brushes.LightGray),0);
            for(int i=0;i<2;i++)
            {
                var report=result.Cards[i].Report;var head=new StackPanel();head.Children.Add(CompareText(i==0?"왼쪽 옵션":"오른쪽 옵션",16,ComparisonAccent));
                head.Children.Add(CompareText(report.DetectedKind&&report.KindCandidates.Length!=1?"종류 확인 필요":$"{RivenScan.KindName(report.Kind)} · {report.Rank}랭크",11,Brushes.LightGray));
                if(!result.Cards[i].Corroborated||!report.Valid)head.Children.Add(CompareText("인식 확인 필요",12,Brushes.Salmon));Cell(head,i+1);
            }
            foreach(var option in result.Cards.Take(2).SelectMany(c=>c.Report.Options).DistinctBy(o=>o.Key))
            {
                row++;table.RowDefinitions.Add(new(){Height=GridLength.Auto});Cell(CompareText(option.Name,14),0);
                for(int i=0;i<2;i++)
                {
                    var card=result.Cards[i];var value=card.Report.Values.FirstOrDefault(v=>v.Option.Key==option.Key);var o=value?.Option??card.Report.Options.FirstOrDefault(o=>o.Key==option.Key);
                    if(o is null){Cell(CompareText("—",18,Brushes.Gray),i+1);continue;}
                    var cell=new StackPanel();string shown=$"{(o.Negative?"−":"+")}{o.Value:0.###}{o.Unit}"+(o.Negative?" · 음옵":"");
                    if(card.Corroborated&&card.Report.Valid&&value?.Grade is {} grade)
                    {
                        double quality=RivenRating.Quality(grade.Position,o.Negative);cell.Children.Add(CompareRated(shown,quality,size:17));
                        var meta=new DockPanel();var range=CompareText($"{value.Range.Minimum:0.##}~{value.Range.Maximum:0.##}{o.Unit}",11,Brushes.LightGray);DockPanel.SetDock(range,Dock.Right);meta.Children.Add(range);meta.Children.Add(CompareText($"품질 {quality:P1}",11,Brushes.LightGray));cell.Children.Add(meta);
                        cell.Children.Add(new ProgressBar{Minimum=0,Maximum=1,Value=quality,Height=3,Margin=new(0,0,0,4),Foreground=new SolidColorBrush(RivenGradePresentation.ColorFor(RivenRating.Tier(quality))),Background=new SolidColorBrush(Color.FromRgb(55,57,60))});
                    }
                    else{cell.Children.Add(CompareText(shown,17,ComparisonAccent));cell.Children.Add(CompareText("확인 필요",11,Brushes.Salmon));}
                    Cell(cell,i+1);
                }
                var line=new Border{Height=1,VerticalAlignment=VerticalAlignment.Bottom,Background=new SolidColorBrush(Color.FromRgb(55,57,60))};Grid.SetRow(line,row);Grid.SetColumnSpan(line,3);table.Children.Add(line);
            }
            panel.Children.Add(table);
        }
        panel.Children.Add(CompareText("등급은 수치 품질 · DPS 점수 아님\n음옵은 페널티가 작을수록 높은 등급 · 클릭은 게임으로 통과",11,Brushes.LightGray));
        var view=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,VerticalAlignment=VerticalAlignment.Top,Child=new Border{Width=right?340:700,Child=panel}};
        return new Border{Background=new SolidColorBrush(Color.FromArgb(242,15,17,19)),BorderBrush=new SolidColorBrush(Color.FromRgb(96,87,54)),BorderThickness=new(1),CornerRadius=new(5),Child=view};
    }
    private static Border Divider()=>new(){Height=1,Margin=new(0,8,0,12),Background=new SolidColorBrush(Color.FromRgb(55,57,60))};
    private static FrameworkElement CompareRated(string label,double low,double? high=null,double size=13)
    {
        var row=new DockPanel{Margin=new(0,0,0,4)};var badge=RivenGradePresentation.Badge(low,high);DockPanel.SetDock(badge,Dock.Right);row.Children.Add(badge);row.Children.Add(CompareText(label,size));return row;
    }
}
