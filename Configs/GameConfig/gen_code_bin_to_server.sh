#!/bin/bash
WORKSPACE=$(cd "../..";pwd)
LUBAN_DLL=${WORKSPACE}/Tools/Luban/Luban.dll
CONF_ROOT=$(cd "$(dirname $0)";pwd)
DATA_OUTPATH=${WORKSPACE}/Server/GameConfig 
CODE_OUTPATH=${WORKSPACE}/Server/Hotfix/Config/GameConfig

dotnet ${LUBAN_DLL} \
    -t server \
    -c cs-bin \
    -d bin \
    --conf ${CONF_ROOT}/luban.conf \
    -x outputCodeDir=${CODE_OUTPATH} \
    -x outputDataDir=${DATA_OUTPATH} 
read -p "Press any key to continue..."
