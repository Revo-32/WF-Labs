using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TennoForge.Core;

namespace TennoForge.Desktop;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Theme.Apply(true);
        if(e.Args.Length>=2&&e.Args[0]=="--client-update-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);ShutdownMode=ShutdownMode.OnExplicitShutdown;
            Dispatcher.BeginInvoke(async()=>
            {
                try
                {
                    var report=await ClientUpdateChecks.Run((content,name)=>Render(content,Path.Combine(dir,name+".png"),560,660));
                    File.WriteAllText(Path.Combine(dir,"client-update-check.json"),JsonSerializer.Serialize(report,Catalog.Json));
                    var window=new MainWindow();Render(window,Path.Combine(dir,"update-header.png"),1760,920);
                    File.WriteAllText(Path.Combine(dir,"update-navigation-check.json"),JsonSerializer.Serialize(window.CheckUpdateNavigation(),Catalog.Json));
                    Shutdown(0);
                }
                catch(Exception ex){File.WriteAllText(Path.Combine(dir,"client-update-error.txt"),ex.ToString());Shutdown(1);}
            });return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--ui-audit-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try
            {
                var window=new MainWindow();
                foreach(var scale in new[]{1d,1.25,1.5})
                {
                    var report=window.AuditUi((content,name,width,height)=>Render(content,Path.Combine(dir,$"{name}-{scale*100:0}.png"),width,height,scale));
                    File.WriteAllText(Path.Combine(dir,$"ui-audit-{scale*100:0}.json"),JsonSerializer.Serialize(report,Catalog.Json));
                }
                Shutdown(0);
            }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"ui-audit-error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--usability-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try
            {
                var window=new MainWindow();Render(window,Path.Combine(dir,"initial.png"),1760,920);
                File.WriteAllText(Path.Combine(dir,"usability-check.json"),JsonSerializer.Serialize(window.CheckUsabilityFlow(),Catalog.Json));
                File.WriteAllText(Path.Combine(dir,"enemy-ux-check.json"),JsonSerializer.Serialize(window.CheckEnemyUx(),Catalog.Json));
                File.WriteAllText(Path.Combine(dir,"combat-ux-check.json"),JsonSerializer.Serialize(window.CheckCombatUx(),Catalog.Json));
                File.WriteAllText(Path.Combine(dir,"equation-check.json"),JsonSerializer.Serialize(window.CheckEquationInteractions(),Catalog.Json));
                window.ScrollEncounterControlsForCheck();Render(window,Path.Combine(dir,"encounter-controls.png"),1760,920);
                window.ResetModdingScroll();
                window.UsabilityPreview(true);Render(window,Path.Combine(dir,"modding-5s.png"),1760,920);
                File.WriteAllText(Path.Combine(dir,"layout-check.json"),JsonSerializer.Serialize(window.CheckModdingLayout("1760×920"),Catalog.Json));
                window.UsabilityPreview(false);Render(window,Path.Combine(dir,"modding-first-hit.png"),1760,920);
                window.EncounterPreviewForCheck(true);Render(window,Path.Combine(dir,"modding-timeline.png"),1760,920);
                window.UsabilityAnalysisPreview();Render(window,Path.Combine(dir,"modding-analysis.png"),1760,920);
                window.EquationPreviewForCheck("Pressure Point");Render(window,Path.Combine(dir,"modding-formula.png"),1760,920);
                Shutdown(0);
            }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"usability-error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=3&&e.Args[0]=="--installed-update-check"){RunInstalledUpdateCheck(e.Args);return;}
        if(e.Args.Length>=2&&e.Args[0]=="--riven-smoke")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Width=1760,Height=920,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
            window.Loaded+=async(_,_)=>
            {
                try
                {
                    File.WriteAllText(Path.Combine(dir,"riven-workshop-check.json"),JsonSerializer.Serialize(await window.CheckRivenWorkshopUx(),Catalog.Json));
                    Render(window,Path.Combine(dir,"WF-Labs-riven-workshop.png"),1760,920);
                    window.PendingRivenForCheck();Render(window,Path.Combine(dir,"WF-Labs-riven-pending.png"),1760,920);
                    Render(new ClientSettingsWindow(()=>{}),Path.Combine(dir,"WF-Labs-client-settings.png"),560,540);
                    Shutdown(0);
                }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"riven-workshop-error.txt"),ex.ToString());Shutdown(1);}
            };
            window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--expansion-smoke")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try{var window=new MainWindow();window.Measure(new Size(1760,920));window.Arrange(new Rect(0,0,1760,920));window.UpdateLayout();File.WriteAllText(Path.Combine(dir,"expansion-ux-check.json"),JsonSerializer.Serialize(window.CheckExpansionUx(),Catalog.Json));Shutdown(0);}
            catch(Exception ex){File.WriteAllText(Path.Combine(dir,"expansion-error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=3&&e.Args[0]=="--accuracy-preview")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try
            {
                var preview=new MainWindow();
                preview.Measure(new Size(1760,920));preview.Arrange(new Rect(0,0,1760,920));preview.UpdateLayout();
                foreach(var mode in e.Args.Skip(2))
                {
                    preview.AccuracyPreviewForCheck(mode);Render(preview,Path.Combine(dir,"WF-Labs-"+mode+".png"),1760,920);
                    File.WriteAllText(Path.Combine(dir,mode+"-geometry.json"),JsonSerializer.Serialize(preview.EncounterGeometryForCheck(),Catalog.Json));
                    preview.AccuracyPreviewForCheck(null);
                }
                Shutdown(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(dir,"preview-error.txt"),ex.ToString());Shutdown(1);}
            return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--os-pointer-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);var previous=TennoForge.Desktop.MainWindow.ForegroundForCheck();
            var probe=new MainWindow();probe.Loaded+=async(_,_)=>
            {
                try{File.WriteAllText(Path.Combine(dir,"os-pointer-check.json"),JsonSerializer.Serialize(await probe.CheckOsPointerFlow(Path.Combine(dir,"WF-Labs-os-drag.png"),previous),Catalog.Json));Shutdown(0);}
                catch(Exception ex){File.WriteAllText(Path.Combine(dir,"os-pointer-error.txt"),ex.ToString());Shutdown(1);}
            };
            probe.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--font-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            foreach(var scale in new[]{1d,1.25,1.5})
                Render(new FontComparison(),Path.Combine(dir,$"WF-Labs-font-comparison-{scale*100:0}.png"),750,324,scale);
            Shutdown(0);return;
        }
        if (e.Args.Length >= 2 && e.Args[0] is "--smoke" or "--equipment-smoke")
        {
            try
            {
                var dir = Path.GetFullPath(e.Args[1]); Directory.CreateDirectory(dir);
                var window = new MainWindow();
                if(e.Args[0]=="--equipment-smoke")
                {
                    Render(window,Path.Combine(dir,"WF-Labs-equipment-slots.png"),1760,920);
                    File.WriteAllText(Path.Combine(dir,"equipment-ux-check.json"),JsonSerializer.Serialize(window.CheckEquipmentUx(),Catalog.Json));
                    Render(window,Path.Combine(dir,"WF-Labs-equipment-slots.png"),1760,920);
                    File.WriteAllText(Path.Combine(dir,"pointer-drag-check.json"),JsonSerializer.Serialize(window.CheckPointerDragFlow(),Catalog.Json));
                    Render(window,Path.Combine(dir,"WF-Labs-equipment-slots.png"),1760,920);
                    window.RemovalPreviewForCheck(true);Render(window,Path.Combine(dir,"WF-Labs-drag-remove.png"),1760,920);window.RemovalPreviewForCheck(false);
                    File.WriteAllText(Path.Combine(dir,"window-check.json"),JsonSerializer.Serialize(WindowFrameChecks.Run(window),Catalog.Json));Shutdown(0);return;
                }
                window.RunSmokeChecks();
                File.WriteAllText(Path.Combine(dir,"expansion-ux-check.json"),JsonSerializer.Serialize(window.CheckExpansionUx(),Catalog.Json));
                Render(window.CalculationSettingsForCheck(),Path.Combine(dir,"WF-Labs-calculation-settings.png"),300,440);
                Render(window.CombatInputsForCheck(true),Path.Combine(dir,"WF-Labs-combat-inputs.png"),376,420);window.CombatInputsForCheck(false);
                window.AccuracyPreviewForCheck("eximus");Render(window,Path.Combine(dir,"WF-Labs-eximus-stance-5s.png"),1760,920);
                window.AccuracyPreviewForCheck("untimed");Render(window,Path.Combine(dir,"WF-Labs-untimed-zaw-heavy.png"),1760,920);
                window.AccuracyPreviewForCheck("glaive");Render(window,Path.Combine(dir,"WF-Labs-glaive-charged-recall.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("tak");Render(window,Path.Combine(dir,"WF-Labs-tak-heavy.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("spinner");Render(window,Path.Combine(dir,"WF-Labs-spinnerex.png"),1760,920);Render(window.CombatSecondaryForCheck(),Path.Combine(dir,"WF-Labs-spinnerex-secondary.png"),800,130);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("contagion");Render(window.SecondaryDetailsForCheck(),Path.Combine(dir,"WF-Labs-contagion-components.png"),800,590);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("contagion-buffs");Render(window,Path.Combine(dir,"WF-Labs-contagion-buffs.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("contagion-flight");Render(window,Path.Combine(dir,"WF-Labs-contagion-flight.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("vinquibus-shot");Render(window,Path.Combine(dir,"WF-Labs-vinquibus-shot.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("epidemic");Render(window,Path.Combine(dir,"WF-Labs-epidemic.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("glaive-bounce");Render(window,Path.Combine(dir,"WF-Labs-glaive-bounce.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("ichor-cloud");Render(window,Path.Combine(dir,"WF-Labs-ichor-cloud.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("caust-pool");Render(window,Path.Combine(dir,"WF-Labs-caust-pool.png"),1760,920);Render(window.SecondaryDetailsForCheck(),Path.Combine(dir,"WF-Labs-caust-components.png"),880,440);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("okina");Render(window,Path.Combine(dir,"WF-Labs-okina.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("ceramic");Render(window,Path.Combine(dir,"WF-Labs-ceramic.png"),1760,920);window.AccuracyPreviewForCheck(null);
                foreach(var mode in new[]{"destreza-flight","weapon-buffs","atlas-rubble","mirrors-disc","mirrors-glaive","mirrors-wolf","mirrors-shield","mirrors-vinquibus","mirrors","mirrors-flight","jetstream-clock","metronome","opera-flight","warcry","external-daikyu","external-argonak","external-jetstream","keratinos-source","keratinos-miss","jat-kusar","jat-kusar-component","vinquibus-flight","sampotes-components","exec-components","azothane-components","discharge-components","pathocyst-components"}){window.AccuracyPreviewForCheck(mode);Render(window,Path.Combine(dir,"WF-Labs-"+mode+".png"),1760,920);window.AccuracyPreviewForCheck(null);}
                window.AccuracyPreviewForCheck("warfan-slam");Render(window,Path.Combine(dir,"WF-Labs-warfan-slam.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("weapon-arum");Render(window,Path.Combine(dir,"WF-Labs-weapon-arum.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("weapon-nepheri");Render(window,Path.Combine(dir,"WF-Labs-weapon-nepheri.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("weapon-agendus");Render(window,Path.Combine(dir,"WF-Labs-weapon-agendus.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("weapon-grigori");Render(window,Path.Combine(dir,"WF-Labs-weapon-grigori.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("weapon-quassus");Render(window,Path.Combine(dir,"WF-Labs-weapon-quassus.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("weapon-edun");Render(window,Path.Combine(dir,"WF-Labs-weapon-edun.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("weapon-korumm");Render(window,Path.Combine(dir,"WF-Labs-weapon-korumm.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("cobra-sleep");Render(window,Path.Combine(dir,"WF-Labs-cobra-sleep.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("shield-flight");Render(window,Path.Combine(dir,"WF-Labs-shield-flight.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("zenistar-healing");Render(window,Path.Combine(dir,"WF-Labs-zenistar-healing.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("zenistar");Render(window,Path.Combine(dir,"WF-Labs-zenistar.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("orvius");Render(window,Path.Combine(dir,"WF-Labs-orvius.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("glaive-flight");Render(window,Path.Combine(dir,"WF-Labs-glaive-flight.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("blade-blind");Render(window,Path.Combine(dir,"WF-Labs-blade-blind.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("wind-control");Render(window,Path.Combine(dir,"WF-Labs-wind-control.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("wolf-sledge");Render(window,Path.Combine(dir,"WF-Labs-wolf-sledge.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("ack-shared");Render(window,Path.Combine(dir,"WF-Labs-ack-shared.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("diwata-exilus");Render(window,Path.Combine(dir,"WF-Labs-diwata-exilus.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("exalted-wave-flight");Render(window,Path.Combine(dir,"WF-Labs-exalted-wave-flight.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("vitrica-glass");Render(window,Path.Combine(dir,"WF-Labs-vitrica-glass.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("innodem-flight");Render(window,Path.Combine(dir,"WF-Labs-innodem-flight.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("incarnon-blade");Render(window,Path.Combine(dir,"WF-Labs-incarnon-blade.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("magus-aggress");Render(window,Path.Combine(dir,"WF-Labs-magus-aggress.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("rakta");Render(window,Path.Combine(dir,"WF-Labs-rakta-shield.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("stealth");Render(window,Path.Combine(dir,"WF-Labs-stealth.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("vitrica");Render(window.SecondaryDetailsForCheck(),Path.Combine(dir,"WF-Labs-vitrica-components.png"),800,520);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("hirudo");Render(window,Path.Combine(dir,"WF-Labs-hirudo.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("finisher");Render(window,Path.Combine(dir,"WF-Labs-finisher.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("killbuffs");Render(window,Path.Combine(dir,"WF-Labs-kill-buffs.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("thalys");Render(window.SecondaryDetailsForCheck(),Path.Combine(dir,"WF-Labs-thalys-components.png"),800,620);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("brave");Render(window,Path.Combine(dir,"WF-Labs-exodia-brave.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("fortification");Render(window,Path.Combine(dir,"WF-Labs-fortification.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("obex");Render(window,Path.Combine(dir,"WF-Labs-obex-parkour.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("lifestrike");Render(window,Path.Combine(dir,"WF-Labs-life-strike.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("animosity");Render(window,Path.Combine(dir,"WF-Labs-animosity.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("harmony");Render(window,Path.Combine(dir,"WF-Labs-harmony-buffs.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("healingreturn");Render(window,Path.Combine(dir,"WF-Labs-healing-return.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("sancti");Render(window,Path.Combine(dir,"WF-Labs-sancti-healing.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("obexwave");Render(window.SecondaryDetailsForCheck(),Path.Combine(dir,"WF-Labs-obex-finisher-waves.png"),800,560);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("firstrecovery");Render(window,Path.Combine(dir,"WF-Labs-first-contact-recovery.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("scene");Render(window,Path.Combine(dir,"WF-Labs-multiple-targets.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("framearmor");Render(window,Path.Combine(dir,"WF-Labs-warframe-armor.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("tatsu");Render(window,Path.Combine(dir,"WF-Labs-tatsu-souls.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("tennokai");Render(window,Path.Combine(dir,"WF-Labs-tennokai-inputs.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("target-debuffs");Render(window,Path.Combine(dir,"WF-Labs-target-debuffs.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("whip-dome");Render(window,Path.Combine(dir,"WF-Labs-whip-dome.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("energy-channel");Render(window,Path.Combine(dir,"WF-Labs-energy-channel.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("eruption");Render(window,Path.Combine(dir,"WF-Labs-eruption.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("enraged-lifetime");Render(window,Path.Combine(dir,"WF-Labs-enraged-lifetime.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("amanata");Render(window,Path.Combine(dir,"WF-Labs-amanata.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("ackbrunt");Render(window,Path.Combine(dir,"WF-Labs-ackbrunt.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("fallout");Render(window,Path.Combine(dir,"WF-Labs-fallout.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("truculence");Render(window,Path.Combine(dir,"WF-Labs-truculence.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("exalted-augment");Render(window,Path.Combine(dir,"WF-Labs-exalted-augment.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("exalted");Render(window,Path.Combine(dir,"WF-Labs-exalted.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("vinquibus");Render(window,Path.Combine(dir,"WF-Labs-vinquibus.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("residual");Render(window,Path.Combine(dir,"WF-Labs-residual-arcanes.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("incarnonclock");Render(window,Path.Combine(dir,"WF-Labs-incarnon-clock.png"),1760,920);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("destreza");Render(window.SecondaryDetailsForCheck(),Path.Combine(dir,"WF-Labs-destreza-projectiles.png"),880,700);window.AccuracyPreviewForCheck(null);
                window.AccuracyPreviewForCheck("might");Render(window,Path.Combine(dir,"WF-Labs-exodia-might.png"),1760,920);window.AccuracyPreviewForCheck(null);
                File.WriteAllText(Path.Combine(dir,"interaction-check.json"),JsonSerializer.Serialize(window.CheckInteractions(),Catalog.Json));
                Render(window.DetailPreviewForCheck("finishes"),Path.Combine(dir,"WF-Labs-mod-finishes.png"),800,252);
                Render(new System.Windows.Controls.ToolTip{Content=CursorTooltip.TextContent("충격\n비치명 피해 413.8\n전체의 2.05%")},Path.Combine(dir,"WF-Labs-tooltip.png"),232,116);
                Render(window.DetailPreviewForCheck("ascending"),Path.Combine(dir,"WF-Labs-sort-ascending.png"),180,66);
                Render(window.DetailPreviewForCheck("descending"),Path.Combine(dir,"WF-Labs-sort-descending.png"),180,66);
                Render(window.DetailPreviewForCheck("ascending"),Path.Combine(dir,"WF-Labs-sort-125.png"),180,66,1.25);
                Render(window.DetailPreviewForCheck("ascending"),Path.Combine(dir,"WF-Labs-sort-150.png"),180,66,1.5);
                Render(window.TypographyForCheck(),Path.Combine(dir,"WF-Labs-typography.png"),900,270);
                Render(window.TypographyForCheck(),Path.Combine(dir,"WF-Labs-typography-150.png"),900,270,1.5);
                foreach(var scale in new[]{1d,1.25,1.5})
                    Render(new FontComparison(),Path.Combine(dir,$"WF-Labs-font-comparison-{scale*100:0}.png"),750,324,scale);
                window.ResetLibraryOrderForCheck();
                window.SettleSimulationForPreview();
                Render(window, Path.Combine(dir, "WF-Labs-modding-dark.png"));
                File.WriteAllText(Path.Combine(dir,"equipment-ux-check.json"),JsonSerializer.Serialize(window.CheckEquipmentUx(),Catalog.Json));
                Render(window.EquipmentPickerForCheck(),Path.Combine(dir,"WF-Labs-arcane-picker.png"),420,450);window.CloseEquipmentPickerForCheck();
                File.WriteAllText(Path.Combine(dir,"pointer-drag-check.json"),JsonSerializer.Serialize(window.CheckPointerDragFlow(),Catalog.Json));
                Render(window,Path.Combine(dir,"WF-Labs-equipment-slots.png"),1760,920);
                window.RemovalPreviewForCheck(true);Render(window,Path.Combine(dir,"WF-Labs-drag-remove.png"),1760,920);window.RemovalPreviewForCheck(false);
                File.WriteAllText(Path.Combine(dir,"enemy-ux-check.json"),JsonSerializer.Serialize(window.CheckEnemyUx(),Catalog.Json));
                window.EnemyPreviewForCheck("ice");Render(window,Path.Combine(dir,"WF-Labs-target-icebind.png"),1760,920);
                window.EnemyPreviewForCheck("steel");Render(window,Path.Combine(dir,"WF-Labs-target-steel-path.png"),1760,920,1.5);
                window.EnemyPreviewForCheck("primer");Render(window,Path.Combine(dir,"WF-Labs-target-primer.png"),1760,920);
                window.ScrollEncounterControlsForCheck();Render(window,Path.Combine(dir,"WF-Labs-target-primer.png"),1760,920);
                window.EnemyPreviewForCheck("normal");Render(window,Path.Combine(dir,"WF-Labs-modding-dark.png"));
                window.EnemyPreviewForCheck("eximus");Render(window,Path.Combine(dir,"WF-Labs-target-eximus.png"),1760,920);window.EnemyPreviewForCheck("normal");
                File.WriteAllText(Path.Combine(dir,"combat-ux-check.json"),JsonSerializer.Serialize(window.CheckCombatUx(),Catalog.Json));
                // Restoring the UX fixture recreates rack cards. Arrange those new
                // elements before taking the same snapshot used by a real drag.
                Render(window,Path.Combine(dir,"WF-Labs-modding-dark.png"));
                window.DragPreviewForCheck(true);Render(window,Path.Combine(dir,"WF-Labs-drag-preview.png"));File.WriteAllText(Path.Combine(dir,"drag-geometry.json"),JsonSerializer.Serialize(window.DragGeometryForCheck(Path.Combine(dir,"drag-snapshot.png")),Catalog.Json));window.DragPreviewForCheck(false);
                window.EncounterPreviewForCheck(true);Render(window,Path.Combine(dir,"WF-Labs-encounter-5s.png"));window.EncounterPreviewForCheck(false);
                window.EncounterPreviewForCheck(true);Render(window,Path.Combine(dir,"WF-Labs-combat-history.png"));window.ScrollCombatHistoryForCheck();Render(window,Path.Combine(dir,"WF-Labs-combat-history.png"),1760,920);window.EncounterPreviewForCheck(false);
                window.StanceEncounterPreviewForCheck(true);Render(window,Path.Combine(dir,"WF-Labs-encounter-stance.png"));window.StanceEncounterPreviewForCheck(false);
                File.WriteAllText(Path.Combine(dir,"equation-check.json"),JsonSerializer.Serialize(window.CheckEquationInteractions(),Catalog.Json));
                window.SettleSimulationForPreview();
                window.EquationPreviewForCheck("Pressure Point");Render(window,Path.Combine(dir,"WF-Labs-equation-damage.png"));
                window.EquationPreviewForCheck("stance");Render(window,Path.Combine(dir,"WF-Labs-equation-stance-150.png"),1760,920,1.5);
                window.EquationPreviewForCheck(null);Render(window,Path.Combine(dir,"WF-Labs-modding-dark.png"));
                var layoutChecks=new List<object>{window.CheckModdingLayout("1920×1000")};
                window.ArcanePreviewForCheck(true);window.SettleSimulationForPreview();Render(window,Path.Combine(dir,"WF-Labs-arcanes.png"));window.ArcanePreviewForCheck(false);window.SettleSimulationForPreview();
                window.CatalogPreviewForCheck(0);Render(window,Path.Combine(dir,"WF-Labs-full-catalog.png"));
                window.CatalogPreviewForCheck(2);Render(window,Path.Combine(dir,"WF-Labs-arcane-catalog.png"));window.ShowModding();
                window.ScrollChartsForCheck();Render(window,Path.Combine(dir,"WF-Labs-charts-dark.png"));
                Render(window,Path.Combine(dir,"WF-Labs-charts-compact.png"),1760,920);window.ScrollChartsForCheck();
                Render(window,Path.Combine(dir,"WF-Labs-charts-compact.png"),1760,920);
                Render(window,Path.Combine(dir,"WF-Labs-charts-150.png"),1760,920,1.5);window.ScrollChartsForCheck();
                Render(window,Path.Combine(dir,"WF-Labs-charts-150.png"),1760,920,1.5);window.ResetChartsScroll();
                window.FormaForCheck(true);Render(window,Path.Combine(dir,"WF-Labs-forma.png"));window.FormaForCheck(false);
                Render(window.ModCardForCheck(), Path.Combine(dir, "WF-Labs-mod-card.png"),160,172);
                Render(window.ModCardForCheck(),Path.Combine(dir,"WF-Labs-mod-card-150.png"),160,172,1.5);
                Render(window.LibraryDetailsForCheck(),Path.Combine(dir,"WF-Labs-library-details.png"),512,220);
                window.SettleSimulationForPreview();
                Render(window, Path.Combine(dir, "WF-Labs-modding-compact.png"),1760,920);
                layoutChecks.Add(window.CheckModdingLayout("1760×920 · 최소 창"));
                window.CheckCompactScroll();
                Render(window, Path.Combine(dir, "WF-Labs-modding-compact-library.png"),1760,920);
                layoutChecks.Add(window.CheckModdingLayout("1760×920 · 보관함 끝 스크롤"));
                window.ResetModdingScroll();
                Render(window,Path.Combine(dir,"WF-Labs-modding-1366.png"),1840,940);layoutChecks.Add(window.CheckModdingLayout("1840×940"));
                Render(window,Path.Combine(dir,"WF-Labs-modding-125.png"),1920,960,1.25);layoutChecks.Add(window.CheckModdingLayout("1920×960 · 125% 이미지"));
                Render(window,Path.Combine(dir,"WF-Labs-modding-150.png"),1760,920,1.5);layoutChecks.Add(window.CheckModdingLayout("1760×920 · 150% 이미지"));
                Render(window,Path.Combine(dir,"WF-Labs-modding-wide.png"),2560,1080);layoutChecks.Add(window.CheckModdingLayout("2560×1080"));
                File.WriteAllText(Path.Combine(dir,"layout-check.json"),JsonSerializer.Serialize(new{passed=true,cases=layoutChecks},Catalog.Json));
                Render(window.StanceForCheck(), Path.Combine(dir, "WF-Labs-stance.png"),840,760);
                window.ShowRivens(); Render(window, Path.Combine(dir, "WF-Labs-rivens-dark.png"));
                window.ShowModding(); window.SetDarkTheme(false); Render(window, Path.Combine(dir, "WF-Labs-modding-light.png"));
                window.SetDarkTheme(true); window.SetLightMode(true); Render(window, Path.Combine(dir, "WF-Labs-performance.png"));
                File.WriteAllText(Path.Combine(dir, "window-check.json"), JsonSerializer.Serialize(WindowFrameChecks.Run(window,Path.Combine(dir,"WF-Labs-native-window.png")), Catalog.Json));
                var typeface = new Typeface(window.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                if (!typeface.TryGetGlyphTypeface(out var glyphs) || !glyphs.CharacterToGlyphMap.ContainsKey('한')) throw new Exception("Wanted Sans 한글 글리프 확인 실패");
                var p = Process.GetCurrentProcess();
                File.WriteAllText(Path.Combine(dir, "ui-check.json"), JsonSerializer.Serialize(new { passed=true, font= glyphs.FamilyNames.Values.ToArray(), glyphs.FontUri, workingSetMiB=p.WorkingSet64/1048576d, renderTier=RenderCapability.Tier>>16, note="비표시 WPF 레이아웃·상호작용 검사. 실제 게임 병행 성능 측정은 별도 필요." }, Catalog.Json));
                Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(e.Args[1], "ui-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var main=new MainWindow();main.RestoreUpdateRecovery();main.Show();main.StartUpdateChecks();
    }
    private static void Render(Window w, string path,int width=1920,int height=1000,double scale=1)
        =>Render((FrameworkElement)w.Content,path,width,height,scale,w as MainWindow);
    private static void Render(FrameworkElement root,string path,int width,int height,double scale=1,MainWindow? previewWindow=null)
    {
        Visual visualRoot=root;
        while(VisualTreeHelper.GetParent(visualRoot) is Visual parent)visualRoot=parent;
        VisualTreeHelper.SetRootDpi(visualRoot,new DpiScale(scale,scale));
        TextRendering.Apply(root,new DpiScale(scale,scale));
        void Invalidate(DependencyObject node)
        {
            if(node is UIElement element){element.InvalidateMeasure();element.InvalidateArrange();element.InvalidateVisual();}
            for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Invalidate(VisualTreeHelper.GetChild(node,i));
        }
        Invalidate(root);
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        previewWindow?.SettleScenarioPreviewForCheck();root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(width*scale), (int)(height*scale), 96*scale, 96*scale, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
