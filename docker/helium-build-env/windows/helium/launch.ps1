
$env:Path += ";$env:HELIUM_SDK_PATH"

$proxy = Start-Process C:/helium/LocalhostToUnix/Helium.LocalhostToUnix.exe -ArgumentList C:/helium/socket/helium.sock,9000 -PassThru

if ( Test-Path -Path 'C:\helium\install\home\' -PathType Container ) { Copy-Item -Path 'C:\helium\install\home\*' -Destination "$env:USERPROFILE" -Recurse }
if ( Test-Path -Path 'C:\helium\install\config\' -PathType Container ) { Copy-Item -Path 'C:\helium\install\config\*' -Destination "$env:APPDATA" -Recurse }

& $args[0] $args[1..($args.Length)]

Stop-Process -InputObject $proxy
