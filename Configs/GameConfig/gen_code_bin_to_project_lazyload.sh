#!/bin/bash
WORKSPACE=$(cd "../..";pwd)
LUBAN_DLL=${WORKSPACE}/Tools/Luban/Luban.dll
CONF_ROOT=$(cd "$(dirname $0)";pwd)
DATA_OUTPATH=${WORKSPACE}/UnityProject/Assets/AssetRaw/Configs/bytes/
CODE_OUTPATH=${WORKSPACE}/UnityProject/Assets/GameScripts/HotFix/GameProto/GameConfig/

cp -rf "${CONF_ROOT}/CustomTemplate/ConfigSystem.cs" "${WORKSPACE}/UnityProject/Assets/GameScripts/HotFix/GameProto/ConfigSystem.cs"

dotnet ${LUBAN_DLL} \
    -t client \
    -c cs-bin \
    -d bin \
    --conf ${CONF_ROOT}/luban.conf \
    --customTemplateDir ${CONF_ROOT}/CustomTemplate/CustomTemplate_Client_LazyLoad \
    -x outputCodeDir=${CODE_OUTPATH} \
    -x outputDataDir=${DATA_OUTPATH} 
read -p "Press any key to continue..."

