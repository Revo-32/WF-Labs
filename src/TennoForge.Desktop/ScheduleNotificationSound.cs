using System.IO;
using System.Media;
using System.Reflection;

namespace TennoForge.Desktop;

internal static class ScheduleNotificationSound
{
    private static readonly SemaphoreSlim gate=new(1,1);
    internal static byte[] Wave(double volume)
    {
        using var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("WFLabs.schedule-notification.wav")!;
        using var buffer=new MemoryStream();source.CopyTo(buffer);var bytes=buffer.ToArray();
        double gain=double.IsFinite(volume)?Math.Clamp(volume,0,1):.65;
        for(int p=12;p+8<=bytes.Length;)
        {
            int size=BitConverter.ToInt32(bytes,p+4);if(size<0||p+8L+size>bytes.Length)throw new InvalidDataException("알림음 형식을 확인하세요.");
            if(System.Text.Encoding.ASCII.GetString(bytes,p,4)=="data")
            {for(int i=p+8;i+1<p+8+size;i+=2){short sample=(short)Math.Round(BitConverter.ToInt16(bytes,i)*gain);bytes[i]=(byte)sample;bytes[i+1]=(byte)(sample>>8);}break;}p+=8+size+(size&1);
        }
        return bytes;
    }
    internal static Task Play(double volume)
    {
        if(volume<=0||Environment.GetEnvironmentVariable("WF_LABS_NONINTERACTIVE")=="1")return Task.CompletedTask;
        return Task.Run(()=>
        {
            if(!gate.Wait(0))return;
            try{using var stream=new MemoryStream(Wave(volume));using var player=new SoundPlayer(stream);player.Load();player.PlaySync();}
            catch(Exception e)when(e is InvalidOperationException or IOException or System.TimeoutException){}
            finally{gate.Release();}
        });
    }
}
