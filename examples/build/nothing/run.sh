cd "$(readlink -f "$(dirname "$0")")"/../../../

DIR=examples/build/nothing/

rm -rf $DIR/output/
mkdir -p $DIR/output/

java -jar target/scala-2.13/Helium-assembly-0.1.0-SNAPSHOT.jar build $DIR
