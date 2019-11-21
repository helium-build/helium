cd "$PSScriptRoot/../../../../"

$DIR="examples/build/node/12/"

if ( Test-Path -Path $DIR/src/bin/ -PathType Container ) { rm -r -fo $DIR/src/bin/ }
if ( Test-Path -Path $DIR/src/bin/ -PathType Container ) { rm -r -fo $DIR/src/node_modules/ }
if ( Test-Path -Path $DIR/output/ -PathType Container ) { rm -r -fo $DIR/output/ }
mkdir $DIR/output/ | Out-Null

java -jar target/scala-2.13/Helium-assembly-0.1.0-SNAPSHOT.jar build $DIR
