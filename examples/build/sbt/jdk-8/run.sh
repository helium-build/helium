cd "$(readlink -f "$(dirname "$0")")"/../../../../

DIR=examples/build/sbt/jdk-8/

rm -rf $DIR/sources/project/
rm -rf $DIR/sources/target/
rm -rf $DIR/output/
mkdir -p $DIR/output/

dotnet run -p src/Engine/ -- build $DIR
