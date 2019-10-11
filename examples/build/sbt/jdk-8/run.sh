cd "$(readlink -f "$(dirname "$0")")"/../../../../

DIR=examples/build/sbt/jdk-8/

rm -rf $DIR/src/project/
rm -rf $DIR/src/target/
rm -rf $DIR/output/
mkdir -p $DIR/output/

java -jar target/scala-2.13/Helium-assembly-0.1.0-SNAPSHOT.jar build-once $DIR/build.toml $DIR/src/ $DIR/output/
