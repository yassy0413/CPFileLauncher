# CPFileLauncher（旧 FileLauncher）— Claude Code 向けプロジェクトメモ

Windows & macOS 対応のファイル／アプリランチャー（ベンチマーク: CLaunch）。製品名は **CPFileLauncher**（CyberPunk FileLauncher。2026-10-04 確定）。**見た目はサイバーパンク専用**（テーマの選択は無く、配色 = 主色・副色だけ）。
**C# / .NET 10 LTS / Avalonia 11** で実装する。仕様は `spec/SPEC.md` が唯一の正。

## フォルダ運用ルール（厳守）

- `spec/` — 仕様。常に最新版だけを置く。古い版・差分ファイルは残さない（履歴は SPEC 末尾の変更履歴表に 1 行追記）。
- `issue/` — 進行中の課題。完了したら**ファイルを削除**する。
- `src/` — 実装。`src/spikes/` は使い捨ての技術検証で、結果を SPEC に反映したら削除する。
- `bin/` — ビルド済み実行ファイル。Windows は `bin/win-x64/`・`bin/win-arm64/` に単一 exe。macOS の配布物は `bin/osx-arm64/CPFileLauncher.app`（Apple シリコン専用。Intel / Universal は v1 で対応しない、SPEC §1.1 / §10.4 / §13.2 T7）。

## 現在の状態（2026-10-03）

- 仕様 **v1.0 確定**（`spec/SPEC.md`）。Mac 検証（SPEC §13.1）完了。残りは実装しながら確認する項目（SPEC §13.3 C1〜C7）。
- 設定項目の一覧は `spec/SETTINGS.md` が正（SPEC §7 は方針のみ）。
- 2026-10-03 ユーザー確定: 不透明度の既定 80%、Drop-to-Open の同名競合規則は両 OS 共通の Core `FileTransfer`（同一内容はスキップ、別内容は「名前 2」）、Mac の配布は Apple シリコン（osx-arm64）専用の `.app` 1 つ（Intel / Universal は取りやめ。最低 OS は macOS 15、.NET 10 のサポート範囲）、bundle id は未定（仮 `local.filelauncher.dev`）、設定画面表示中も Dock に出さない。
- 製品コード: `src/FileLauncher.sln`。SharpHook は **8.0.0**（5.3.9 は Mac で中ボタン離上を誤通知する）。
  - **M1（基盤）完了**: データモデル・JSON 保存（アトミック / 世代バックアップ / 破損復旧 / 新しい版は読み取り専用）・データフォルダ決定・Platform インターフェース。
  - **M2（トレイ常駐・ポップアップ盤面・トリガー）完了**（Windows、2026-10-02 ユーザー確認済み）。
  - **M3（Windows）は実装済み・ユーザー確認待ち**。SharpHook 8 更新後の Windows 再確認も未（`issue/MAC_SUPPORT.md` Mac-0）。
  - **サイバーパンク化 完了**（Mac、2026-10-05 ユーザー確認済み。SPEC §3.6〜§3.9）: テーマの選択を廃止し配色（主色・副色、プリセット 5 組）、製品名 CPFileLauncher、HUD（ステータス行 + 日付・空きスロットの角マーカー・タイトル行のタグ・コンソール風トースト・背景グリッド・枠線だけのタブ・面取り）、窓のグリッチ、アイコン（`src/build/make-icons.sh` で SVG から生成）。常時の演出（光の玉・明滅）は CPU が重いので既定オフ。
  - **枠の発光を八角形（面取り）に沿わせた**（Mac、2026-10-06 ユーザー確認済み。SPEC §3.9 H8 / §13.3 C22）: 枠の発光は `BoxShadow` ではなく `Themes/NeonGlowLayer`（`NeonGlowPlan`、Core `NeonProfile` / `Octagon`）。明滅 `PulseLayer` も同じものを焼く。窓の余白 40（トーストは 34）。八角形の座標式は Core `Octagon` 以外に書かない。
  - **M4（盤面の編集）完了**（Mac、2026-10-03 ユーザー確認済み）: 盤面内 D&D（移動 / 入替 / 複製 / 盤面外で削除）、右クリックメニュー、編集ダイアログ、ページ操作、キーボード操作、ホイールクリック + 回転、元に戻す（1 段階）、「について」。**削除はどの操作でも確認ダイアログを出さない**（救済は「元に戻す」）。**盤面の変更は必ず `BoardEditor` の `Commit` を通す**（元に戻すの基準がずれるため。SPEC §6.7）。
  - **フレームレスの設定画面・ダイアログと盤面の背景画像 完了**（Mac、2026-10-04 ユーザー確認済み。SPEC §3.7 / §3.8）: 盤面と窓の枠は `Themes/FrameChrome`、設定画面・ダイアログの基底は `Themes/ChromeWindow`（新しい窓はこれを使う。`Themed.Window` は廃止）。背景画像はデータフォルダの `background/` にコピーして相対パスで参照する。盤面は、アイテム以外の所（タブ・空きスロット・余白）のドラッグでも動かせる。
  - **M5（設定画面・常駐・演出・リソース化）完了**（Mac。Windows での確認は `issue/MAC_SUPPORT.md` / SPEC §13.3 に移した）。**Mac の `.app`（Mac-4）**: `src/build/publish-mac.sh` で `bin/osx-arm64/CPFileLauncher.app`（ReadyToRun、キーチェーンのコード署名証明書で署名、bundle id `com.palmjoy.cpfilelauncher`）。設定画面などの窓は `WindowActivation.ShowAndActivate` で開く（Mac は閉じたら直前のアプリへフォーカスを返す）。
  - **M5 ステップ 4・5 の内容**（エクスポート / インポート、設定のリセット、ハードウェアアクセラレーション、UI 文字列の日英リソース化と言語設定。Mac、2026-10-04 ユーザー確認済み）。**UI 文字列は直書きしない**: `Resources/Strings.resx`（英語）/ `Strings.ja.resx`（日本語）に足し、生成クラス `Strings` で参照する（訳語は `spec/TERMS.md`、enum は `EnumNames.Of`、OS 別は `Loc.Os`、キー表記は `Loc.Shortcut`、件数は `Loc.Count`）。ログ（AppLog）は日本語のまま。直書きは `NoHardcodedJapaneseTests` が検出する（残すリテラルは行末に `i18n:ignore`）。UI テストは英語カルチャで動き、要素は `Strings.*` で探す。英語表示の確認は `-- --lang en` を付けて起動。
  - **macOS**: `Platform.MacOS` 実装済み（Native/ の P/Invoke 層、Window / Permission / Desktop / Icon / Shell）。常駐・トリガー・Esc でのフォーカス返却・D&D 登録はユーザー確認済み（2026-10-03）。残りは `issue/MAC_SUPPORT.md`。テストは Core 167 件 + Desktop（UI）66 件。
  - 実行ファイル（Debug）: Win `src/FileLauncher.Desktop/bin/Debug/net10.0/CPFileLauncher.exe`、Mac は `dotnet run --project src/FileLauncher.Desktop/FileLauncher.Desktop.csproj`（`--project` には csproj のパスを渡す）。データは Win `%APPDATA%\CPFileLauncher\`、Mac `~/Library/Application Support/CPFileLauncher/`。Debug ビルドはログを `logs/` に常に出す。
  - **再ビルド前に起動中の FileLauncher を終了すること**（exe がロックされる。スパイクと同時に動かすとホットキーを二重に拾う）。
  ビルド / テスト: `cd src && dotnet build FileLauncher.sln && dotnet test FileLauncher.sln`。
  Windows 開発機には winget で .NET 10 SDK を導入済み（`C:\Program Files\dotnet`。既存シェルの PATH に無ければ先頭に足す）。Mac は Homebrew の .NET 10 SDK。
  - **Mac の注意**: AirDrop 等で受け取ったプロジェクトは `xattr -dr com.apple.quarantine <プロジェクト>` を実行する。名前が `.app` で終わるフォルダ（大文字小文字を区別しない）は macOS にアプリとみなされ、quarantine 付きだと Gatekeeper が「damaged」ダイアログを出して起動を止める（「Move to Trash」でフォルダごとゴミ箱へ移る）。旧 `src/FileLauncher.App` でこれが起きたため `src/FileLauncher.Desktop` に改名した（2026-10-03）。**フォルダ名を `.app` で終わらせない**。アクセシビリティ権限は起動元（Terminal.app）に付く。
- スパイク（削除のタイミングは `issue/MAC_SUPPORT.md` Mac-6）:
  - `src/spikes/PlatformSpike/` — Win / Mac 検証完了。Mac-1 の完了条件を満たしたら削除。
  - `src/spikes/MacIconSpike/` — Mac のアイコン取得。Mac-2 の完了条件を満たしたら削除。
  - `src/spikes/IconSpike/` — Windows のアイコン取得。Windows M3 のユーザー確認後に削除。
- 進行管理は `issue/` のマイルストーン（M3・M6）と `issue/MAC_SUPPORT.md`（順序表あり。Windows での確認項目もここの Mac-0 に集約）。Windows で確かめる観点は SPEC §13.3（C9〜C16）。

## 決定済みの方針（SPEC に詳細あり。覆す場合は SPEC を先に直す）

- 表示形式は「表示モード（常駐 / ポップアップ）× 呼び出しトリガー」の 2 軸で整理。v1 で両モード対応。
- v1 のトリガーはグローバルホットキー（既定 Ctrl+Alt+Space / ⌃⌥Space）とマウス操作。
  マウスの既定は **Ctrl(⌃)+中クリック ON**（押下・離上とも抑止）。Ctrl+左クリック・Alt+中クリック等は選択可・既定 OFF。修飾キーなしの中クリックは競合が多いので既定 OFF。
  画面端・デスクトップダブルクリックは v2 以降。
- 登録は Explorer / Finder からの D&D が基本。盤面は複数ページ（タブ）。
- データは人間可読 JSON（`settings.json` / `board.json`）、アトミック保存＋世代バックアップ、ポータブルモードあり。
- プロジェクト分割: `FileLauncher.Core`（OS 非依存）/ `FileLauncher.Desktop`（Avalonia。フォルダ・csproj 名。名前空間は `FileLauncher.App`、実行ファイル名は `CPFileLauncher`（アセンブリ名。`avares://CPFileLauncher/`））/ `FileLauncher.Platform`（抽象 IF）/ `.Platform.Windows` / `.Platform.MacOS` / `FileLauncher.Tests`（xUnit）。
  OS 固有処理は必ず Platform のインターフェース（IHotkeyService, IIconProvider, IWindowService など SPEC §10.3）の裏に置く。
- 製品名は CPFileLauncher（表示・実行ファイル・データフォルダ・bundle id。コードの名前空間・プロジェクト名は FileLauncher.* のまま）。画面の製品名は `AppInfo` から取る。ライセンス・コード署名・自動アップデートは TODO（SPEC §13.2）。
- テストは本物のデータフォルダ（`~/Library/Application Support/CPFileLauncher` など）に触れないこと（`DataPaths.Resolve` は `userDataRoot` を渡す）。2026-10-04 にテストが本物の旧フォルダを改名した。

## エージェントの使い分け

- **設計作業**（マイルストーン着手前の設計、仕様の追加・変更、インターフェース検討、issue のタスク分解）は、ユーザーの指定がなくても**実装前に必ず** `designer` サブエージェント（`.claude/agents/designer.md`、Fable で実行）に委譲する。実装の依頼に仕様未記載の振る舞いや設計判断が含まれる場合も同様。designer は `spec/` と `issue/` だけを更新し、実装はメインが行う。
- designer が Fable の利用上限（"out of usage credits" / 429）で失敗したら、同じ依頼を Agent ツールの `model: "opus"` を指定して呼び直す（エージェント定義・設定には上限時の自動切替が無いため。2026-10-03 ユーザー判断）。

## 実装時の約束

- 入力フックは SharpHook。イベント抑止（SuppressEvent）が要るので `SimpleGlobalHook` を使う（TaskPoolGlobalHook は抑止不可）。フックスレッドから UI を触るときは `Dispatcher.UIThread.Post`。
- macOS は「アクセシビリティ」権限が要る（入力監視は不要。SPEC §4.3）。未許可時はクラッシュさせず、トーストとメニューバーアイコンの警告で知らせ、ユーザーがメニューの「権限の設定…」を選んだら権限ガイドを開く。
- **設定画面・権限ガイドはユーザーが選んだときだけ開く**（初回起動・権限未許可・エラー時に自動で開かない）。
- 設定項目を足す・変える・既定値を変えるときは、先に `spec/SETTINGS.md` を直してから `AppSettings.cs` と設定画面を合わせる。
- Mac のネイティブ呼び出しは `Platform.MacOS/Native/` の P/Invoke 層（objc_msgSend / CF / CG / AX）経由のみ。構造体を返すセレクタは使わない（x64 の stret 問題）。AppKit の外のスレッドでは `AutoreleasePool` で囲む。
- ウィンドウはタスクバー / Dock に出さない（ShowInTaskbar=false、macOS は Info.plist の LSUIElement）。
- ポップアップは Close せず Hide/Show で再利用する（表示遅延 100ms 以内が目標）。
- UI 文字列はリソース化（日本語・英語）。新しい文言は `Strings.resx` と `Strings.ja.resx` の両方に足す（キー命名は `spec/TERMS.md` §4）。
- コミットメッセージ・コード内コメントは日本語でよい。識別子は英語。テストメソッド名は日本語でよい（何を保証するかを文で書く）。
- 全プロジェクト `net10.0` 単一 TFM。Windows 実装は `[SupportedOSPlatform("windows")]` を付けて実行時に選ぶ（SPEC §10.1）。
- settings.json / board.json の読み書きは `AppDataStore` 経由のみ。`IsReadOnly`（新しい版のデータ）のときは保存しない。

## 次にやること（優先順）

1. Windows 機で一括確認（`issue/MAC_SUPPORT.md` Mac-0 と SPEC §13.3 C9〜C20）: ビルド・テストは 2026-10-05 に Windows で通った（Core 179 / Desktop 85）。残りはユーザーの GUI 確認（M3 / SharpHook 8 / M4・M5 以降の機能）。
2. M6（`issue/M6_DISTRIBUTION_WINDOWS.md`）: `src/build/publish-win.ps1` で `bin/win-x64|win-arm64/CPFileLauncher.exe` を作れる（2026-10-05）。残りは 200 件での非機能要件の計測（ユーザー）。
3. Mac: `issue/MAC_SUPPORT.md` の Mac-1 / Mac-2 の残り（権限なし起動の確認、デスクトップ判定、Retina の C1）。完了したマイルストーンの issue ファイルは削除する。
