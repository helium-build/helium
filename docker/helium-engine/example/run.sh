DIR=/workspace/examples/build/dotnet/2.1/

EXAMPLE="$(readlink -f "$(dirname "$0")")"
WORKSPACE="$(readlink -f "$EXAMPLE/../../../")"

mkdir -p "$EXAMPLE/realpaths/"
echo -n "$WORKSPACE/cache/" > "$EXAMPLE/realpaths/cache"
echo -n "$WORKSPACE/" > "$EXAMPLE/realpaths/workspace"
echo -n "/tmp/helium-temp/" > "$EXAMPLE/realpaths/tmp"
mkdir -p /tmp/helium-temp/
chown 1000:1000 /tmp/helium-temp/

docker run --rm -it \
  -v "/var/run/docker.sock:/var/run/docker.sock" \
  -v "/tmp/helium-temp/:/tmp" \
  -v "$WORKSPACE:/workspace" \
  -v "$WORKSPACE/conf:/helium/conf" \
  -v "$WORKSPACE/cache:/helium/cache" \
  -v "$EXAMPLE/realpaths:/helium/realpaths:ro" \
  helium-build/engine build-once $DIR/build.toml $DIR/src/ $DIR/output/