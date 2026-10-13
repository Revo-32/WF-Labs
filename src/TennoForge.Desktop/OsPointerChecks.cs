using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace TennoForge.Desktop;

public sealed partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)] private struct PointerPacket{public int X,Y;public uint Data,Flags,Time;public IntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct InputPacket{public uint Type;public PointerPacket Mouse;}
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,InputPacket[] input,int size);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out DragPoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    internal async Task<object> CheckOsPointerFlow(string capturePath,IntPtr previousWindow)
    {
        var saved=ReadBuild();GetCursorPos(out var original);var checks=new List<string>();var held=false;
        void Button(bool down)
        {var inputs=new[]{new InputPacket{Mouse=new(){Flags=down?2u:4u}}};if(SendInput(1,inputs,Marshal.SizeOf<InputPacket>())!=1)throw new InvalidOperationException("운영체제 마우스 입력 전달 실패");held=down;}
        async Task Move(FrameworkElement target,double x,double y)
        {var p=target.PointToScreen(new Point(x,y));if(!SetCursorPos((int)p.X,(int)p.Y))throw new InvalidOperationException("운영체제 커서 이동 불가");await Task.Delay(90);}
        try
        {
            Activate();await Task.Delay(180);
            if(GetForegroundWindow()!=new WindowInteropHelper(this).Handle)return new{executed=false,note="검증 창이 활성화되지 않아 실제 OS 입력 검사를 진행하지 못했습니다."};
            var card=board.SlotForCheck(0);var slot=board.Slots[0]!;
            await Move(card,25,35);Button(true);await Task.Delay(80);await Move(card,55,55);
            if(!board.PointerActiveForCheck||!board.PointerVisibleForCheck)throw new Exception("운영체제 입력: 장착 모드의 드래그 카드가 보이지 않음");checks.Add("OS 마우스 누름·이동 → 장착 모드의 반투명 카드 표시");
            await Move(board.LibraryView,90,180);
            var hwnd=new WindowInteropHelper(this).Handle;NativeWindowCapture.Save(hwnd,(int)ActualWidth,(int)ActualHeight,capturePath);
            Button(false);await Task.Delay(120);
            if(board.Slots[0] is not null||board.PointerActiveForCheck)throw new Exception("운영체제 입력: 모드 드래그 해제 실패");checks.Add("보관함으로 실제 커서 드롭 → 슬롯 해제·미리보기 종료");
            board.SearchForCheck("Pressure Point");UpdateLayout();
            var source=board.LibraryItemForCheck(slot.ModId);await Move(source,25,35);Button(true);await Task.Delay(80);await Move(source,55,55);
            if(!board.PointerActiveForCheck||!board.PointerVisibleForCheck)throw new Exception("운영체제 입력: 보관함에서 드래그 카드가 보이지 않음");checks.Add("보관함 모드의 반투명 카드 표시");
            await Move(board.SlotForCheck(0),45,45);Button(false);await Task.Delay(120);
            if(board.Slots[0]?.ModId!=slot.ModId||board.PointerActiveForCheck)throw new Exception("운영체제 입력: 모드 장착 실패");checks.Add("실제 커서로 빈 슬롯에 드롭 → 모드 장착·미리보기 종료");
            return new{executed=true,passed=true,checks,note="활성 HWND에서 SendInput·실제 OS 커서 경로 검사. 사람이 물리 마우스로 조작한 검사는 아닙니다."};
        }
        finally{if(held)Button(false);Mouse.Capture(null);board.SearchForCheck("");ApplyBuild(saved);SetCursorPos(original.X,original.Y);if(previousWindow!=IntPtr.Zero)SetForegroundWindow(previousWindow);}
    }
    internal static IntPtr ForegroundForCheck()=>GetForegroundWindow();
}
