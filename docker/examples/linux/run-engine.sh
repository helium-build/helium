cd "$(readlink -f "$(dirname "$0")")/../../../"

rm -rf examples/build/dotnet/2.1/output examples/build/dotnet/2.1/sources/bin examples/build/dotnet/2.1/sources/obj
mkdir -p examples/build/dotnet/2.1/output

chown 1000:1000 examples/build/dotnet/2.1/output

docker run --rm \
 -v "$(pwd)/cache:/helium/cache" \
 -v "/var/run/docker.sock:/var/run/docker.sock" \
 -v "$(pwd)/conf:/helium/conf:ro" \
 -v "$(pwd)/examples/build/dotnet/2.1/:/workspace" \
 -e HELIUM_REALPATH_WORKSPACE="$(pwd)/examples/build/dotnet/2.1/" \
 -e HELIUM_REALPATH_CACHE="$(pwd)/cache" \
 helium-build/engine helium build /workspace/