param([Parameter(Mandatory=$true)][string]$Output,[ValidateRange(1,30)][int]$Samples=20)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$destination=[IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory($destination)|Out-Null
# Read-only accessibility observation. No Invoke, activation, injected input or
# screenshots. Bounded and explicitly invoked; never starts with the client.
for($sample=0;$sample -lt $Samples;$sample++){
    $app=Get-Process WFLabs -ErrorAction SilentlyContinue | Select-Object -First 1
    $panels=@()
    if($app){
        $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,[int]$app.Id)
        $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$condition)
        foreach($window in $windows){
            if($window.Current.Name -ne 'WF Labs · 리벤 옵션 분석'){continue}
            $textCondition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text)
            $labels=$window.FindAll([Windows.Automation.TreeScope]::Descendants,$textCondition)
            $panels+=@{handle=$window.Current.NativeWindowHandle;bounds=$window.Current.BoundingRectangle.ToString();labels=@($labels | ForEach-Object {$_.Current.Name})}
        }
    }
    $statusPath=Join-Path $env:LOCALAPPDATA 'WF Labs/riven-capture-status.json'
    $status=if(Test-Path -LiteralPath $statusPath){try{Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json}catch{$null}}else{$null}
    @{utc=[DateTimeOffset]::UtcNow;processId=$app.Id;panels=$panels;status=$status} | ConvertTo-Json -Depth 8 -Compress | Add-Content -LiteralPath (Join-Path $destination 'client.jsonl') -Encoding utf8
    if($sample -lt $Samples-1){Start-Sleep -Milliseconds 1500}
}
