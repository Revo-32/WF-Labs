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
    private void OnComboBoxLoaded(object sender,RoutedEventArgs e)=>UiMotion.ConnectComboBox((System.Windows.Controls.ComboBox)sender);
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        UiMotion.Initialize();
        Theme.Apply(true);
        UiFonts.PersistChanges=!e.Args.Any(a=>a.StartsWith("--")&&(a.Contains("check")||a.Contains("smoke")||a.Contains("preview")||a=="--layout-probe"));
        UiFonts.Apply(UiFonts.PersistChanges?UiFonts.Read().Id:"jamsil");
        if(!UiFonts.PersistChanges&&!e.Args.Contains("--monitor-check"))
            EventManager.RegisterClassHandler(typeof(MainWindow),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,loaded)=>
            {
                // A headless Windows runner can expose a 1024x768 desktop. WPF's
                // native maximum tracking size otherwise silently shrinks our offscreen
                // 1760x920 fixtures. Keep the real HWND at the requested test size;
                // production and physical-monitor checks still obey MonitorWorkArea.
                if(sender is MainWindow window&&ReferenceEquals(loaded.OriginalSource,window))
                {window.MinWidth=Math.Max(1760,window.Width);window.MinHeight=Math.Max(920,window.Height);window.UpdateLayout();}
            }));
        if(e.Args.Length>=2&&e.Args[0] is "--native-file-check" or "--monitor-check" or "--workspace-produce-check" or "--workspace-restore-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);ShutdownMode=ShutdownMode.OnExplicitShutdown;
            var window=new MainWindow{Width=1760,Height=920,Left=-30000,Top=-30000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};
            window.Loaded+=async(_,_)=>{try{await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);object report=e.Args[0] switch{
                "--native-file-check"=>await window.CheckNativeFiles(dir),
                "--monitor-check"=>await NativeSessionChecks.Monitors(window,dir),
                _=>await window.CheckWorkspaceProcess(dir,e.Args[0]=="--workspace-restore-check")};
                File.WriteAllText(Path.Combine(dir,e.Args[0][2..]+".json"),JsonSerializer.Serialize(report,Catalog.Json));window.Close();Shutdown(0);
            }catch(Exception ex){File.WriteAllText(Path.Combine(dir,e.Args[0][2..]+".error.txt"),ex.ToString());Shutdown(1);}};window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--search-interaction-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Width=1760,Height=920,Left=-30000,Top=-30000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};
            window.Loaded+=async(_,_)=>{try{var report=await window.CheckSearchInteractions((element,name,width,height,scale)=>Render(element,Path.Combine(dir,name+".png"),width,height,scale));File.WriteAllText(Path.Combine(dir,"checks.json"),JsonSerializer.Serialize(report,Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}};window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--mod-card-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Width=1760,Height=920,Left=-30000,Top=-30000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};
            window.Loaded+=async(_,_)=>{try{var report=await window.CheckModCardLayouts((element,name,width,height,scale)=>Render(element,Path.Combine(dir,name+".png"),width,height,scale));File.WriteAllText(Path.Combine(dir,"checks.json"),JsonSerializer.Serialize(report,Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}};window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--ranged-workflow-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Width=1760,Height=920,Left=-30000,Top=-30000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};
            window.Loaded+=async(_,_)=>{try{var report=await window.CheckRangedWorkflow((element,name,width,height,scale)=>Render(element,Path.Combine(dir,name+".png"),width,height,scale));File.WriteAllText(Path.Combine(dir,"checks.json"),JsonSerializer.Serialize(report,Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}};window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--approximation-ui-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Width=1760,Height=920,Left=-30000,Top=-30000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};
            window.Loaded+=async(_,_)=>{try{var report=await window.CheckApproximationUx((element,name,scale)=>Render(element,Path.Combine(dir,name+".png"),1760,920,scale));File.WriteAllText(Path.Combine(dir,"checks.json"),JsonSerializer.Serialize(report,Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}};window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--interaction-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Width=1760,Height=920,Left=-30000,Top=-30000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};
            window.Loaded+=async(_,_)=>{try{var report=await window.CheckInteractions((element,name,width,height)=>Render(element,Path.Combine(dir,name+".png"),width,height));File.WriteAllText(Path.Combine(dir,"checks.json"),JsonSerializer.Serialize(report,Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}};window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--weapon-entry-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try{var window=new MainWindow();var result=window.CheckWeaponEntries((element,name,width,height)=>{if(element is Window w)Render(w,Path.Combine(dir,name+".png"),width,height);else Render(element,Path.Combine(dir,name+".png"),width,height);});File.WriteAllText(Path.Combine(dir,"checks.json"),JsonSerializer.Serialize(result,Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--text-smoothing-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try{TextSmoothingChecks.Run(dir);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--typography-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try{
            File.WriteAllText(Path.Combine(dir,"font-check.json"),JsonSerializer.Serialize(TypographyReview.CheckSelection(dir),Catalog.Json));
            foreach(var scale in new[]{1d,1.25,1.5})Render(TypographyReview.Create(),Path.Combine(dir,$"comparison-{scale}.png"),1100,800,scale);
            Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--riven-file-check")
        {
            try{File.WriteAllText(Path.GetFullPath(e.Args[1]),JsonSerializer.Serialize(RangedRivenEditor.CheckFileUx(),Catalog.Json));Shutdown(0);}
            catch(Exception ex){File.WriteAllText(Path.GetFullPath(e.Args[1])+".error",ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--ranged-smoke")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try {var w=new MainWindow();foreach(var dark in new[]{true,false})foreach(var category in new[]{"primary","secondary"})foreach(var scale in new[]{1d,1.25,1.5}){w.SetDarkTheme(dark);w.ShowRanged(category);Render(w,Path.Combine(dir,$"{category}-{dark}-{scale}.png"),1760,920,scale);w.CheckRangedPage(category);}foreach(var dark in new[]{true,false})foreach(var category in new[]{"primary","secondary"})foreach(var details in new[]{false,true}){w.SetDarkTheme(dark);w.ShowRanged(category);w.PrepareRangedMechanicsAudit(category,details);Render(w,Path.Combine(dir,$"mechanics-{category}-{dark}-{details}.png"),1760,920,1);}
            foreach(var dark in new[]{true,false})foreach(var category in new[]{"primary","secondary"}){w.SetDarkTheme(dark);w.ShowRanged(category);w.PrepareRangedMechanicsAudit(category,false);w.PrepareRangedCombatAudit(category);Render(w,Path.Combine(dir,$"combat-{category}-{dark}.png"),1760,920,1);}
            foreach(var dark in new[]{true,false})foreach(var category in new[]{"primary","secondary"}){w.SetDarkTheme(dark);w.ShowRanged(category);w.PrepareRangedRivenAudit(category);Render(w,Path.Combine(dir,$"riven-{category}-{dark}.png"),1760,920,1.25);w.CloseRangedRivenAudit(category);}
            foreach(var dark in new[]{true,false})foreach(var category in new[]{"primary","secondary"}){w.SetDarkTheme(dark);w.PrepareRangedRivenWorkbenchAudit(category);Render(w,Path.Combine(dir,$"workshop-{category}-{dark}.png"),1760,920,1.25);}
            foreach(var dark in new[]{true,false})foreach(var name in new[]{"Azima","ResidualBoils","ResidualMalodor","ResidualShock","ResidualViremia","Simulor","Singularity ally","Nullifier","Perigale","Cedo","Trumna","Zarr","Alternox","Grimoire","Ocucor","Onos","Gaze (Primary)","Rattleguts (Primary)","Catabolyst","Tenet Ferrox","Cantare","Corinth Prime","Kompressa","Kuva Ogris","Synoid Simulor","Grakata","Velox","Stug","Vectis Prime","Vaykor Hek","Rakta Ballistica","Pox","Hema","Dread","Battacor","Kuva Drakgoon","Tenet Tetra","Flux Rifle","Higasa","EFV-5 Jupiter","Quanta","Tenet Quanta","Embolist","Hikou","Evensong","Boltor","Bolto","Proboscis Cernos","Scyotid","Penta","Lato","Mutalist Quanta","Lanka","Zymos","Secura Penta","Prisma Gorgon","Sporelacer (Secondary)","Rattleguts (Secondary)","Vermisplicer (Primary)","Vermisplicer (Secondary)"}){var category=name is "Azima" or "Vermisplicer (Secondary)" or "Rattleguts (Secondary)" or "Sporelacer (Secondary)" or "Zymos" or "Lato" or "Scyotid" or "Bolto" or "Embolist" or "Hikou" or "Grimoire" or "Ocucor" or "Onos" or "Catabolyst" or "Cantare" or "Kompressa" or "Velox" or "Stug" or "Rakta Ballistica" or "Pox"?"secondary":"primary";w.SetDarkTheme(dark);w.ShowRanged(category);w.PrepareRangedSceneAudit(category,name);Render(w,Path.Combine(dir,$"scene-{name}-{dark}.png"),1760,920,1);}
            foreach(var dark in new[]{true,false}){w.SetDarkTheme(dark);w.ShowRanged("primary");w.PrepareRangedSceneAudit("primary","Rattleguts (Primary)");var dialog=w.KitgunDialogForCheck()!;Render(dialog,Path.Combine(dir,$"kitgun-parts-{dark}.png"),420,700);}
            File.WriteAllText(Path.Combine(dir,"ranged-ui.json"),JsonSerializer.Serialize(new{passed=true,states=Directory.EnumerateFiles(dir,"*.png").Count()},Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}return;
        }
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
        if(e.Args.Length>=2&&e.Args[0]=="--popup-layout-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Left=20,Top=20,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false,Opacity=0};
            window.Loaded+=async(_,_)=>{try{var report=await window.CheckPopupLayout();File.WriteAllText(Path.Combine(dir,"popup-check.json"),JsonSerializer.Serialize(report,Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}};window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--font-layout-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try{
                foreach(var font in UiFonts.Choices)foreach(var scale in new[]{1d,1.25,1.5})
                {
                    UiFonts.Apply(font.Id);Theme.Apply(true);
                    var folder=Path.Combine(dir,font.Id,scale.ToString(System.Globalization.CultureInfo.InvariantCulture));Directory.CreateDirectory(folder);
                    var window=new MainWindow();var report=window.ProbeLayouts((element,name,width,height)=>Render(element,Path.Combine(folder,name+".png"),width,height,scale));
                    File.WriteAllText(Path.Combine(folder,"issues.json"),JsonSerializer.Serialize(report,Catalog.Json));
                    if(report.Values.Any(issues=>issues.Length>0))throw new Exception($"{font.Label} {scale:P0}: 레이아웃 겹침 또는 텍스트 잘림 발견");
                }
                Shutdown(0);
            }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--layout-probe")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try{
                foreach(var scale in new[]{1d,1.25,1.5})
                {
                    var window=new MainWindow();var result=window.ProbeLayouts((element,name,width,height)=>Render(element,Path.Combine(dir,name+"-"+scale+".png"),width,height,scale));File.WriteAllText(Path.Combine(dir,"issues-"+scale+".json"),JsonSerializer.Serialize(result,Catalog.Json));
                    if(result.Values.Any(issues=>issues.Length>0))throw new Exception($"{scale:P0} 배율에서 레이아웃 겹침 또는 텍스트 잘림 발견. issues-{scale}.json 확인.");
                }
                Shutdown(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--scenario-flow-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            try{var window=new MainWindow();Render(window,Path.Combine(dir,"closed.png"),1760,920);
                File.WriteAllText(Path.Combine(dir,"enemy.json"),JsonSerializer.Serialize(window.CheckEnemyUx(),Catalog.Json));
                File.WriteAllText(Path.Combine(dir,"combat.json"),JsonSerializer.Serialize(window.CheckCombatUx(),Catalog.Json));
                File.WriteAllText(Path.Combine(dir,"equation.json"),JsonSerializer.Serialize(window.CheckEquationInteractions(),Catalog.Json));
                window.EnemyPreviewForCheck("primer");window.ScrollEncounterControlsForCheck();Render(window,Path.Combine(dir,"primer.png"),1760,920);window.EnemyPreviewForCheck("normal");
                File.WriteAllText(Path.Combine(dir,"layout.json"),JsonSerializer.Serialize(window.CheckModdingLayout("scenario flow"),Catalog.Json));Shutdown(0);
            }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--drawer-check")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Width=1760,Height=920,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
            window.Loaded+=async(_,_)=>{try{File.WriteAllText(Path.Combine(dir,"drawer-check.json"),JsonSerializer.Serialize(await window.CheckScenarioDrawers(),Catalog.Json));Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"error.txt"),ex.ToString());Shutdown(1);}};window.Show();return;
        }
        if(e.Args.Length>=2&&e.Args[0]=="--workflow-smoke")
        {
            var dir=Path.GetFullPath(e.Args[1]);Directory.CreateDirectory(dir);
            var window=new MainWindow{Width=1760,Height=920,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-30000,Top=-30000};
            window.Loaded+=(_,_)=>
            {
                try
                {
                    File.WriteAllText(Path.Combine(dir,"workflow-check.json"),JsonSerializer.Serialize(window.CheckWorkflowUx(),Catalog.Json));
                    foreach(var kind in new[]{"primary","secondary","mod-melee","Rifle","Shotgun","Pistol","Melee"})foreach(var dark in new[]{true,false})foreach(var scale in new[]{1d,1.25,1.5})
                    {
                        window.SetDarkTheme(dark);window.WorkflowPreview(kind);Render(window,Path.Combine(dir,$"{kind}-{dark}-{scale}.png"),1760,920,scale);
                        if(kind is "primary" or "secondary")window.CheckRangedPage(kind);
                        if(kind=="mod-melee")File.WriteAllText(Path.Combine(dir,$"layout-{dark}-{scale}.json"),JsonSerializer.Serialize(window.CheckModdingLayout(kind),Catalog.Json));
                    }
                    Shutdown(0);
                }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"workflow-error.txt"),ex.ToString());Shutdown(1);}
            };window.Show();return;
        }
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
                    foreach(var dark in new[]{true,false})foreach(var scale in new[]{1d,1.5})for(int tab=0;tab<4;tab++){window.SetDarkTheme(dark);window.SelectRivenSectionForCheck(tab);Render(window,Path.Combine(dir,$"melee-{dark}-{scale}-{tab}.png"),1760,920,scale);}
                    foreach(var category in new[]{"primary","secondary"})
                    {
                        window.ShowRanged(category);
                        foreach(var dark in new[]{true,false})foreach(var scale in new[]{1d,1.25,1.5}){window.SetDarkTheme(dark);Render(window,Path.Combine(dir,$"modding-{category}-{dark}-{scale}.png"),1760,920,scale);window.CheckRangedPage(category);}
                        window.PrepareRangedRivenAudit(category);Render(window,Path.Combine(dir,$"modal-{category}.png"),1760,920);window.CloseRangedRivenAudit(category);window.PrepareRangedRivenWorkbenchAudit(category);
                        File.WriteAllText(Path.Combine(dir,$"{category}-workshop-check.json"),JsonSerializer.Serialize(await window.CheckRangedWorkshop(category),Catalog.Json));
                        foreach(var dark in new[]{true,false})foreach(var scale in new[]{1d,1.25,1.5})for(int tab=0;tab<4;tab++){window.SetDarkTheme(dark);window.SelectRangedRivenSectionForCheck(category,tab);Render(window,Path.Combine(dir,$"{category}-{dark}-{scale}-{tab}.png"),1760,920,scale);}
                    }
                    File.WriteAllText(Path.Combine(dir,"ranged-riven-recovery.json"),JsonSerializer.Serialize(window.CheckRangedRivenRecovery(),Catalog.Json));
                    window.ShowModding();window.ShowRivens();window.SetDarkTheme(true);window.SelectRivenSectionForCheck(3);
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
                if (!typeface.TryGetGlyphTypeface(out var glyphs) || !glyphs.CharacterToGlyphMap.ContainsKey('한')) throw new Exception("더잠실체 한글 글리프 확인 실패");
                var p = Process.GetCurrentProcess();
                File.WriteAllText(Path.Combine(dir, "ui-check.json"), JsonSerializer.Serialize(new { passed=true, font= glyphs.FamilyNames.Values.ToArray(), glyphs.FontUri, workingSetMiB=p.WorkingSet64/1048576d, renderTier=RenderCapability.Tier>>16, note="비표시 WPF 레이아웃·상호작용 검사. 실제 게임 병행 성능 측정은 별도 필요." }, Catalog.Json));
                Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(e.Args[1], "ui-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var main=new MainWindow();main.EnableWorkspacePersistence();main.RestoreUpdateRecovery();main.Show();main.StartUpdateChecks();
    }
    private static void Render(Window w, string path,int width=1920,int height=1000,double scale=1)
        =>Render((FrameworkElement)w.Content,path,width,height,scale,w as MainWindow);
    internal static void Render(FrameworkElement root,string path,int width,int height,double scale=1,MainWindow? previewWindow=null)
    {
        var nativePreview=Window.GetWindow(root) is MainWindow {IsVisible:true} host&&ReferenceEquals(host.Content,root);
        if(nativePreview&&Window.GetWindow(root) is {} native)
        {
            // Whole-window previews must measure the requested viewport, not leave
            // a desktop-sized image in the corner of a larger PNG canvas.
            native.MinWidth=width;native.MinHeight=height;
            // A prior larger minimum can clamp an earlier Width assignment while
            // retaining its requested value. Force a fresh native resize after lowering it.
            native.ClearValue(Window.WidthProperty);native.ClearValue(Window.HeightProperty);
            native.Width=width;native.Height=height;
            native.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            native.UpdateLayout();
        }
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
        if(nativePreview&&(Math.Abs(root.ActualWidth-width)>2||Math.Abs(root.ActualHeight-height)>2))
            throw new InvalidOperationException($"검수 창 크기 불일치: 요청 {width}×{height}, 실제 {root.ActualWidth}×{root.ActualHeight}, window={Window.GetWindow(root)?.Width}×{Window.GetWindow(root)?.Height}, min={Window.GetWindow(root)?.MinWidth}×{Window.GetWindow(root)?.MinHeight}, actual={Window.GetWindow(root)?.ActualWidth}×{Window.GetWindow(root)?.ActualHeight}, scale={scale}");
        previewWindow?.SettleScenarioPreviewForCheck();root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(width*scale), (int)(height*scale), 96*scale, 96*scale, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
