# Build from the actual checkout layout; every native command's exit code matters.
param([switch]$Publish)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
 dotnet build SpeakerMesh.csproj -c Release -p:UseSharedCompilation=false
 if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
 dotnet run --project SpeakerMesh.Tests.csproj -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Core verification failed.' }
 if ($Publish) {
  dotnet publish SpeakerMesh.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64 -p:PublishSingleFile=false
  if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
 }
} finally { Pop-Location }
