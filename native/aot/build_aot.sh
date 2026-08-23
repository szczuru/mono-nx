#!/bin/sh
set -e

cd "$(dirname "$0")"

if ! which dotnet >/dev/null 2>&1; then
    if [ -e ~/.dotnet/dotnet ]; then
        export DOTNET_ROOT=~/.dotnet
        export PATH=$PATH:$DOTNET_ROOT
    else
        echo "dotnet was not found"
        exit 1
    fi
fi

if [ -z "$MONO_NX_ROOT" ]; then
    echo "MONO_NX_ROOT is not set"
    exit 1
fi

rm -rf output
mkdir -p output romfs source

# --- entry: Crash host jeśli leży w managed/, inaczej stary program ---
    cp -v managed/game.recomp.dll output/
    mkdir -p output/game/95005139715D6EF9/
    cp -v managed/game.recomp.dll output/game/95005139715D6EF9/
ENTRY_DLL=""
if [ -f managed/CrashBandicoot.Switch.dll ]; then
    ENTRY_DLL=managed/CrashBandicoot.Switch.dll
    echo "Crash mode: using prebuilt DLLs from managed/"
    cp -v managed/CrashBandicoot.Switch.dll output/
    if [ -f managed/RecompOne.Runtime.dll ]; then
        cp -v managed/RecompOne.Runtime.dll output/
    fi
    # opcjonalnie pełna gra w AOT:
    if [ -f managed/game.recomp.dll ]; then
        cp -v managed/game.recomp.dll output/
    fi
else
    echo "Example mode: building managed/program.csproj"
    dotnet build managed/program.csproj
    ENTRY_DLL=managed/bin/Debug/net9.0/program.dll
    if [ ! -f "$ENTRY_DLL" ]; then
        # net10 / Release — dociągnij jeśli u Ciebie inna ścieżka
        ENTRY_DLL=$(find managed/bin -name 'program.dll' | head -n1)
    fi
    cp -v "$ENTRY_DLL" output/program.dll
    ENTRY_DLL=output/program.dll
fi

echo "Entry assembly: $ENTRY_DLL"

echo Trimming the assemblies...

ILLINK=$MONO_NX_ROOT/artifacts/bin/Mono.Linker/Debug/net9.0/illink.dll
ILLINK_CFG=$MONO_NX_ROOT/src/mono/System.Private.CoreLib/src/ILLink/ILLink.Descriptors.xml
ILLINK_CFG1=$MONO_NX_ROOT/src/mono/System.Private.CoreLib/src/ILLink/ILLink.LinkAttributes.xml

LIB_ROOT=$MONO_NX_ROOT/artifacts/bin/mono/libnx.arm64.Debug/
FRAMEWORK_ROOT=$MONO_NX_ROOT/artifacts/bin/runtime/net9.0-libnx-Debug-arm64/

# Illink pisze do ./output (zachowanie stock mono-nx)
dotnet "$ILLINK" \
    -x "$ILLINK_CFG" \
    -x "$ILLINK_CFG1" \
    --feature System.Resources.UseSystemResourceKeys true \
    -d "$LIB_ROOT" \
    -d "$FRAMEWORK_ROOT" \
    -d output \
    --trim-mode link \
    -a "$ENTRY_DLL"

# Upewnij się, że entry i zależności Crash są w output (Illink mógł nadpisać strukturę)
if [ -f managed/CrashBandicoot.Switch.dll ]; then
    cp -v managed/CrashBandicoot.Switch.dll output/ 2>/dev/null || true
    cp -v managed/RecompOne.Runtime.dll output/ 2>/dev/null || true
    cp -v managed/game.recomp.dll output/ 2>/dev/null || true
fi

echo Mono AOT build...

MONO_COMPILER=$MONO_NX_ROOT/artifacts/bin/mono/linux.x64.Debug/cross/linux-x64/libnx-arm64/mono-aot-cross
export PATH=$PATH:$DEVKITPRO/devkitA64/bin/

echo "build log" > mono_aot.log

# BEZ direct-pinvoke — SDL / PInvoke przez dl_shim
for file in output/*.dll; do
    [ -f "$file" ] || continue
    echo "AOT $file"
    "$MONO_COMPILER" --path=output/ \
        --aot=full,static,direct-icalls,tool-prefix=aarch64-none-elf- \
        "$file" >> mono_aot.log 2>&1 || {
            echo "AOT failed for $file — see mono_aot.log"
            tail -n 40 mono_aot.log
            exit 1
        }
done

echo copying outputs
cp output/*.dll romfs/

echo copying full icu data file
if [ -n "$ICU_NX_INSTALL_DIR" ] && [ -f "$ICU_NX_INSTALL_DIR/share/icu/77.1/icudt77l.dat" ]; then
    cp "$ICU_NX_INSTALL_DIR/share/icu/77.1/icudt77l.dat" romfs/
else
    # fallback — znajdź dat
    find "$ICU_NX_INSTALL_DIR" -name 'icudt*.dat' 2>/dev/null | head -n1 | while read -r f; do
        cp -v "$f" romfs/
    done
fi

grep -r "Linking symbol:" mono_aot.log \
    | sed "s/Linking symbol: '\([^']*\)'\./STATIC_MONO_SYM(\1);/" \
    > source/mono_symbols.h

echo "build_aot.sh done. default_assembly in romfs/aot_config.ini must match entry DLL name."
ls -la output/*.dll output/*.o 2>/dev/null || ls -la output/
