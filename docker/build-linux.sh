#!/bin/bash

mkdir -p build-env/helium/install/root
mkdir -p build-env/helium/install/home

chown -R 1000:1000 build-env/helium/

pushd build-env
docker build -t helium/build-env:debian-buster-20190708 -f Dockerfile.linux-x86_64 .
popd
