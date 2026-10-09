---
name: publish
description: CPFileLauncher のリリースビルド（配布物）を、いま動いている OS に合わせて作る。Mac は src/build/publish-mac.sh で bin/osx-arm64/CPFileLauncher.app、Windows は src/build/publish-win.ps1 で bin/win-x64|win-arm64/CPFileLauncher.exe。「リリースビルド」「配布物を作って」「publish して」と言われたら使う。
argument-hint: "[win-x64|win-arm64]（Windows のみ。省略で両方）"
---

# リリースビルド

OS を判定して、対応するスクリプトを実行する。ビルドの中身はスクリプト側にあるので、ここに手順を重複して書かない。

## 手順

1. OS を判定する（環境の Platform、または `uname -s`）。
2. 起動中の CPFileLauncher があれば止まっていることを確かめる（実行ファイルがロックされる・ホットキーを二重に拾う）。
   - Mac: `pgrep -x CPFileLauncher`。見つかったら終了するようユーザーに頼む（勝手に kill しない）。
   - Windows: `publish-win.ps1` が起動中なら自分でエラーにする。
3. スクリプトを実行する（リポジトリのルートで。数分かかるので timeout は 600000 ms）。
   - **macOS**: `src/build/publish-mac.sh`
     - 署名の証明書を指定したいときだけ `CODESIGN_IDENTITY="…"` を前に付ける。
   - **Windows**: `powershell -NoProfile -ExecutionPolicy Bypass -File src\build\publish-win.ps1`
     - 引数に RID（`win-x64` / `win-arm64`）があれば `-Rid <RID>` を付ける。無ければ両方作る。
     - PATH に dotnet が無くてもスクリプトが `C:\Program Files\dotnet` を足す。
4. 結果を報告する: 出力先のパスとサイズ。失敗したらエラー出力をそのまま示す。
   - Mac で「ad-hoc で署名します」の警告が出たら、再ビルドのたびにアクセシビリティの許可が外れることを伝える。
   - Windows で「想定外のファイル」の警告が出たら、そのファイル名を伝える。

## やらないこと

- コミット・タグ付け・アップロードはしない（頼まれたときだけ）。
- スクリプトの外で dotnet publish を直接打たない（オプションがずれる）。
