#!/bin/bash

export PATH=$PATH:$HELIUM_SDK_PATH

socat TCP-LISTEN:9000,reuseaddr,fork UNIX-CONNECT:/helium/socket/helium.sock &
SOCAT_PID=$!

if [ -d "/helium/install/home" ]; then cp -rT /helium/install/home $HOME; fi
if [ -d "/helium/install/config" ]; then cp -rT /helium/install/config $HOME/.config; fi

"$@"

kill $SOCAT_PID
