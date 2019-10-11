cd "$(readlink -f "$(dirname "$0")")"/../../

mkdir -p examples/print-version/src/
mkdir -p examples/print-version/output/

for sdk in examples/print-version/*.toml; do
  echo "SDK: $sdk"
  java -jar target/scala-2.13/Helium-assembly-0.1.0-SNAPSHOT.jar build-once "$sdk" examples/print-version/src/ examples/print-version/output/
done
