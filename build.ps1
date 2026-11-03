Set-Location $PSScriptRoot

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$framework = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"

$sources = @(
    "AudioInterop.cs",
    "Models.cs",
    "AudioEngine.cs",
    "QuickFlyoutForm.cs",
    "MainForm.cs",
    "PrtScnRemapper.cs",
    "Program.cs"
)

$refs = @(
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:$framework\System.Web.Extensions.dll"
)

$args = @(
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/win32icon:soundflow.ico",
    "/out:SoundFlow.exe"
) + $refs + $sources

Write-Host "Compilando SoundFlow.exe nativo (WinForms ultra-ligero)..." -ForegroundColor Cyan
& $csc $args

if ($LASTEXITCODE -eq 0) {
    Write-Host "Compilacion exitosa: SoundFlow.exe generado con exito!" -ForegroundColor Green
} else {
    Write-Host "Error en la compilacion." -ForegroundColor Red
}
