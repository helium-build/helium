#!/bin/bash

export PATH=$PATH:/helium/bin:$HELIUM_SDK_PATH

/helium/LocalhostToUnix/Helium.LocalhostToUnix /helium/socket/helium.sock 9000 &
PROXY_PID=$!

while ! lsof -n -Fn -p $PROXY_PID | grep -q '^n.*:9000$'; [[ "$SECONDS" -lt 10 ]]; do
    sleep 0.01
done

if [ -d "/helium/install/home" ]; then cp -rT /helium/install/home $HOME; fi
if [ -d "/helium/install/config" ]; then cp -rT /helium/install/config $HOME/.config; fi

"$@"
EXIT_CODE=$?

kill $PROXY_PID

exit $EXIT_CODE
