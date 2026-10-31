using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using TennoForge.Core;

namespace TennoForge.Desktop;

// SelectedItem is the committed reward. Suggestions deliberately use a separate selector.
internal sealed class RewardSearchBox : Control
{
    private object? selectedItem;
    private string suggestedText="";
    private object[] items=[];
    private RelicCatalog? searchCatalog;
    public IEnumerable? ItemsSource
    {
        get=>items;
        set{items=value?.Cast<object>().ToArray()??[];searchCatalog=null;if(selectedItem is not null&&!items.Contains(selectedItem))SelectedItem=null;Filter("");}
    }
    public IList Items=>items;
    public Func<RelicItem,bool>? SuggestionFilter {get;set;}
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public object SelectedItem
    {
        get=>selectedItem!;
        set
        {
            if(Equals(selectedItem,value))return;
            var old=selectedItem;selectedItem=value;if(value is not null)suggestedText="";SyncText();
            SelectionChanged?.Invoke(this,new SelectionChangedEventArgs(Selector.SelectionChangedEvent,old is null?Array.Empty<object>():new[]{old},value is null?Array.Empty<object>():new[]{value}));
        }
    }
    public int SelectedIndex{get=>Array.IndexOf(items,selectedItem);set=>SelectedItem=value>=0&&value<items.Length?items[value]:null;}
    public string Text=>(SelectedItem as RelicItem)?.Name??"";
    public string DisplayMemberPath{get;set;}="Name";
    public event SelectionChangedEventHandler? SelectionChanged;
    internal event Action? DropDownClosed;
    public static readonly DependencyProperty IsDropDownOpenProperty=DependencyProperty.Register(nameof(IsDropDownOpen),typeof(bool),typeof(RewardSearchBox),new FrameworkPropertyMetadata(false));
    public bool IsDropDownOpen{get=>(bool)GetValue(IsDropDownOpenProperty);set=>SetValue(IsDropDownOpenProperty,value);}
    private TextBox? query;
    private ListBox? suggestions;
    private TextBlock? empty;
    private bool syncing, composing;
    private Window? owner;
    internal TextBox QueryInput => query!;
    internal ListBox Suggestions => suggestions!;
    internal Popup SearchPopup => (Popup)Template.FindName("PART_Popup", this);
    public RewardSearchBox()
    {
        Style=(Style)Application.Current.FindResource(typeof(Control));MinHeight=38;Focusable=false;
        Template=(ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Control">
          <Grid x:Name="Shell">
            <Border x:Name="SearchBorder" Background="{DynamicResource ControlBrush}" BorderBrush="{DynamicResource StrokeBrush}" BorderThickness="1" CornerRadius="5">
              <Grid>
                <TextBox x:Name="Query" Margin="10,0,32,0" Padding="0" Background="Transparent" VerticalContentAlignment="Center">
                  <TextBox.Template><ControlTemplate TargetType="TextBox"><ScrollViewer x:Name="PART_ContentHost"/></ControlTemplate></TextBox.Template>
                </TextBox>
                <TextBlock x:Name="Placeholder" Text="보상 이름 검색" Margin="10,0,32,0" VerticalAlignment="Center" Foreground="{DynamicResource MutedBrush}" IsHitTestVisible="False" Visibility="Collapsed"/>
                <ToggleButton x:Name="Open" Width="32" HorizontalAlignment="Right" Focusable="False" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="Transparent"><Path x:Name="Chevron" Data="M 0 0 L 4 4 L 8 0" Width="8" Height="4" Stretch="Fill" Stroke="{DynamicResource MutedBrush}" StrokeThickness="1.5" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></ControlTemplate></ToggleButton.Template>
                </ToggleButton>
              </Grid>
            </Border>
            <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}" AllowsTransparency="True" Focusable="False" StaysOpen="True">
              <Border Width="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="360" Background="{DynamicResource PopupBrush}" BorderBrush="{DynamicResource StrokeBrush}" BorderThickness="1" CornerRadius="5" Padding="5" Margin="0,4,0,0">
                <Grid>
                  <ListBox x:Name="Suggestions" Background="Transparent" BorderThickness="0" ScrollViewer.VerticalScrollBarVisibility="Auto" ScrollViewer.HorizontalScrollBarVisibility="Disabled" KeyboardNavigation.TabNavigation="None" Focusable="False">
                    <ListBox.ItemTemplate><DataTemplate><StackPanel><TextBlock Text="{Binding Name}" TextWrapping="Wrap"/><TextBlock Text="{Binding EnglishName}" Foreground="{DynamicResource MutedBrush}" FontSize="12" Margin="0,3,0,0"/></StackPanel></DataTemplate></ListBox.ItemTemplate>
                  </ListBox>
                  <TextBlock x:Name="Empty" Text="검색 결과 없음" Margin="10,12" Foreground="{DynamicResource MutedBrush}" Visibility="Collapsed"/>
                </Grid>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers><MultiTrigger><MultiTrigger.Conditions><Condition SourceName="Query" Property="Text" Value=""/><Condition Property="IsKeyboardFocusWithin" Value="False"/></MultiTrigger.Conditions><Setter TargetName="Placeholder" Property="Visibility" Value="Visible"/></MultiTrigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Shell" Property="Opacity" Value="0.45"/></Trigger><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="SearchBorder" Property="BorderBrush" Value="{DynamicResource AccentBrush}"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        """);
        Loaded+=(_,_)=>
        {
            var window=Window.GetWindow(this);if(owner==window)return;DetachOwner();owner=window;
            if(owner is not null){owner.PreviewMouseDown+=OutsideClick;owner.Deactivated+=OwnerDeactivated;}
        };
        Unloaded+=(_,_)=>{if(!IsVisible){CancelSearch();DetachOwner();}};
    }
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        query=(TextBox)Template.FindName("Query",this);
        suggestions=(ListBox)Template.FindName("Suggestions",this);
        empty=(TextBlock)Template.FindName("Empty",this);
        suggestions.SelectionChanged+=(_,e)=>e.Handled=true;
        query.TextChanged+=(_,_)=>{if(syncing)return;Filter(query.Text);IsDropDownOpen=true;};
        query.GotKeyboardFocus+=(_,_)=>query.SelectAll();
        query.PreviewMouseLeftButtonDown+=(_,e)=>{if(!query.IsKeyboardFocusWithin){query.Focus();query.SelectAll();e.Handled=true;}};
        TextCompositionManager.AddPreviewTextInputStartHandler(query,(_,_)=>composing=true);
        query.AddHandler(TextCompositionManager.TextInputEvent,new TextCompositionEventHandler((_,_)=>composing=false),true);
        query.PreviewKeyDown+=HandleKey;
        suggestions.PreviewMouseLeftButtonDown+=(_,e)=>
        {
            var row=ItemsControl.ContainerFromElement(suggestions,e.OriginalSource as DependencyObject) as ListBoxItem;
            if(row?.Content is RelicItem w){Commit(w);e.Handled=true;}
        };
        ((ToggleButton)Template.FindName("Open",this)).Click+=(_,_)=>{if(IsDropDownOpen){Filter("");query.Focus();query.SelectAll();}};
        var toggle=(ToggleButton)Template.FindName("Open",this);toggle.ApplyTemplate();
        if(toggle.Template.FindName("Chevron",toggle) is FrameworkElement arrow)UiMotion.Attach(SearchPopup,arrow);
        SearchPopup.Closed+=(_,_)=>{composing=false;SyncText();DropDownClosed?.Invoke();};
        SyncText();Filter("");
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Never allow native ComboBox arrow-key or text-search selection.
        if(e.Key is Key.Down or Key.Up or Key.Enter or Key.Escape){HandleKey(this,e);return;}
        if(e.Key==Key.Tab)CancelSearch();
        base.OnPreviewKeyDown(e);
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {e.Handled=true;}
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {base.OnLostKeyboardFocus(e);Dispatcher.BeginInvoke(new Action(()=>{if(query is not null&&!IsKeyboardFocusWithin&&!SearchPopup.IsKeyboardFocusWithin)CancelSearch();}));}
    private void HandleKey(object sender,KeyEventArgs e)
    {
        if(composing||e.Key is Key.ImeProcessed)return;
        if(e.Key is Key.Down or Key.Up)
        {
            if(!IsDropDownOpen){Filter("");IsDropDownOpen=true;}
            var count=suggestions!.Items.Count;
            if(count>0){var i=suggestions.SelectedIndex;suggestions.SelectedIndex=e.Key==Key.Down?Math.Min(i+1,count-1):(i<0?count-1:Math.Max(0,i-1));suggestions.ScrollIntoView(suggestions.SelectedItem);}
            e.Handled=true;
        }
        else if(e.Key==Key.Enter)
        {if(suggestions?.SelectedItem is RelicItem w&&IsDropDownOpen)Commit(w);e.Handled=true;}
        else if(e.Key==Key.Escape){CancelSearch();e.Handled=true;}
    }
    private void OutsideClick(object sender,MouseButtonEventArgs e)
    {if(IsDropDownOpen&&!IsMouseOver&&SearchPopup.Child is UIElement child&&!child.IsMouseOver)CancelSearch();}
    private void OwnerDeactivated(object? sender,EventArgs e)=>CancelSearch();
    private void DetachOwner()
    {if(owner is not null){owner.PreviewMouseDown-=OutsideClick;owner.Deactivated-=OwnerDeactivated;owner=null;}}
    private static string Normalize(string s)=>string.Concat(s.Where(c=>!char.IsWhiteSpace(c)&&c!='-'&&c!='_')).ToUpperInvariant();
    internal void Filter(string text)
    {
        if(suggestions is null)return;
        var q=Normalize(text);
        bool stale=searchCatalog is null||searchCatalog.Items.Length!=items.Length;
        if(!stale)for(int i=0;i<items.Length;i++)if(!ReferenceEquals(items[i],searchCatalog!.Items[i])){stale=true;break;}
        if(stale)searchCatalog=new RelicCatalog(DateTimeOffset.MinValue,items.Cast<RelicItem>().ToArray());
        var catalog=searchCatalog!;
        var matches=q.Length==0?catalog.Items.Take(30).ToArray():catalog.Search(text,30).Select(x=>x.Item).ToArray();
        suggestions.ItemsSource=matches;suggestions.SelectedIndex=-1;
        empty!.Visibility=matches.Length==0?Visibility.Visible:Visibility.Collapsed;
    }
    internal void Suggest(string value){suggestedText=value;ApplyTemplate();syncing=true;try{query!.Text=value;}finally{syncing=false;}Filter(value);}
    internal void OpenSuggestions(){ApplyTemplate();Filter(query!.Text);IsDropDownOpen=true;query.Focus();query.SelectAll();}
    private void SyncText()
    {if(query is null)return;syncing=true;try{query.Text=(SelectedItem as RelicItem)?.Name??suggestedText;}finally{syncing=false;}}
    private void Commit(RelicItem w){SelectedItem=w;IsDropDownOpen=false;SyncText();if(IsVisible){query?.Focus();query?.SelectAll();}}
    internal void CancelSearch(){IsDropDownOpen=false;SyncText();}
}
