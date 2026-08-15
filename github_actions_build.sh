#!/bin/bash
set -e

echo test 

# Build dotnet and deps
source env.sh 
cd icu && ./build_icu.sh
cd ..
./build_mono.sh

# Prepare sdk release
./gather_sdk.sh

# Build mono-nx demos
cd managed
./managed_build.sh

cd ../native/interpreter
make -j4

cd ../aot
./build_aot.sh
make -j4

cd ../..
./copy_sd_files.sh
