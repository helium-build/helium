cd "$PSScriptRoot/../../../../"

$DIR="examples/build/dotnet/2.1/"
$env:HELIUM_CONTENT_ROOT = $(Get-Location).Path + "/src/Engine/"

if ( Test-Path -Path $DIR/sources/bin/ -PathType Container ) { rm -r -fo $DIR/sources/bin/ }
if ( Test-Path -Path $DIR/sources/obj/ -PathType Container ) { rm -r -fo $DIR/sources/obj/ }
if ( Test-Path -Path $DIR/output/ -PathType Container ) { rm -r -fo $DIR/output/ }
mkdir $DIR/output/ | Out-Null

dotnet run -p src\Engine\ -- build $DIR
