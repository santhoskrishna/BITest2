#!/usr/bin/env bash

configuration=Release
#arch=x64,arm,arm64

cat pack-list.txt | while read line || [[ -n $line ]];
do
    echo $line
    fullPath=$(pwd)/$(find . -iname "$line")
    ./dockerbuild.sh bionic --configuration $configuration --projects $fullPath
done
