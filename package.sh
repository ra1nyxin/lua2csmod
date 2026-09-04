#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
configuration="Release"
artifact_root="$project_root/artifacts"

restore_args=()
if [[ "${1:-}" == "--no-restore" ]]; then
    restore_args+=(--no-restore)
fi

rm -rf "$artifact_root/publish" "$artifact_root/stage"
rm -f "$artifact_root/Lua2CS-preview-linux-x64.zip" "$artifact_root/Lua2CS-preview-win-x64.zip"

package_runtime() {
    local runtime="$1"
    local native_library="$2"
    local publish_dir="$artifact_root/publish/$runtime"
    local stage_dir="$artifact_root/stage/$runtime"
    local plugin_dir="$stage_dir/addons/counterstrikesharp/plugins/Lua2CS"
    local archive="$artifact_root/Lua2CS-preview-$runtime.zip"

    dotnet publish "$project_root/src/Lua2CS/Lua2CS.csproj" \
        --configuration "$configuration" \
        --runtime "$runtime" \
        --self-contained false \
        --output "$publish_dir" \
        "${restore_args[@]}"

    if [[ "$runtime" == "linux-x64" ]]; then
        "$project_root/scripts/build-lua54-linux.sh" "$publish_dir/$native_library"
    fi

    mkdir -p "$plugin_dir/scripts" "$plugin_dir/examples" "$plugin_dir/types"
    cp "$publish_dir/Lua2CS.dll" "$publish_dir/Lua2CS.deps.json" "$plugin_dir/"
    cp "$publish_dir/NLua.dll" "$publish_dir/KeraLua.dll" "$plugin_dir/"
    cp "$publish_dir/$native_library" "$plugin_dir/"
    cp -R "$project_root"/examples/. "$plugin_dir/examples/"
    cp "$project_root/examples/tpa.lua" "$plugin_dir/scripts/tpa.lua"
    cp "$project_root/lua-types/Lua2CS.lua" "$plugin_dir/types/Lua2CS.lua"
    cp "$project_root/lua-types/.luarc.json.example" "$plugin_dir/scripts/.luarc.json.example"

    (
        cd "$stage_dir"
        zip -qr "$archive" addons
    )

    echo "$archive"
}

mkdir -p "$artifact_root"
package_runtime "linux-x64" "liblua54.so"
package_runtime "win-x64" "lua54.dll"
