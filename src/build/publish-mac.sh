#!/bin/zsh
# macOS 配布物 bin/osx-arm64/CPFileLauncher.app を作る（SPEC §10.4、issue/MAC_SUPPORT.md Mac-4）。
# 使い方: src/build/publish-mac.sh   （CODESIGN_IDENTITY=… で署名に使う証明書を指定できる）
set -euo pipefail

RID=osx-arm64                       # Apple シリコン専用（SPEC §13.2 T7）
BUNDLE_ID=com.palmjoy.cpfilelauncher  # SPEC §13.2 T6（2026-10-04 確定）。macos/Info.plist と合わせる
EXE=CPFileLauncher                   # 実行ファイル名（csproj の AssemblyName）

ROOT=${0:A:h:h:h}
PROJECT=$ROOT/src/FileLauncher.Desktop
APP=$ROOT/bin/$RID/$EXE.app
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT/FileLauncher.Desktop.csproj" | head -1)
VERSION=${VERSION:-0.0.0}

echo "==> dotnet publish ($RID, Release, self-contained, ReadyToRun) version $VERSION"
# ReadyToRun（事前コンパイル）: 起動が約 0.2 秒速くなる。配布物は約 +15 MB（2026-10-04 ユーザー判断。SPEC §10.4 / §11）
dotnet publish "$PROJECT/FileLauncher.Desktop.csproj" -c Release -r $RID --self-contained -p:UseAppHost=true -p:PublishReadyToRun=true -o "$TMP/publish" -nologo -v q

echo "==> $APP を組み立て"
rm -rf "$APP" "$ROOT/bin/$RID/FileLauncher.app" # 旧名の .app も消す（同時に動かさないため）
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
sed "s/__VERSION__/$VERSION/g" "$PROJECT/macos/Info.plist" > "$APP/Contents/Info.plist"
cp "$PROJECT/Assets/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns" # アイコン（src/build/make-icons.sh で作る）
cp -R "$TMP/publish/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/$EXE"
cp -R "$PROJECT/macos/"*.lproj "$APP/Contents/Resources/" # 許可ダイアログの文言の各言語版（InfoPlist.strings）
plutil -lint "$APP/Contents/Info.plist" "$APP/Contents/Resources/"*.lproj/InfoPlist.strings > /dev/null

# Hardened Runtime（--options runtime）を付けるときは entitlement com.apple.security.automation.apple-events が要る（Finder の制御。SPEC §4.3 / §13.2 T3）。今は付けない
# 署名: TCC（アクセシビリティの許可）は「証明書 + bundle id」で覚えるので、再ビルドしても同じ証明書で署名する。
# 指定が無ければ「FileLauncher Dev」、無ければキーチェーンにある最初のコード署名証明書、それも無ければ ad-hoc（再ビルドのたびに許可が外れる）
IDENTITY=${CODESIGN_IDENTITY:-}
if [[ -z "$IDENTITY" ]]; then
  if security find-identity -p codesigning -v | grep -q '"FileLauncher Dev"'; then
    IDENTITY="FileLauncher Dev"
  else
    IDENTITY=$(security find-identity -p codesigning -v | sed -n 's/^ *[0-9]*) \([0-9A-F]*\) .*/\1/p' | head -1)
  fi
fi
if [[ -z "$IDENTITY" ]]; then
  echo "警告: コード署名の証明書が無いので ad-hoc で署名します（再ビルドのたびにアクセシビリティの許可が外れます）"
  IDENTITY=-
fi
echo "==> 署名: $IDENTITY"
# Contents/MacOS の中身はすべて「コード」とみなされるので、.dll などの Mach-O 以外も 1 つずつ署名する（署名は拡張属性に入る）。
# --deep には頼らず、中身 → バンドルの順。本体の実行ファイル（CFBundleExecutable）はバンドルの署名に含まれるので最後
find "$APP/Contents/MacOS" -type f ! -path "$APP/Contents/MacOS/$EXE" | while read -r f; do
  codesign --force --timestamp=none --sign "$IDENTITY" "$f" 2>&1 | grep -v 'replacing existing signature' || true
done
codesign --force --timestamp=none --sign "$IDENTITY" --identifier "$BUNDLE_ID" "$APP"
codesign --verify --deep --strict "$APP"

echo "==> 確認"
file "$APP/Contents/MacOS/$EXE"
du -sh "$APP"
echo "完了: $APP"
