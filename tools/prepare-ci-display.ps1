$ErrorActionPreference='Stop'
# Only the disposable GitHub runner may change display mode. Never touch a developer's monitors.
if($env:GITHUB_ACTIONS -ne 'true'){throw 'GitHub Actions 전용 화면 설정입니다. 로컬 모니터에서는 실행하지 않습니다.'}
# DEVMODEW and CDS_TEST: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WfCiDisplay {
    // Explicit DEVMODEW storage retains all driver-provided fields, including ones we do not edit.
    [StructLayout(LayoutKind.Explicit, Size=220)]
    public struct Mode {
        [FieldOffset(68)] public ushort Size;
        [FieldOffset(72)] public uint Fields;
        [FieldOffset(172)] public uint Width;
        [FieldOffset(176)] public uint Height;
    }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="EnumDisplaySettingsW")]
    public static extern bool Read(string device, int index, ref Mode mode);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="ChangeDisplaySettingsExW")]
    public static extern int Change(string device, ref Mode mode, IntPtr hwnd, uint flags, IntPtr data);
    public static Mode Current() {
        var mode=new Mode { Size=220 };
        if(!Read(null,-1,ref mode)) throw new InvalidOperationException("CI 화면 정보를 읽지 못했습니다.");
        return mode;
    }
    public static string Prepare() {
        var before=Current();
        // Covers the widest 2560-DIP preview at 150% and the native 1760 x 920 tests.
        if(before.Width>=3840 && before.Height>=2160) return before.Width+"x"+before.Height+" 유지";
        var target=before; target.Width=3840; target.Height=2160; target.Fields=0x80000|0x100000;
        var test=Change(null,ref target,IntPtr.Zero,2,IntPtr.Zero);
        if(test!=0) throw new InvalidOperationException("CI 3840x2160 모드 지원 검사 실패: "+test+" (현재 "+before.Width+"x"+before.Height+")");
        var applied=Change(null,ref target,IntPtr.Zero,0,IntPtr.Zero);
        if(applied!=0) throw new InvalidOperationException("CI 화면 설정 실패: "+applied);
        var after=Current();
        if(after.Width<3840 || after.Height<2160) throw new InvalidOperationException("CI 화면 크기 적용 누락");
        return before.Width+"x"+before.Height+" → "+after.Width+"x"+after.Height;
    }
}
'@
[WfCiDisplay]::Prepare()
