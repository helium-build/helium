#!/bin/bash

export PATH=$PATH:$HELIUM_SDK_PATH

socat TCP-LISTEN:9000,reuseaddr,fork UNIX-CONNECT:/helium/helium.sock &
SOCAT_PID=$!

cp -rT /helium/install/home $HOME
cp -rT /helium/install/root /

"$@"

kill $SOCAT_PID
