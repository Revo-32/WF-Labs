using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TennoForge.Core;

namespace TennoForge.Desktop;

internal sealed partial class RangedPage
{
    private RangedDragSession? pointerDrag;
    private ScrollViewer libraryScroller=null!;
    private void Drag(Button button,string format,object value)
    {
        Point start=default;
        button.PreviewMouseLeftButtonDown+=(_,e)=>start=e.GetPosition(button);
        button.PreviewMouseMove+=(_,e)=>
        {
            if(pointerDrag is not null||e.LeftButton!=MouseButtonState.Pressed||(e.GetPosition(button)-start).Length<8)return;
            BeginDrag(button,format=="WFLabs.RangedSlot"?(int)value:null,format=="WFLabs.RangedMod"?(string)value:null);e.Handled=true;
        };
    }
    private void BeginDrag(Button source,int? slot,string? modId)
    {
        pointerDrag?.Dispose();
        if(Window.GetWindow(source)?.Content is not FrameworkElement host)return;
        var appearance=new Button{Padding=source.Padding,BorderThickness=source.BorderThickness};
        if(source.Tag is ValueTuple<RangedMod,int> data)Decorate(appearance,data.Item1,data.Item2,source.Content is ModCardFace face&&int.TryParse(face.CostText,out var cost)?cost:null,source.Content is ModCardFace{LibraryCard:true});
        pointerDrag=new(this,source,host,appearance,slot,modId);
    }
    private Button SlotCard(int index)=>(Button)((StackPanel)slotsPanel.Children[index]).Children[0];
    private void CommitDrop(int? source,string? modId,int? target,bool remove)
    {
        if(target is {} index)
        {
            if(source is {} from)(slots[index],slots[from])=(slots[from],slots[index]);
            else if(modId is not null)
            {
                var mod=RangedEngine.ModCatalog.Mods.Single(m=>m.Id==modId);
                if(RangedRivens.IsRiven(mod)){active=index;OpenRiven();return;}
                var existing=Array.FindIndex(slots,s=>s?.ModId==modId);
                if(existing>=0)(slots[index],slots[existing])=(slots[existing],slots[index]);
                else slots[index]=new(mod.Id,mod.MaxRank);
            }
            active=index;
        }
        else if(remove&&source is {} from)slots[from]=null;
        else return;
        RefreshConditionControls();Refresh();
    }
    private sealed class RangedDragSession : IDisposable
    {
        private readonly RangedPage page;private readonly FrameworkElement host;private readonly Window? owner;
        private readonly ModDragPreview? preview;private readonly int? source;private readonly string? mod;
        private int? target;private bool remove,disposed;
        internal FrameworkElement Host=>host;
        internal bool Visible=>preview is {Visibility:Visibility.Visible,VisibleSnapshotPixels:>1000};
        internal RangedDragSession(RangedPage page,Button card,FrameworkElement host,Button appearance,int? source,string? mod)
        {
            this.page=page;this.host=host;this.source=source;this.mod=mod;owner=Window.GetWindow(card);
            preview=ModDragPreview.Start(card,appearance);
            host.PreviewMouseMove+=Move;host.PreviewMouseLeftButtonUp+=Up;host.PreviewKeyDown+=Key;host.LostMouseCapture+=Lost;
            if(owner is not null){owner.Deactivated+=Deactivate;owner.Closed+=Deactivate;}
            Mouse.Capture(host,CaptureMode.Element);host.Focus();Update(Mouse.GetPosition(host));
        }
        private void Move(object sender,MouseEventArgs e){Update(e.GetPosition(host));e.Handled=true;}
        private void Up(object sender,MouseButtonEventArgs e){Update(e.GetPosition(host));Finish();e.Handled=true;}
        private void Key(object sender,KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape){Dispose();e.Handled=true;}}
        private void Lost(object sender,MouseEventArgs e){if(Mouse.Captured!=host)Dispose();}
        private void Deactivate(object? sender,EventArgs e)=>Dispose();
        internal void Update(Point position)
        {
            if(disposed)return;preview?.MoveFrom(host,position);target=null;remove=false;
            for(int i=0;i<8;i++)
            {
                var card=page.SlotCard(i);RestoreBorder(card,i);
                if(new Rect(card.RenderSize).Contains(host.TranslatePoint(position,card))){target=i;card.SetResourceReference(Control.BorderBrushProperty,"AccentBrush");card.BorderThickness=new(1);}
            }
            remove=target is null&&source is not null&&new Rect(page.libraryScroller.RenderSize).Contains(host.TranslatePoint(position,page.libraryScroller));
            if(remove)page.libraryScroller.SetResourceReference(Control.BackgroundProperty,"AccentSurfaceBrush");else page.libraryScroller.ClearValue(Control.BackgroundProperty);
            host.Cursor=target is not null||remove?Cursors.Hand:Cursors.No;
        }
        private void RestoreBorder(Button card,int index)
        {
            card.SetResourceReference(Control.BorderBrushProperty,index==page.active?"AccentBrush":card.Tag is ValueTuple<RangedMod,int> data?ModAppearance.StrokeKey(ModAppearance.ForId(data.Item1.Id)):"StrokeBrush");
            card.BorderThickness=new(1);
        }
        internal void Finish(){if(disposed)return;var slot=target;var erase=remove;Dispose();page.CommitDrop(source,mod,slot,erase);}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            host.PreviewMouseMove-=Move;host.PreviewMouseLeftButtonUp-=Up;host.PreviewKeyDown-=Key;host.LostMouseCapture-=Lost;
            if(owner is not null){owner.Deactivated-=Deactivate;owner.Closed-=Deactivate;}
            if(Mouse.Captured==host)Mouse.Capture(null);host.ClearValue(CursorProperty);preview?.Dispose();page.libraryScroller.ClearValue(Control.BackgroundProperty);
            for(int i=0;i<8;i++)RestoreBorder(page.SlotCard(i),i);
            if(page.pointerDrag==this)page.pointerDrag=null;
        }
    }
    private void CheckDragFlow()
    {
        var saved=Capture();
        try
        {
            Array.Clear(slots);Refresh();UpdateLayout();
            var card=library.Children.OfType<Button>().First(b=>b.IsEnabled&&b.Tag is ValueTuple<RangedMod,int> d&&!RangedRivens.IsRiven(d.Item1));var mod=((ValueTuple<RangedMod,int>)card.Tag).Item1;
            BeginDrag(card,null,mod.Id);var session=pointerDrag??throw new Exception("총기 드래그 시작 실패");
            session.Update(SlotCard(0).TranslatePoint(new(20,20),session.Host));if(!session.Visible)throw new Exception("총기 장착 미리보기 픽셀 누락");if(SlotCard(0).BorderThickness!=new Thickness(1))throw new Exception("드래그 대상의 테두리가 내용 위치를 변경");session.Finish();UpdateLayout();
            if(slots[0]?.ModId!=mod.Id)throw new Exception("총기 드래그 장착 실패");
            BeginDrag(SlotCard(0),0,null);session=pointerDrag!;session.Update(SlotCard(1).TranslatePoint(new(20,20),session.Host));session.Finish();UpdateLayout();
            if(slots[0] is not null||slots[1]?.ModId!=mod.Id)throw new Exception("총기 슬롯 이동 실패");
            BeginDrag(SlotCard(1),1,null);session=pointerDrag!;session.Update(libraryScroller.TranslatePoint(new(25,25),session.Host));if(!session.Visible)throw new Exception("총기 해제 미리보기 누락");if(!ReferenceEquals(libraryScroller.Background,Application.Current.FindResource("AccentSurfaceBrush")))throw new Exception("드래그 해제 강조가 현재 테마와 불일치");session.Finish();UpdateLayout();
            if(slots.Any(s=>s is not null))throw new Exception("총기 보관함 드래그 해제 실패");
            card=library.Children.OfType<Button>().First(b=>b.Tag is ValueTuple<RangedMod,int> d&&d.Item1.Id==mod.Id);BeginDrag(card,null,mod.Id);session=pointerDrag!;session.Update(new(-10,-10));session.Finish();
            if(slots.Any(s=>s is not null))throw new Exception("총기 영역 밖 드롭이 상태를 변경");
        }
        finally{pointerDrag?.Dispose();Restore(saved);UpdateLayout();}
    }
}
