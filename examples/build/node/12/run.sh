cd "$(readlink -f "$(dirname "$0")")"/../../../../

DIR=examples/build/node/12/

rm -rf $DIR/sources/bin/
rm -rf $DIR/sources/node_modules/
rm -rf $DIR/output/
mkdir -p $DIR/output/

dotnet run -p src/Engine/ -- build $DIR
