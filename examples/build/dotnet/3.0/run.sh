cd "$(readlink -f "$(dirname "$0")")"/../../../../

DIR=examples/build/dotnet/3.0/

rm -rf $DIR/sources/bin/
rm -rf $DIR/sources/obj/
rm -rf $DIR/output/
mkdir -p $DIR/output/

dotnet run -p src/Engine/ -- build $DIR
