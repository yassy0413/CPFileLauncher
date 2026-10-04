# macOS 対応

2026-10-02: Mac 環境（macOS 26.6.2 arm64、.NET 10 SDK、Xcode）でのスパイク検証が完了し、SPEC v1.0 を確定した。
検証結果の要点は SPEC §13.1（確定）・§13.3（実装しながら確認する項目）に転記済み。生の記録は `src/spikes/PlatformSpike/RESULTS.md`（スパイク削除時に消える）。
ここからは実装。macOS 固有処理は `FileLauncher.Platform.MacOS` に閉じる（SPEC §10.3）。GUI の操作確認はユーザーが行い、ログは「ログをコピー」で貼ってもらう。

## 検証結果の要約（詳細は SPEC）

| 項目 | 結果 | SPEC |
|---|---|---|
| 権限 | アクセシビリティのみで取得・抑止 OK。未許可時 `ErrorAxApiDisabled (40)`。権限は起動元（`dotnet run` なら Terminal.app）に付く | §4.3, §13.1-3 |
| SharpHook | 5.3.9 は Mac で中ボタン離上が `MousePressed` になる → **8.0.0 で解消** | §10.2 |
| ホットキー | Mac でも SharpHook で検出・抑止・リピート無視 OK → SharpHook 統一 | §4.1, §13.1-2 |
| フォーカス返却 | frontmost pid 記録 → `yieldActivationToApplication:` + `activate`。`Hide()` 中の `Deactivated` は無視が必要 | §3.3 |
| アプリ単位アクティブ化 | 盤面前面化で設定ウィンドウも前に出る → v1 は許容し規則化（2026-10-03 ユーザー確定。Dock も常に非表示で確定）。nonactivating パネルは任意スパイク | §3.5, §13.3 C7 |
| デスクトップ判定 | CGWindowList 不可。AX の `AXUIElementCopyElementAtPosition` で Finder + `AXScrollArea` | §4.2, §13.1-1 |
| 座標 | フック・Avalonia・CG とも左上原点 pt で一致（scale=1）。Retina 未確認 | §3.5, §13.3 C1 |
| D&D | Win と同じ受け口。`.app` はフォルダ（末尾 `/`）で届く | §5.1 |
| アイコン | NSWorkspace / CGImageSource で規則確定。シンボリックリンクは実体に解決 | §5.4 |
| Dock 非表示 | 未確認（`.app` が要る。`ShowInDock=false` でも試せる） | §8, §13.3 C2 |

## 開発ループの前提（最初に読む）

- **AirDrop 等で受け取ったプロジェクトは quarantine を外す**: `xattr -dr com.apple.quarantine <プロジェクト>`。旧 `src/FileLauncher.App` は名前が `.App` で終わるため macOS にアプリバンドルとみなされ、quarantine 付きだとその中の実行ファイルを起動した時点で Gatekeeper が「"FileLauncher.App.app" is damaged」と出して起動を止めた（「Move to Trash」でフォルダごとゴミ箱へ。2026-10-03 に実際に発生）。→ **2026-10-03 に `src/FileLauncher.Desktop` へ改名**（SPEC §10.1。アセンブリ名 `FileLauncher`・名前空間 `FileLauncher.App` は据え置き）。今後 `src/` に `.app` / `.App` で終わるフォルダを作らない。
- `dotnet run` が使えないとき `src/FileLauncher.Desktop/bin/Debug/net10.0/FileLauncher` を直接起動するには `DOTNET_ROOT` が要る（Homebrew の .NET は既定の場所に無い）。

- **ビルド・テスト**: `cd src && dotnet build FileLauncher.sln && dotnet test FileLauncher.sln`（Mac でも同じ。Windows 専用コードは `[SupportedOSPlatform("windows")]` で実行時に避ける）。
- **実行**: Terminal.app から `dotnet run --project src/FileLauncher.Desktop`。アクセシビリティ権限は **Terminal.app に 1 回だけ**付与すれば以後の再ビルドでも有効（権限はホストプロセスに付くため）。Claude Code のシェルから起動すると権限の付与先が別アプリになるので、GUI 確認はユーザーが Terminal.app から起動する。
- **`.app` で動かすとき**（Mac-4 以降、または C2〜C4 の確認時）: ad-hoc 署名（`codesign -s -`）は再ビルドのたびにアイデンティティが変わり、システム設定上は許可済みに見えても効かなくなる。対策:
  1. Keychain Access → 証明書アシスタント → 証明書を作成 → 名前 `FileLauncher Dev`、固有名のタイプ「自己署名ルート」、証明書のタイプ「コード署名」。
  2. 署名は `publish-mac.sh` が組み立て後に行う（`CODESIGN_IDENTITY` 環境変数、既定 `FileLauncher Dev`。手でやるなら `Contents/MacOS/` 内の Mach-O → バンドルの順、Mac-4 参照）。TCC は「証明書 + bundle id」で識別するので再ビルドしても権限が保たれる。
  3. それでもおかしくなったら システム設定 → プライバシーとセキュリティ → アクセシビリティ で項目を「−」で消して入れ直すか、`tccutil reset Accessibility <bundle id>`。
  4. `CFBundleIdentifier` を変えると権限は付け直し。**bundle id は `com.palmjoy.cpfilelauncher` で確定（SPEC §13.2 T6、2026-10-04 ユーザー判断）**。2026-10-04 の製品名変更（実施済み）で実行ファイル名が `CPFileLauncher` に、id が仮 `local.filelauncher.dev` からこの正式 id に同時に変わるので、そのとき 1 回だけ権限を付け直す（以後は変えない）。
  5. **2026-10-04 製品名の変更**: 本ファイルの `FileLauncher.app` / `MacOS/FileLauncher` / `~/Library/Application Support/FileLauncher/` は、ステップ 3 の後はそれぞれ `CPFileLauncher.app` / `MacOS/CPFileLauncher` / `…/CPFileLauncher/` と読み替える（SPEC §9.1 / §10.4。署名証明書「FileLauncher Dev」の名前は変えない）。
- `dotnet run` 版と `.app` 版を同時に動かさない（単一インスタンス制御で 2 つ目は終了するが、別データフォルダ＝ポータブルだと両方動いてホットキーを二重に拾う）。
- Debug ビルドは `logs/` に常にログを出す（`~/Library/Application Support/FileLauncher/logs/`）。

## タスク

実装者がそのまま着手できる粒度で書く。各タスクの完了条件を満たしたらチェックし、全部終わったらこのファイルを削除する。

### Mac-0: SharpHook 8.0 への更新（Win / Mac 共通。最初にやる）

- [x] `src/FileLauncher.Platform/FileLauncher.Platform.csproj` の SharpHook を `5.3.9` → `8.0.0`。
- [x] `Input/SharpHookInputService.cs` を 8.0 API に合わせる: `using SharpHook.Native` → `using SharpHook.Data`、`ModifierMask` → `EventMask`、`new SimpleGlobalHook(runAsyncOnBackgroundThread: true)` → `new SimpleGlobalHook()`（スレッドの指定は `RunAsync` の引数か、現状どおり `Task.Run` で包む）。`KeyCode` 名の変更（`Vc102` → `VcSection` 等）が `KeyName()` の表記に影響しないことを確認。
- [x] `Start()` を再試行可能にする（SPEC §10.3）: フックは `Start()` のたびに作り直し、前のインスタンスは `Dispose`。失敗後に再度 `Start()` で開始できること（Mac の権限付与後に使う）。
- [ ] **Windows で再確認**（ユーザー）: 全テスト緑 / Ctrl+Alt+Space 表示・トグル・抑止・押しっぱなしでリピートしない / Ctrl+中クリック（ブラウザのリンク上で新規タブが開かない）/ Alt+中 / 中長押し / 左右同時 / 盤面外クリックで閉じる・Explorer からの D&D で閉じない / 表示遅延のログ値 / 2 重起動（`Global\` 付き Mutex、SPEC §10.3）で 2 つ目が盤面表示だけして終了する。
- [ ] **M5（設定画面・多言語）の Windows 側の確認**（ユーザー。M5 は Mac で完了・issue 削除済み。詳細は SPEC §13.3 **C14**）: (1) `dotnet build` / `dotnet test` が Windows で通る（`ResxSourceGenerator`。通らなければ C14 の退避案）。(2) 設定画面の基本動作 7 項目（開く経路・不透明度の追従とまとめ書き・テーマ即時反映・ボタンサイズ / ラベル・表示 / 非表示演出とアニメーション OFF・「表示まで n ms」が演出で変わらない・再起動後も残る）とタスクバー / Alt+Tab（C9）。(3) `--lang en` で英語 UI を一巡（サイバーパンク / ダーク）、日本語の回帰、言語切替と「今すぐ再起動」。(4) `general.language = system` の判定（表示言語 日本語 / 英語。**C12**）。
- [ ] **ポップアップ表示位置のキーボード／マウス 2 組の Windows 確認**（ユーザー。実装・テスト完了、issue は削除済み。手順は SPEC §13.3 **C15** の 5 手順: v1 → v2 マイグレーション / 中央 + カーソル / 前回位置の組ごとの記憶 / 両方前回位置 / 固定座標が汚されない）。
- [ ] **見た目「サイバーパンク」の Windows 側の残り**（ユーザー。手順は SPEC §13.3 **C16**: 新規 settings.json でシアンで出てリソース欠落なし / glitch で「表示まで」が +10 ms 以内 / 色帯 3 項目。2026-10-04 のテーマ廃止で「4 テーマ往復」「基調色」は C16 から消え、配色は下の C18 へ）。
- [ ] **サイバーパンク化（製品名・配色・HUD・アイコン）の Windows 側**（ユーザー。Mac 側は 2026-10-05 にすべて確認済み、`issue/CYBERPUNK_IDENTITY.md` は削除済み。手順は SPEC §13.3 **C18** (1)〜(4): 旧 settings.json（`theme: dark` + `accent: red`）の 2 → 3 移行 / 「配色」プリセットと `ColorPicker`（フライアウトが透過窓の上で出る、暗い色が補正されない、⇄）/ 製品名（`CPFileLauncher.exe`・トレイのツールチップ・メニュー・About・タイトル接頭辞・ログ名・zip 名）/ 旧 `%APPDATA%\FileLauncher` → `CPFileLauncher` の改名引き継ぎ。**自動起動のレジストリ値名も `CPFileLauncher` に変わるので旧 `FileLauncher` の値が残る — 扱い（起動時に旧値を消す / 放置）をこの確認で決める**。**C19** (1)〜(6): ステータス行（CPU を `seconds` で記録）/ 角マーカー / タグとトーストの状態コード / 背景グリッドと濃さ / タブのアウトライン（`--lang en` で 7 タブ 1 行）/ 面取り（C8 の透過が取れない環境で角が黒く残らないか）。**C20** (1)〜(4): exe のアイコン（Explorer・タスクバー・各サイズ）/ 通知領域のシアンの線画（150% / 200%）/ 設定画面の Alt+Tab・タスクバーのアイコン / `dotnet run` でもトレイアイコンが出る。C9 の Windows 項目（グリッチの出入り・ボタンの高さ）も同時に）。
- [x] `src/spikes/PlatformSpike` は既に 8.0.0 なので変更不要。

完了条件: Windows で M2 / M3 の挙動が 5.3.9 のときと変わらない。

### Mac-1: Platform.MacOS の基盤と M2 相当（常駐・ポップアップ・トリガー）

**Platform 抽象の追加**（Windows 実装も同時に直す）

- [x] `IWindowService` に `bool UsesLogicalScreenCoordinates { get; }`（Win: false、Mac: true）と `void ConfigureBoardWindow(nint handle, DisplayMode mode)` を追加。Win は `HideFromTaskbar` の中身を `ConfigureBoardWindow` に移し、`HideFromTaskbar` は削除（呼び出し元は `PopupController` のみ）。
- [x] `IPermissionService` に `void RequestAccessibility()`（Mac: `AXIsProcessTrustedWithOptions` に prompt=true。Win: no-op）を追加。
- [x] `PopupController`: (1) **隠し中フラグ** — `Hide()` で `_window.Hide()` を呼ぶ前に立て、`OnDeactivated` はフラグ中は無視（SPEC §3.3）。(2) 幅・高さ・`BoardBounds()` の拡大率は `UsesLogicalScreenCoordinates` なら 1.0（アイコンの px は従来どおり `RenderScaling`）。(3) `HideFromTaskbar` → `ConfigureBoardWindow`。(4) 返却先 pid が自プロセスなら返さない（設定ウィンドウは M5 で扱うので今は no-op で可）。

**`FileLauncher.Platform.MacOS`**（すべて `[SupportedOSPlatform("macos")]`。csproj に `AllowUnsafeBlocks` 不要）

- [x] `Native/ObjC.cs`: `objc_getClass` / `sel_registerName` / `objc_msgSend` の引数型別オーバーロード（`nint`, `int`, `bool`, `double`, `ref CGRect`, `nuint` …）、`NSString` ⇄ `string`、`objc_autoreleasePoolPush/Pop` を `using` で使える `AutoreleasePool` 構造体。スパイク `src/spikes/PlatformSpike/Native/MacNative.cs` と `src/spikes/MacIconSpike/Program.cs` の P/Invoke を集約する。**構造体を返すセレクタは使わない**（x64 の `objc_msgSend_stret` 問題、SPEC §10.3）。
- [x] `Native/CoreFoundation.cs`（CFString / CFArray / CFDictionary / CFNumber / CFRelease / CFURL）、`Native/CoreGraphics.cs`（`CGEventCreate` / `CGEventGetLocation` / `CGWindowLevelForKey` / ビットマップコンテキスト）、`Native/Accessibility.cs`（`AXIsProcessTrusted(WithOptions)` / `AXUIElementCreateSystemWide` / `AXUIElementCopyElementAtPosition` / `AXUIElementCopyAttributeValue` / `AXUIElementGetPid` / `AXUIElementSetMessagingTimeout`、`IOHIDCheckAccess`）。
- [x] `MacWindowService : IWindowService`:
  - `ConfigureBoardWindow`: Avalonia の `IMacOSTopLevelPlatformHandle.NSWindow` に `setCollectionBehavior:`（Popup: MoveToActiveSpace(2) | FullScreenAuxiliary(256)、Resident: CanJoinAllSpaces(1) | Stationary(16)）。
  - `SetZOrder`: `setLevel:`（Topmost 3 / Normal 0 / Bottommost `CGWindowLevelForKey(kCGDesktopIconWindowLevelKey)+1`）。Avalonia の `Topmost` は常駐の重ね順と二重管理にならないよう、Mac では `Topmost=false` にして `setLevel:` だけで制御する。
  - `CaptureForeground`: `NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier`（pid を nint で返す）。
  - `BringToForeground`: Avalonia の `Activate()` で足りることを確認し、必要なら `activateIgnoringOtherApps:` + `makeKeyAndOrderFront:`。
  - `RestoreForeground(pid)`: pid が自分なら false。`runningApplicationWithProcessIdentifier:` → `NSApp yieldActivationToApplication:`（respondsToSelector で確認）→ `activate`（無ければ `activateWithOptions:0`）。スパイク `MacNative.ActivatePid` を移植。
  - `GetCursorPosition`: `CGEventGetLocation(CGEventCreate(0))`（左上原点 pt）。`CFRelease` を忘れない。
- [x] `MacPermissionService : IPermissionService`: `Accessibility` = `AXIsProcessTrusted`、`InputMonitoring` = `IOHIDCheckAccess(1 /* ListenEvent */)`（表示用）、`RequestAccessibility`、`OpenSystemSettings` = `open x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility`。
- [x] `MacDesktopDetector : IDesktopDetector`（SPEC §4.2）: system-wide 要素を 1 つ作って保持し `AXUIElementSetMessagingTimeout(0.1)`。`CopyElementAtPosition(x, y)` → pid → `NSRunningApplication.bundleIdentifier == "com.apple.finder"` を確認 → 要素の `kAXWindowAttribute`（無ければ `kAXParentAttribute` を辿った最上位）の `kAXRoleAttribute` が `AXScrollArea` なら true。取得物は `CFRelease`。例外・タイムアウトは false。フックスレッドから呼ばれるので UI には触らない。
- [x] `MacPlatformServices : IPlatformServices`: `SharpHookInputService(Desktop.IsDesktopAt)`、上記サービス、`Icons` / `Shell` / `AutoStart` は Mac-2 / Mac-3 までは「未対応」を返す仮実装（起動は失敗トースト、アイコンは null）。

**App 側**

- [x] `App.axaml.cs`: `OperatingSystem.IsMacOS()` なら `MacPlatformServices`。「未対応の OS」通知は Linux 等にだけ出す。
- [x] `Program.cs`: `.With(new MacOSPlatformOptions { ShowInDock = false })`（SPEC §8。C2 の前半をこれで確認）。
- [ ] 権限ガイド（SPEC §4.3）: フック `Failed` 時はトースト + メニューバーアイコン警告のみ（**ウィンドウを勝手に開かない**）。メニュー「権限の設定…」（未許可時だけ表示）で `PermissionGuideWindow` を開く: 状態表示 / 「許可を求める」(`RequestAccessibility`) / 「システム設定を開く」/ 「再試行」(`InputHook.Start()`) / 「再起動」。2 秒ごとに `Accessibility` を見て許可されたら自動で `Start()`、成功したら警告解除。
  → 実装済み（`PermissionGuideWindow.cs`、`App.OnHookFailed` / `StartPermissionPolling`）。未確認（下の「権限なしで起動」）。
- [x] メニューバーアイコン: Avalonia `TrayIcon` が Mac でテンプレート画像として描くか確認。描かなければ単色（黒）の画像を別に用意し、警告はバッジ。`Clicked`（左クリック）が Mac ではメニュー表示になることを確認し、メニュー先頭に「盤面を表示」を置く（SPEC §8）。
  → 表示・クリックでメニューが出ることはユーザー確認済み（2026-10-03）。**テンプレート画像化は 2026-10-05 完了・確認済み**（黒単色の `Assets/tray-template.png` + `MacOSProperties.SetIsTemplateIcon`。暗いメニューバーで白・明るいメニューバーで黒）。警告は**バッジではなく橙の線画 `tray-warning.png` への差し替え**（テンプレート解除）にした（SPEC §8 / §3.9「アイコンの仕様」）。警告状態の実機確認は「権限なしで起動」（下）と同時に。
- [x] アプリメニュー（`NativeMenu.SetMenu(app, …)`）に「終了 ⌘Q」。「設定… ⌘,」は M5 で追加。

**確認（ユーザー、`dotnet run`）** — `src/spikes/PlatformSpike/README.md` の A〜I を製品で:

- [ ] 権限なしで起動 → トーストと警告アイコン、ガイドから許可 → 再試行でフック開始（§13.3 **C4** を記録: 再起動が要ったか）
- [x] ⌃⌥Space 表示・トグル・抑止・リピート無視 / ⌃+中クリック / ⌥+中 / 中長押し / 左右同時
- [x] Esc で閉じて直前アプリ（エディタ）にフォーカスが戻る / 外クリックで閉じて戻さない / Finder でつかんでも閉じない
- [ ] 「デスクトップ上のみ」ON で壁紙上・デスクトップアイコン上だけ反応し、Finder ウィンドウ・他アプリ・メニューバー上では反応しない
- [x] Dock に出ない（**C2** 前半）
- [ ] **Retina**（内蔵ディスプレイ、または外部モニタを HiDPI 解像度に）で: カーソル位置に正しい大きさで出る、外クリック判定の矩形が合う、アイコンがぼけない（**C1**）
- [x] **Retina** で空きスロット・タブをドラッグして盤面を動かし、カーソルの移動量どおりに動く（2 倍 / 半分にならない。`Position` と `PointToScreen` の単位が一致するか、**C10**。SPEC §3.1 / §13.3）
- [x] トリガー → 表示のログ値が 100 ms 以内

完了条件: 上の確認が全部通り、§13.3 C1 / C2(前半) / C4 の結果がこのファイルに記録されている。

### Mac-2: M3 相当（アイテム登録・アイコン・起動）— Windows M3 のユーザー確認後に着手

- [x] `Core/Items/ItemFactory.FromPath`: ディレクトリは末尾の `/` `\` を取り除いて `Target` に保存（Finder の `.app` 対策、SPEC §5.1）。既存テストに Mac パスのケースを足す（`/Applications/Safari.app/` → App、名前 `Safari`）。
- [x] `MacShellService : IShellService`（SPEC §5.2, §5.3, §10.3）。すべて `Process.Start` で `open` を呼び、終了コード ≠ 0 を失敗にする:
  - App: `open -a <path> [--args <引数>]`、Drop-to-Open は `open -a <path> <ファイル…>`。
  - File: `open <path>`（引数は渡せないので無視。設定画面の注記に追加）。Folder: `open <path>`（Finder）。URL: `open <url>`。Command: `/bin/zsh -lc "<target> <args>"`。
  - `LaunchMode` / `RunAsAdmin` は無視（SPEC §5.1）。存在チェックは Windows 実装と同じ。
  - `RevealInFileManager`: `open -R <path>`。
  - `ReadShortcut`: `NSURL fileURLWithPath:` → `getResourceValue:forKey:NSURLIsAliasFileKey` が true なら `URLByResolvingAliasFileAtURL:options:error:`（`NSURLBookmarkResolutionWithoutUI`）で解決し `ShortcutInfo(target, "", "")`。シンボリックリンクは null（解決しない）。
  - ~~`TransferInto`: 同名が既にあればスキップ~~ → **2026-10-03 に規則変更（SPEC §5.3）**: コピー／移動は両 OS 共通の Core `FileTransfer` に移し、`IShellService.TransferInto` は削除する。作業は `issue/M3_ITEMS_AND_LAUNCH.md` の「追加タスク: Drop-to-Open の競合規則」（Windows と共通）。Mac 側はそれが入れば追加作業なし。
- [ ] Core `FileTransfer` 導入後、Mac で確認: 同名・同一内容のファイルをフォルダアイテムへドロップ → スキップのトースト / 同名・別内容 → `名前 2.ext` で追加 / Shift で移動（同一ボリューム = rename、外付けボリュームへ = コピー + 削除）/ `File.Copy` で xattr（`xattr -l`、Finder タグ）が保たれる。
- [x] `MacIconProvider : IIconProvider`（SPEC §5.4 の Mac 表）。`src/spikes/MacIconSpike/Program.cs` を移植:
  - 専用ワーカースレッド（`MacWorker`、Windows の `StaWorker` と同じ役割）で 1 件ずつ、毎回 `AutoreleasePool` で囲む。
  - 取得元の決定は Windows と同じ（上書き > `LinkPath` > `Target`、URL は `URLForApplicationToOpenURL:` の `.app`、Command は Terminal.app）。**シンボリックリンクは `File.ResolveLinkTarget(path, returnFinalTarget: true)` で実体に解決**してから `iconForFile:`。
  - 実在 → `iconForFile:` → `CGImageForProposedRect:context:hints:`（`ref CGRect` 渡し）→ BGRA 乗算済み描画（`IconPixels.Premultiplied = true`）。画像ファイル + サムネイル許可 → `CGImageSourceCreateThumbnailAtIndex`。存在しない → `UTType typeWithFilenameExtension:` → `iconForContentType:`。
  - 返す `IconPixels` は要求 px の正方形。失敗は null（App 側が汎用アイコン）。
- [x] `MacPlatformServices` の仮実装を本物に差し替え。
- [x] テスト: `ItemFactory` の末尾区切り・`.app` 判定（Core、OS 非依存で書ける範囲）。

**確認（ユーザー）** — `issue/M3_ITEMS_AND_LAUNCH.md` の確認手順 1〜7 を Mac で。追加で: Safari.app（シンボリックリンク）のアイコンに矢印が付かない / Finder エイリアスを登録すると解決先が起動しエイリアス自身のアイコンが出る / heic のサムネイル / URL は既定ブラウザのアイコン。

完了条件: M3 の完了条件を Mac で満たす。

### Mac-3: M5 と同時に行う Mac 固有分（設定画面・自動起動）

**完了（2026-10-04、ユーザー確認 OK）**。M5（設定画面・演出・常駐モード・自動起動・多言語。設計の正は SPEC §3.1 / §3.2 / §3.4 / §3.5 / §7 / §10.5、項目は `spec/SETTINGS.md`、演出は `spec/EFFECTS.md`）の Mac 固有分。M5 の issue は削除済み。残る Windows 側の確認は Mac-0 の「Windows で再確認」と SPEC §13.3 C14 / C12 / C9 / C11。

- [x] 設定ウィンドウのアクティブ化規則（SPEC §3.5）: `WindowActivation.ShowAndActivate` — 開く前に `CaptureForeground` で pid を記録 → `Show()` + `BringToForeground` + `Activate()`。`Closed` で自アプリの可視ウィンドウ（`IsVisible` かつ `Opacity > 0`）が無ければ `RestoreForeground(pid)`、失敗したら新設の `IWindowService.HideApplication()`（Mac は `NSApp hide:`、既定は何もしない）。権限ガイドも同じ経路。**2026-10-04 ユーザー確認 OK**（エディタ入力中に設定を開いて閉じると、そのままエディタに打てる）。
- [x] ポップアップを閉じたときの返却先が自プロセス（設定ウィンドウ操作中にトリガー）なら、設定ウィンドウをキーに戻す（SPEC §3.3 / §3.5。C9 の Mac 側確認 2026-10-03 で「閉じたときの返却が今までどおり」を確認済み）。
- [x] アプリメニューに「設定… ⌘,」（Mac-1 で「終了 ⌘Q」、M5 で「設定…」を追加済み）。盤面表示中の `⌘,`（Win は `Ctrl+,`）で設定を開く。**「閉じる ⌘W」はアプリメニューには置かず、`ChromeWindow` の `KeyDown`（⌘W / Esc）で代替**（SPEC §3.5 / §3.8。C9 の Mac 側で ⌘W を確認済み）。
- [x] 設定画面・編集ダイアログの Mac 対応（SPEC §1.2 の原則「使えない項目は無効表示 + 注記ではなく非表示」、2026-10-03 ユーザー判断）: 修飾キー表示（⌃ ⌥ ⇧ ⌘）/ 起動方法・管理者実行・ファイル種別の引数欄は編集ダイアログに出さない（M4 ステップ 3。SPEC §5.1 / §6.4）/ 自動起動の行は `.app` から起動していない間は出さない（`IAutoStartService.IsAvailable`）/ 注記を出すのは「使えるが制約がある」もの（サイドボタンの競合、ホットキー競合は検知できない）だけ。実装・確認済み。
- [x] `MacAutoStartService : IAutoStartService`（2026-10-04 実装。`IAutoStartService.IsAvailable` を追加し、設定画面は使えないとき行を出さない）: `SMAppService.mainAppService` の `registerAndReturnError:` / `unregister…` / `status`。`NSBundle.mainBundle.bundleIdentifier` が無い（`dotnet run`）ときは `IsAvailable = false` を返し、設定画面は自動起動の行を出さない（SPEC §1.2 の原則。`.app` から起動したときだけ行が出る）。**`.app` で確認済み → §13.3 C6 OK（2026-10-04）**。
- [x] 不透明度スライダーを Mac で確認: 透過背景のまま盤面全体が半透明になる → **§13.3 C5 OK**（M5 ステップ 1・2 の Mac 確認で済み。`Window.Opacity` のまま）。
- [x] 「権限の設定…」ボタンを一般タブに（SPEC §4.3。Mac のみ表示）。実装・確認済み。
- [x] 演出（`spec/EFFECTS.md`）を Mac で確認: `Frame.Opacity` / `RenderTransform` のフェード・ズーム・グリッチが透過背景のまま動き、表示までの ms（ログ）が演出 ON/OFF で変わらない → **§13.3 C8 OK**（M5 ステップ 1・2 の Mac 確認で済み）。
- [x] M4（盤面の編集、2026-10-03 完了。SPEC §6.2〜§6.7）の Mac 固有確認: ⌃クリックの右クリック扱い / 複製の修飾キー ⌥ / ⌥+数字 / Delete・fn+Delete / ⌃Tab / コンテキストメニュー表示中に盤面が閉じない / 編集ダイアログの非表示項目 → **M4 を Mac で確認したときに済み**（2026-10-03〜04）。

完了条件: 満たした（M5 の完了条件を Mac で満たし、C5 / C6 / C8 を記録済み）。

### Mac-4: `.app` バンドル化と配布物（SPEC §10.4）

配布物は **Apple シリコン（`osx-arm64`）専用の `.app` 1 つ**（`bin/osx-arm64/FileLauncher.app`。2026-10-03 ユーザー確定、Intel / Universal は取りやめ → SPEC §13.2 T7。**2026-10-04 の製品名変更後は `bin/osx-arm64/CPFileLauncher.app`**、以下の `FileLauncher` も `CPFileLauncher` と読み替える。Mac-0 の 5 参照）。
`dotnet publish -r osx-arm64 --self-contained` の出力一式を `Contents/MacOS/` に置くだけで、ネイティブランチャーも `lipo` も要らない。
Mac-1 の確認で C2 後半（LSUIElement）と C3（`.app` での権限）を見るため、**`publish-mac.sh` は Mac-1 の直後に作ってよい**（レイアウトは最終形と同じ）。

**レイアウト**（SPEC §10.4）

```
FileLauncher.app/Contents/
  Info.plist
  MacOS/FileLauncher      # apphost（publish 出力そのもの。CFBundleExecutable）
  MacOS/FileLauncher.dll, *.deps.json, *.runtimeconfig.json, libhostfxr.dylib, libcoreclr.dylib, …（publish 出力一式）
  MacOS/portable.flag     # 置けばポータブルモード（SPEC §9.1）
  Resources/AppIcon.icns  # アイコン（2026-10-05 実装済み。SPEC §3.9「アイコンの仕様」/ §10.4。publish-mac.sh が Assets/AppIcon.icns をコピー、Info.plist の CFBundleIconFile = AppIcon）
```

**タスク**

- [x] `src/build/publish-mac.sh`（引数なし。`RID=osx-arm64` を先頭の変数にして将来 T7 で引数化できるようにしておく）:
  1. `dotnet publish src/FileLauncher.Desktop -c Release -r osx-arm64 --self-contained -p:UseAppHost=true -p:PublishReadyToRun=true -o <tmp>/publish`（`PublishSingleFile` / `PublishTrimmed` は付けない。**`PublishReadyToRun` は 2026-10-04 ユーザー判断（案 A）で付けることにした**。SPEC §10.4 / §11）。
  2. `bin/osx-arm64/FileLauncher.app/Contents/` を作り直し（古い出力は先に消す）、`Info.plist` と `Resources/` を配置、`<tmp>/publish/*` を `MacOS/` にそのままコピー。`MacOS/FileLauncher` に実行権限があることを確認。
  3. 署名（`CODESIGN_IDENTITY` 環境変数、既定 `FileLauncher Dev`）: `MacOS/` 内の Mach-O（`*.dylib` と apphost `FileLauncher`。`file` で判定）を `codesign --force --sign ID` → 最後にバンドルを `codesign --force --sign ID --identifier <bundle id> bin/osx-arm64/FileLauncher.app`（`--deep` に頼らない）。`codesign --verify --deep --strict` で検証。
  4. 最後に `file bin/osx-arm64/FileLauncher.app/Contents/MacOS/FileLauncher` が `arm64` 1 スライスであること、`du -sh` で配布物の大きさを表示して終了。
- [x] `src/FileLauncher.Desktop/macos/Info.plist`: `CFBundleIdentifier` = **`local.filelauncher.dev`（仮。SPEC §13.2 T6 で正式 id が決まったら変え、権限を付け直す）**、`CFBundleName` / `CFBundleDisplayName` = FileLauncher、`CFBundleExecutable` = FileLauncher、`CFBundlePackageType` = APPL、`CFBundleShortVersionString` / `CFBundleVersion`、**`LSUIElement` = true**、`NSHighResolutionCapable` = true、`LSMinimumSystemVersion` = 15.0（SPEC §1.1）、`NSPrincipalClass` = NSApplication。`LSArchitecturePriority` / `LSRequiresNativeExecution` は書かない（単一アーキテクチャ）。
- [x] `portable.flag` の探索位置（SPEC §9.1）: `App.axaml.cs` の `DataPaths.Resolve(AppContext.BaseDirectory)` を、`Environment.ProcessPath` のファイル名が `FileLauncher`（`.exe`）ならそのディレクトリ、そうでなければ `AppContext.BaseDirectory` にする（`.app` では `Contents/MacOS/portable.flag`、Windows 単一ファイルでは exe の隣、`dotnet FileLauncher.dll` では dll の隣）。`DataPaths` のテストに「渡したディレクトリで判定する」ケースがあることを確認。
  実装メモ（2026-10-04）: `Contents/MacOS` の中身は .dll も含めてすべて「コード」扱いになるので、Mach-O 以外も 1 つずつ署名する（署名は拡張属性に入る）。順序は中身 → 本体以外 → バンドル（本体の実行ファイルはバンドルの署名に含まれる）。証明書は `CODESIGN_IDENTITY` → 「FileLauncher Dev」→ キーチェーンの最初のコード署名証明書（開発機では Apple Development）→ ad-hoc の順に選ぶ。版は csproj の `<Version>`（0.1.0）を Info.plist に入れる。`DataPaths.ExecutableDirectory` で portable.flag の場所を決める。配布物は 107 MB。
- [x] `.app` で起動して確認（ユーザー、Finder からダブルクリック。2026-10-04 OK。途中で設定画面を開くと落ちる不具合があり修正: `MacAutoStartService` が `new AutoreleasePool()`（空の値）を Pop して SIGSEGV。`AutoreleasePool.Push()` に直し、空の値の Pop は何もしないようにした）: `ps` で実行ファイルが `Contents/MacOS/FileLauncher`、ログの `AppContext.BaseDirectory` が `Contents/MacOS/` / Dock・⌘Tab に出ない（**C2** 後半）/ 初回の権限要求がアクセシビリティだけで入力監視は求められない（**C3**）/ 自己署名で再ビルドしても権限が保たれる / ポータブルモード（`Contents/MacOS/portable.flag`）/ 権限ガイドの「再起動」で `.app` として再起動する。
- [x] 非機能要件の計測（SPEC §11）を Mac でも: 起動 1.5 秒、常駐メモリ（待機 100 MB / 表示後 150 MB）、表示 100 ms。**3 項目とも Mac で計測済み（2026-10-04、`.app` Release）**: メモリ OK / 起動は ReadyToRun 採用で 0.84〜0.88 秒 OK / 表示は 2 回目以降 23〜40 ms OK、**起動後の最初の 1 回だけ 85〜121 ms で 100 ms を少し超えることがある**（SPEC §11 に既知の制約として記録。これ以上追うかはユーザー判断、下記「初回表示」の不採用案を参照）。
  - **ReadyToRun の結論**（2026-10-04 ユーザー判断、案 A）: `publish-mac.sh` の publish に `-p:PublishReadyToRun=true` を付けた（実装済み）。配布物 107 → 122 MB、待機メモリ 95 → 98 MB、起動完了 0.96〜1.12 秒 → 0.84〜0.88 秒。Windows の単一 exe も同じ方針（SPEC §10.4、計測は SPEC §13.3 C13 / `issue/M6_DISTRIBUTION_WINDOWS.md`）。
  - **メモリの結論**（2026-10-04 ユーザー判断、案 A）: SPEC §11 の目標を「待機中 100 MB 以下、盤面を表示した後 150 MB 以下」の 2 段階に改めた（測る量は Mac `footprint -p <pid>`、Windows はタスクマネージャの「プライベート ワーキング セット」）。下記の計測値は新しい目標を満たすので **Mac はメモリ OK**。Windows は SPEC §13.3 C13 で測る。隠すたびにウィンドウを閉じて GPU 面を手放す案は表示 100 ms と衝突するので採らない。
  - **メモリ**（2026-10-04、`.app` Release、`footprint` の値。背景画像なし・サイバーパンク）: 起動後の待機 **95 MB**（.NET のヒープ等 `VM_ALLOCATE` 約 52 MB + ネイティブ `MALLOC_SMALL` 約 34 MB）/ 盤面を表示中 146〜156 MB / 隠した後 119〜135 MB（盤面ウィンドウの GPU 描画面 `IOAccelerator` 約 17 MB が Hide 後も残る。Hide/Show 再利用のため）。試して効かなかったもの: `DOTNET_GCConserveMemory=7`・`gcConcurrent=0`・`TieredPGO=0`・`GCgen0size` 4 MB（いずれも ±3 MB）、`SkiaOptions.MaxGpuResourceSizeBytes` 8 / 2 MB（±5 MB）、ReadyToRun（待機 98 MB、配布物 122 MB）。Avalonia.Diagnostics は Release に入っていない。旧目標「常駐メモリ 100 MB」は待機中なら満たすが盤面を一度出した後は満たさなかったため、上記のとおり目標を見直した（2026-10-04）。
  - **起動時間・表示時間**（2026-10-04、`.app` Release、ログ ON で計測。起動完了 = プロセス開始からトレイ・入力フック・盤面の準備まで、`App.Start` の末尾でログに出すようにした）: Finder（`open`）から 1.29〜1.82 秒（初回だけ 2.25 秒）、実行ファイルを直接起動して 0.96〜1.12 秒。内訳は「起動:」ログ（データ読み込み後）までが 0.6〜1.5 秒（.NET と Avalonia の初期化）、そこから完了まで 0.2〜0.35 秒。**目標 1.5 秒はぎりぎり / 時々超える**。ReadyToRun で 0.84〜0.88 秒（約 0.2 秒短縮、配布物 +15 MB。ビルド直後の初回は 2.2 秒 = OS のファイルキャッシュが無い状態）→ **ユーザー判断で採用**（上記）。Finder 経由は LaunchServices の分が上乗せされるので、目標の判定は実行ファイル直接の値で行い Finder からの値も併記する（SPEC §11）。盤面の表示（2 つ目の起動で既存に表示を送る経路、ログ「表示まで n ms」）: 起動後最初の 1 回 85〜121 ms（100 ms を少し超えることがある）、2 回目以降 23〜40 ms。
  - **単一インスタンスの不具合を修正**（2026-10-04、SPEC §10.3 に反映済み）: 名前付き Mutex は Unix ではログインセッションごとの名前になり、Finder から起動したものとターミナル（やログイン項目など別の起動経路）から起動したものが互いを見つけられず 2 つ動いた。`Global\` を付けて解決（名前にユーザー名を含むので他ユーザーとはぶつからない。パイプは付けない: Unix ではユーザーごとの一時フォルダのソケットなのでどの起動元からも同じ）。Windows でも `Global\` 付きで動くことを Mac-0 / M6 の確認で見る（ユーザーごとの名前なので複数ユーザーの同時ログオンでも衝突しない想定）。
  - **初回表示**（2026-10-03、Mac Debug・dotnet run）: 起動後最初の 1 回だけ 150〜350 ms（2 回目以降 10〜40 ms）。`PopupController.Warmup`（起動直後に画面外・透明で 1 回表示）で 700 ms から下げたが、`Show()` 自体は 15 ms 程度で、残りは初回のアプリのアクティブ化と最初の描画（ログの「表示まで」は描画完了後の Background 優先度で計測）。画面内に透明で出す案・準備運動でアクティブ化まで済ませる案は効果が無いか副作用（フォーカスを奪う）があり不採用。→ Release / `.app` で測り直した結果が上の「起動時間・表示時間」（初回 85〜121 ms、2 回目以降 23〜40 ms）。初回の超過は SPEC §11 に既知の制約として記録済み。配布物の大きさは R2R ありで 122 MB。
- [ ] CI は SPEC §13.2 T1 / T2 の後（M6 と同じ。macOS ランナーで `publish-mac.sh`）。

完了条件: `bin/osx-arm64/FileLauncher.app` を Finder からダブルクリックで起動して SPEC §11 を満たし（2026-10-04 計測済み。初回表示の超過は §11 に記録）、C2（後半）・C3 の結果が記録されている。残りは CI のみ（T1 / T2 の後）。

### Mac-5（任意）: nonactivating パネルの検証（SPEC §3.5, §13.3 C7）

Mac-1 の後ならいつでも。2〜3 時間で結果が出なければ打ち切り、v1 はアプリ単位のアクティブ化のまま。

- [ ] 盤面の `NSWindow` に `setStyleMask:`（現在値 | `NSWindowStyleMaskNonactivatingPanel` = 1<<7）を設定し、(1) トリガーで表示したとき他アプリがアクティブなままか（メニューバーが変わらない）、(2) Esc・矢印・ショートカットキーが盤面に届くか、(3) 閉じたときに何もしなくても元のアプリにキー入力が戻るか、(4) 設定ウィンドウを開いた状態でトリガーしても設定ウィンドウが前に出ないか、を確認。
- [ ] 効いた場合: `MacWindowService.RestoreForeground` を no-op にし、`CaptureForeground` も不要に。SPEC §3.5 の「一緒に前に出る」記述と §3.3 の Mac 返却手順を書き換える（設計担当に戻す）。
- [ ] 効かない場合: 結果を §13.3 C7 に記録して終了。

### Mac-6: スパイクの削除

- [ ] `src/spikes/PlatformSpike/` — Mac-1 の完了条件を満たしたら削除（RESULTS.md の内容は SPEC §13.1 / §3.3 / §3.5 / §4.2 に転記済み）。
- [ ] `src/spikes/MacIconSpike/` — Mac-2 の完了条件を満たしたら削除。
- [ ] `src/spikes/IconSpike/`（Windows）— Windows M3 のユーザー確認が済んだら削除（`issue/M3_ITEMS_AND_LAUNCH.md` の方針どおり）。
- [ ] 全部消えたら `CLAUDE.md` のスパイク記述を更新（メインエージェント）。

## Windows 側マイルストーンとの順序・推奨着手順

| 順 | 作業 | 理由 |
|---|---|---|
| 1 | **Mac-0** SharpHook 8 | 共通コードの変更。Windows の再確認を早く済ませたい |
| 2 | **Mac-1** 基盤 + M2 相当（+ Mac-4 の `publish-mac.sh` で C2/C3 確認） | Windows M3 のユーザー確認と並行できる。触る共通ファイルは `PopupController`（隠し中フラグ・座標）と `Interfaces.cs` だけ |
| 3 | Windows **M3** のユーザー確認 → `IconSpike` 削除 | Mac-2 は M3 の Core / App が固まってから |
| 4 | **Mac-2** M3 相当 | |
| 5 | Windows **M4** 盤面編集（共通コードが中心。Mac では修飾キー表示・右クリックの確認） | |
| 6 | Windows **M5** 設定画面 + **Mac-3** | 不透明度スライダー・設定ウィンドウの規則・自動起動。**M5 / Mac-3 は Mac で完了（2026-10-04）**。Windows 側の確認だけ Mac-0 の項目と SPEC §13.3 C14 に残る |
| 7 | **Mac-4** arm64 `.app` 配布物 → Windows **M6** 配布物 | `publish-mac.sh` 自体は Mac-1 直後に作って C2/C3 を先に見る（レイアウトは最終形と同じ） |
| 任意 | **Mac-5** nonactivating パネル | Mac-1 の後ならいつでも。結果次第で §3.3 / §3.5 を簡素化できる |
| 随時 | **Mac-6** スパイク削除 | 各完了条件のあと |

## §13.3 の確認結果（記録欄）

| # | 項目 | 結果 | 日付 |
|---|---|---|---|
| C1 | Retina 座標 | 未 | |
| C2 | Dock 非表示（ShowInDock / LSUIElement） | OK: `dotnet run` で Dock に出ない（2026-10-03）。`.app`（LSUIElement）を Finder から起動しても Dock・⌘Tab に出ない（2026-10-04） | 2026-10-04 |
| C3 | `.app` での権限 | OK: 求められるのはアクセシビリティだけ（入力監視なし）。許可後は再起動なしでトリガーが効いた。同じ証明書（Apple Development）で再ビルドしても許可は保たれた | 2026-10-04 |
| C4 | 権限付与直後のフック再開 | Mac: `.app` を起動した後で許可を付けたとき、トリガーが効かず**再起動で直った**（2026-10-04、CPFileLauncher への改名で権限を付け直したとき。ログ OFF だったため失敗の記録なし）。自動再開（2 秒ごとの確認）が効かなかった原因は未調査 | 2026-10-04 |
| C5 | `Window.Opacity`（Mac） | 未 | |
| C6 | SMAppService | OK: `.app` 起動時だけ「ログイン時に自動起動」が出て、オン / オフでログイン項目に載る / 消える。`dotnet run` では行を出さない | 2026-10-04 |
| C7 | nonactivating パネル（任意） | 未 | |
| C9 | フレームレスの設定画面・ダイアログ（Mac 側） | OK: 信号ボタンなし、Esc / ⌘W / TextBox のキー受け取り、閉じたときの返却が従来どおり（**Windows 側は未**: タスクバー・Alt+Tab・最小化 / 復帰・ComboBox / ツールチップ。Windows の次回確認時に見て、最小化 / 復帰ができなければ SPEC §3.8 に「許容」の注記） | 2026-10-03 |
| C11 | 盤面の背景画像（Mac 側） | OK「全て OK」: 選択 / クリア・表示方法 5 種・覆い・画像の不透明度・エクスポートへの同梱（**Windows 側は未**: SPEC §13.3 C11 の (1)〜(9)。Windows の次回確認時に M3 / Mac-0 の再確認とまとめて） | 2026-10-04 |
| C10 | 盤面の自前移動の座標単位（Retina） | Mac OK（2026-10-03 ユーザー確認）、Windows 未 | |
| C12 | `.app` 起動時の言語 `system` 判定 | OK: Finder から起動して日本語になった（Windows 未） | 2026-10-04 |
| C8 | 透過ウィンドウ上の表示・非表示演出（`spec/EFFECTS.md`） | 未 | |
| C18 | 配色・製品名・データ引き継ぎ（Mac 側） | OK: (1)〜(6) すべて（2 → 3 移行、配色プリセット / `ColorPicker` / 補正なし / ⇄、`CPFileLauncher.app` と各文言、旧フォルダの改名、bundle id `com.palmjoy.cpfilelauncher` で権限付け直し 1 回、導出した背景色に違和感なし）。**Windows (1)〜(4) は未**（Mac-0 の項目） | 2026-10-04 |
| C19 | HUD 風の要素（Mac 側） | OK: (1)〜(6) すべて（ステータス行 + 日付、`seconds` の CPU 0.4〜0.7%、角マーカー、タグ / 状態コード、背景グリッド 既定 3%、タブのアウトライン `--lang en` 1 行、面取り 10 px 正式採用）。**Windows は未** | 2026-10-04 |
| C20 | アプリアイコン・メニューバーアイコン（Mac 側） | OK: 3 案から B（盤面 3×3）を選定。Finder / Launchpad の `.app` アイコン、メニューバーのテンプレート画像（暗いメニューバーで白・明るいメニューバーで黒）。**Windows (1)〜(4) は未** | 2026-10-05 |
