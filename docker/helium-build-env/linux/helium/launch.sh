#!/bin/bash

export PATH=$PATH:$HELIUM_SDK_PATH

/helium/LocalhostToUnix/Helium.LocalhostToUnix /helium/socket/helium.sock 9000 &
SOCAT_PID=$!

if [ -d "/helium/install/home" ]; then cp -rT /helium/install/home $HOME; fi
if [ -d "/helium/install/config" ]; then cp -rT /helium/install/config $HOME/.config; fi

"$@"
EXIT_CODE=$?

kill $SOCAT_PID

exit $EXIT_CODE
