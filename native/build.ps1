
$ErrorActionPreference = "Stop"

$nativeDir = $PSScriptRoot

Write-Host "Building rental_native.dll..."

Push-Location $nativeDir

try {
    & cl.exe /nologo /LD /TC rental_native.c /Fe:rental_native.dll

    if ($LASTEXITCODE -ne 0) {
        throw "Native C compilation failed."
    }

    Write-Host "Native DLL built successfully!"
}
finally {
    Pop-Location
}
