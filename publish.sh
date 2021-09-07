#!/usr/bin/env bash

set -euo pipefail

# Change to local directory
declare -r LOCAL_DIRECTORY=$(cd -P -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
declare -r OUTPUT="${LOCAL_DIRECTORY}/bin/deb"

declare -ra PROJECTS=(
    "Collectors/Argus.Collector.E621" 
    "Collectors/Argus.Collector.FList" 
    "Collectors/Argus.Collector.Hypnohub" 
    "Collectors/Argus.Collector.Retry" 
    "Collectors/Argus.Collector.Weasyl" 
    "Argus.API"
    "Argus.Coordinator"
    "Argus.Worker"
)

function main() {
    if [[ -e "${OUTPUT}" ]]; then
        rm -rf "${OUTPUT}"
    fi

    # Build
    for project in ${PROJECTS[@]}; do
        declare real_path=$(realpath ${project})

        pushd "${real_path}"
            dotnet deb -c Release -o "${OUTPUT}"
        popd
    done

    # Deploy
    aptly repo add argus-release "${OUTPUT}" || true # suppress errors from adding existing packages
    aptly snapshot create "argus-$(date +"%Y-%m-%d:%H:%M:%S")" from repo argus-release
    aptly publish update focal :argus
    aptly publish update bullseye :argus

    scp -r -P 21122 "${HOME}/.aptly/public/argus/dists" "${HOME}/.aptly/public/argus/pool" jax@algiz.nu:/var/www/repo/
}

main "${@}"

