cd "$(readlink -f "$(dirname "$0")")"/../../

mkdir -p examples/print-version/src/
mkdir -p examples/print-version/output/

export HELIUM_CONTENT_ROOT="$(pwd)/src/Engine/"

for sdk in examples/print-version/*.toml; do
  echo "SDK: $sdk"
  dotnet run -p src/Engine build --schema "$sdk" examples/print-version/
done
