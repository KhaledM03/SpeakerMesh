# Build and verify with the .NET 9 SDK; no external packages are required.
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
 dotnet restore Source/SpeakerMesh/SpeakerMesh.csproj --ignore-failed-sources
 if ($LASTEXITCODE -ne 0) { throw 'Application restore failed.' }
 dotnet build Source/SpeakerMesh/SpeakerMesh.csproj -c Release --no-restore -p:UseSharedCompilation=false
 if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
 dotnet run --project Source/SpeakerMesh.Tests/SpeakerMesh.Tests.csproj -c Release -p:UseSharedCompilation=false -- --devices --startup-failure
 if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }
} finally { Pop-Location }
