cd "$(readlink -f "$(dirname "$0")")"/../../../../

DIR=examples/build/node/12/

rm -rf $DIR/src/bin/
rm -rf $DIR/src/node_modules/
rm -rf $DIR/output/
mkdir -p $DIR/output/

java -jar target/scala-2.13/Helium-assembly-0.1.0-SNAPSHOT.jar build-once $DIR/build.toml $DIR/src/ $DIR/output/
