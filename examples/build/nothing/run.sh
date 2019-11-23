cd "$(readlink -f "$(dirname "$0")")"/../../../

DIR=examples/build/nothing/

rm -rf $DIR/output/
mkdir -p $DIR/output/

dotnet run -p src/Engine/ -- build $DIR
