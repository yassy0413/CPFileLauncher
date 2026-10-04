# M6 — Windows 配布物

## 方針（SPEC §10.4、2026-10-04 ユーザー判断）

- self-contained の**単一 exe + ReadyToRun**（`-p:PublishSingleFile=true -p:PublishReadyToRun=true`）。macOS で R2R を採用した（起動 0.96〜1.12 秒 → 0.84〜0.88 秒、配布物 +15 MB）のと同じ方針。`PublishTrimmed` は使わない（Avalonia のリフレクション利用と相性が悪い）。
- 単一インスタンスの Mutex 名は `Global\` 付き（SPEC §10.3、2026-10-04 修正）。Windows でも従来どおり動くことを確認する。

## タスク

- [ ] `src/build/publish-win.ps1`（または `.cmd`。`publish-mac.sh` に対応するもの）: 引数 `-Rid win-x64|win-arm64`（既定は両方）で
  `dotnet publish src/FileLauncher.Desktop/FileLauncher.Desktop.csproj -c Release -r <rid> --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true -o bin/<rid>/` を実行し、古い出力は先に消す。`IncludeNativeLibrariesForSelfExtract` は付けない（SharpHook / Skia / HarfBuzz のネイティブ dll が exe の隣に出るなら `-p:IncludeNativeLibrariesForSelfExtract=true` を付けて単一ファイルに収め、その場合の展開先が `%TEMP%\.net\FileLauncher\` になることを記録する）。最後に exe のサイズを表示。
  `win-arm64` は x64 の開発機からクロスコンパイルできる（crossgen2 が対応）。ビルドできるかだけ確認し、arm64 実機での動作確認は機材があるときに。
- [ ] ポータブルモード（exe の隣の `portable.flag`。`DataPaths.ExecutableDirectory` が単一ファイルでも exe のディレクトリを返すこと）の動作確認。
- [ ] 非機能要件の計測（SPEC §11 / §13.3 **C13**）。Release の単一 exe を直接ダブルクリックで起動し、設定 → 詳細 →「ログを出力する」をオン:
  - 起動時間: ログ「起動完了: N ms」。**初回（Defender のスキャンが入る 1 回目）と 2 回目以降を分けて**記録。目標 1.5 秒。超えるなら `PublishReadyToRun` なしの exe でも測って比べ、結果を SPEC §10.4 に書く（既定は R2R あり）。
  - 表示遅延: ログ「表示まで n ms」。**起動後の最初の 1 回と 2 回目以降を分けて**記録（Mac は初回 85〜121 ms / 以降 23〜40 ms）。目標 100 ms（2 回目以降に適用）。
  - 常駐メモリ: タスクマネージャ「メモリ（プライベート ワーキング セット）」。待機中（盤面を一度も出していない）100 MB 以下、表示中・隠した後 150 MB 以下。アイテム 200 件（アイコンはキャッシュ済み。起動後に全ページを一巡してから測る）・背景画像なし・サイバーパンク。
  - exe のサイズ（win-x64 / win-arm64）。
  - 結果は SPEC §13.3 C13 の行と §11 の各行（Mac の値の隣）に記録する。
- [ ] 単一インスタンス: exe をダブルクリックで 2 回起動して 2 つ目が盤面表示だけして終了する / ポータブル版（別フォルダ）と通常版が同時に動く。
- [ ] CI（GitHub Actions）はリポジトリ / ライセンス方針（SPEC §13.2 T1, T2）が決まってから。

## 完了条件

`bin/win-x64/CPFileLauncher.exe`（製品名は 2026-10-04 に変更済み。exe のアイコンは `Assets/app.ico`、SPEC §3.9「アイコンの仕様」/ §13.3 C20）単体で動き、SPEC §11 を満たし（または外れた値と判断が §11 / C13 に記録され）、exe のサイズと起動時間（R2R あり）が SPEC に記録されている。
