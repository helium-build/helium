
$env:Path += ";$env:HELIUM_SDK_PATH"

$proxy = Start-Process C:/helium/LocalhostToUnix/LocalhostToUnix.exe -ArgumentList /helium/helium.sock,9000 -PassThru

& $args[0] $args[1..($args.Length)]

Stop-Process -InputObject $proxy
