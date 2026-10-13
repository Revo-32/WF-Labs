using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TennoForge.Core;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hwnd,ref DragPoint point);
    [StructLayout(LayoutKind.Sequential)] private struct DragPoint {public int X,Y;}
    public object CheckNativeDragInput()
    {
        var saved=ReadBuild();var state=new byte[256];GetKeyboardState(state);var held=(byte[])state.Clone();held[1]=0x80;
        var hwnd=new WindowInteropHelper(this).Handle;var steps=new List<object>();
        void Message(FrameworkElement target,Point offset,uint message,bool down)
        {
            var screen=target.PointToScreen(offset);var p=new DragPoint{X=(int)Math.Round(screen.X),Y=(int)Math.Round(screen.Y)};ScreenToClient(hwnd,ref p);
            SetKeyboardState(down?held:state);SendMessage(hwnd,message,new IntPtr(down?1:0),new IntPtr((p.Y<<16)|(p.X&0xffff)));UpdateLayout();
        }
        try
        {
            var source=board.SlotForCheck(0);var initial=board.Slots[0];if(initial is null)throw new Exception("드래그 검증 원본 없음");
            Message(source,new(40,40),0x200,false);Message(source,new(40,40),0x201,true);
            steps.Add(new{step="press",captured=Mouse.Captured is not null,left=Mouse.LeftButton.ToString()});
            Message(source,new(58,55),0x200,true);
            steps.Add(new{step="move",active=board.PointerActiveForCheck,captured=Mouse.Captured is not null});
            if(!board.PointerActiveForCheck)return new{executed=false,steps,note="비활성 화면 밖 HWND는 합성 메시지를 마우스 누름으로 수신하지 않음. 이 시도를 물리 입력 검증 통과로 취급하지 않음."};
            Message(board.LibraryView,new(80,120),0x200,true);
            if(!board.PointerVisibleForCheck)throw new Exception("실제 HWND 드래그 미리보기가 보이지 않음");
            Message(board.LibraryView,new(80,120),0x202,false);
            if(board.Slots[0] is not null||board.PointerActiveForCheck)throw new Exception("실제 HWND 드래그 해제 실패");
            steps.Add(new{step="remove",removed=true,previewClosed=true});
            return new{passed=true,steps,note="화면 밖 실제 HWND에 WM_LBUTTONDOWN/MOUSEMOVE/LBUTTONUP 전달. OS 커서·물리 마우스 이동과는 구분."};
        }
        finally{SetKeyboardState(state);board.RemovalPreviewForCheck(false);Mouse.Capture(null);ApplyBuild(saved);}
    }
}
