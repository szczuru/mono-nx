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

LIB_ROOT=$MONO_NX_ROOT/artifacts/bin/mono/libnx.arm64.Debug/
FRAMEWORK_ROOT=$MONO_NX_ROOT/artifacts/bin/runtime/net9.0-libnx-Debug-arm64/

ILLINK=$MONO_NX_ROOT/artifacts/bin/Mono.Linker/Debug/net9.0/illink.dll
ILLINK_CFG=$MONO_NX_ROOT/src/mono/System.Private.CoreLib/src/ILLink/ILLink.Descriptors.xml
ILLINK_CFG1=$MONO_NX_ROOT/src/mono/System.Private.CoreLib/src/ILLink/ILLink.LinkAttributes.xml

CRASH_MODE=0
if [ -f managed/CrashBandicoot.Switch.dll ]; then
    CRASH_MODE=1
fi

if [ "$CRASH_MODE" = 1 ]; then
    echo "Crash mode: Illink (no embedded fonts in Runtime) + AOT trimmed set"

    cp -v managed/CrashBandicoot.Switch.dll output/
    cp -v managed/RecompOne.Runtime.dll output/
    if [ -f managed/game.recomp.dll ]; then
        cp -v managed/game.recomp.dll output/
    fi

    ENTRY=output/CrashBandicoot.Switch.dll

    dotnet "$ILLINK" \
        -x "$ILLINK_CFG" \
        -x "$ILLINK_CFG1" \
        --feature System.Resources.UseSystemResourceKeys true \
        -d "$LIB_ROOT" \
        -d "$FRAMEWORK_ROOT" \
        -d output \
        --trim-mode link \
        -a "$ENTRY"

    # Fonty BIOS na RomFS (KromFont czyta z dysku)
    if [ -f managed/font1.raw ]; then cp -v managed/font1.raw romfs/; fi
    if [ -f managed/font2.raw ]; then cp -v managed/font2.raw romfs/; fi
    if [ -f romfs_fonts/font1.raw ]; then cp -v romfs_fonts/font1.raw romfs/; fi
    if [ -f romfs_fonts/font2.raw ]; then cp -v romfs_fonts/font2.raw romfs/; fi
else
    echo "Example mode: program.csproj + Illink"
    dotnet build managed/program.csproj
    ENTRY_DLL=managed/bin/Debug/net9.0/program.dll
    if [ ! -f "$ENTRY_DLL" ]; then
        ENTRY_DLL=$(find managed/bin -name 'program.dll' 2>/dev/null | head -n1)
    fi
    dotnet "$ILLINK" \
        -x "$ILLINK_CFG" \
        -x "$ILLINK_CFG1" \
        --feature System.Resources.UseSystemResourceKeys true \
        -d "$LIB_ROOT" \
        -d "$FRAMEWORK_ROOT" \
        --trim-mode link \
        -a "$ENTRY_DLL"
fi

echo Mono AOT build...
MONO_COMPILER=$MONO_NX_ROOT/artifacts/bin/mono/linux.x64.Debug/cross/linux-x64/libnx-arm64/mono-aot-cross
export PATH=$PATH:$DEVKITPRO/devkitA64/bin/

echo "build log" > mono_aot.log

# tylko to, co Illink zostawił w output/ — BEZ direct-pinvoke
for file in output/*.dll; do
    [ -f "$file" ] || continue
    echo "AOT $file"
    "$MONO_COMPILER" --path=output/ \
        --aot=full,static,direct-icalls,tool-prefix=aarch64-none-elf- \
        "$file" >> mono_aot.log 2>&1 || {
            echo "AOT failed for $file"
            tail -n 80 mono_aot.log
            exit 1
        }
done

echo copying outputs
cp output/*.dll romfs/

echo copying full icu data file
if [ -n "$ICU_NX_INSTALL_DIR" ] && [ -f "$ICU_NX_INSTALL_DIR/share/icu/77.1/icudt77l.dat" ]; then
    cp "$ICU_NX_INSTALL_DIR/share/icu/77.1/icudt77l.dat" romfs/
else
    found=$(find "$ICU_NX_INSTALL_DIR" -name 'icudt*.dat' 2>/dev/null | head -n1)
    if [ -n "$found" ]; then cp -v "$found" romfs/; fi
fi

grep -r "Linking symbol:" mono_aot.log \
    | sed "s/Linking symbol: '\([^']*\)'\./STATIC_MONO_SYM(\1);/" \
    > source/mono_symbols.h

echo "build_aot.sh done"
ls -la output/*.dll output/*.o 2>/dev/null | head -n 40
