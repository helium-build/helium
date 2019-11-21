cd "$PSScriptRoot/../../../../"

$DIR="examples/build/sbt/jdk-8/"
$env:HELIUM_CONTENT_ROOT = $(Get-Location).Path + "/src/Engine/"

if ( Test-Path -Path $DIR/sources/project/ -PathType Container ) { rm -r -fo $DIR/sources/project/ }
if ( Test-Path -Path $DIR/sources/target/ -PathType Container ) { rm -r -fo $DIR/sources/target/ }
if ( Test-Path -Path $DIR/output/ -PathType Container ) { rm -r -fo $DIR/output/ }
mkdir $DIR/output/ | Out-Null

dotnet run -p src\Engine\ -- build $DIR
