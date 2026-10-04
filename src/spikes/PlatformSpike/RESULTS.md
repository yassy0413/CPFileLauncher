# PlatformSpike 検証結果

## Windows 11 Pro (10.0.26200) / .NET 10.0.401 / Avalonia 11.3.22 / SharpHook 5.3.9 — 2026-10-02

**結論: 全項目 OK（一部はスパイクを修正したうえで OK）。修正で得た知見は SPEC §3.3 / §4.1 / §4.2 に反映済み。**

| # | 結果 | メモ |
|---|---|---|
| A | OK | SharpHook のフック開始 OK（`hook enabled`） |
| B | OK（修正後） | Ctrl+Alt+Space で表示／トグル、抑止（下のアプリにスペースが入らない）も効く。**押しっぱなしだとキーリピートで Show/Hide を繰り返した** → Space を離すまで再発火しないよう修正。約 0.9 秒の押下で 19 回のリピートを無視して 1 回だけ表示されることをログで確認（SPEC §4.1） |
| C | OK（修正後） | **既定は Ctrl+中クリック**（ユーザー判断で Ctrl+左クリック → Ctrl+中クリックに変更、SPEC §4.2 / §13.1-5）。押下・離上とも抑止され、ブラウザのリンク上でも新規タブが開かない。Ctrl+左クリック / Alt+中クリック / 中ボタン長押し / 左右同時クリックもそれぞれ OK。**初期実装は単発の左クリックで左右同時と誤判定していた**（番兵値 `long.MinValue` の引き算がオーバーフロー）→ 修正して解消 |
| D | OK | タイトルバー無し・半透明（`transparency=Transparent`）・タスクバー非表示・最前面切替 |
| E | OK | 2560×1440 ×2 枚（scale=1）。2 枚目のモニタ上でもカーソル位置に表示、画面端で補正。スケーリング 125%/150% 環境は未確認（手元に無し） |
| F | OK（修正後） | **ポップアップ外をクリックしても閉じなかった**。(1) フックで入力を抑止して呼ぶとフォアグラウンドロックで前面化が拒否される → `AttachThreadInput` で前面化（`popup foreground: ok (forced)`）。しかしそれでも外クリックで `Deactivated` が来ない（盤面内をクリック／ドラッグした後なら来る）。(2) `Deactivated` に頼らず、**フックのマウス座標と盤面矩形の比較で外クリックを判定**する方式に変更 → OK。外クリック・非アクティブ化で閉じたときは直前アプリへフォーカスを戻さない（戻すと `FAILED` になったりクリック先からフォーカスを奪う）。Esc / アイテム起動で閉じたときの返却は OK（SPEC §3.3） |
| G | OK（修正後） | **Explorer でつかんだ瞬間にポップアップが閉じてドロップできなかった** → 外クリック判定を**離上時**に変更（移動 4px 以下なら閉じる／ドラッグして盤面上で離せば開いたまま／盤面外で離せば閉じる）、押下中は `Deactivated` でも閉じない。修正後: フォルダ 1 件・5 件同時とも `DROP folder: <フルパス>` で取得、ファイル・.lnk もユーザー確認で OK。Chrome のアドレスバー／リンクは `DROP text: <URL>` で届く（`file:` ではない）（SPEC §3.3） |
| H | OK | トリガー → 表示 0〜6 ms（目標 100 ms 以内） |
| I | OK | 壁紙上 → `Progman`（2 枚目モニタでも同じ）。Explorer 上 → `CabinetWClass`、Chrome → `Chrome_WidgetWin_1`、Windows Terminal → `CASCADIA_HOSTING_WINDOW_CLASS` |

### 気づいた点（製品実装時の注意）

- 1 回だけ、表示直後（約 0.5 秒）にマウス操作なしで `popup deactivated` → 閉じたことがあった（16:16:22）。ユーザー操作によるものか、Explorer 側がフォーカスを取り返したのかは未特定。製品で頻発するようなら、表示直後の短時間は非アクティブ化で閉じない猶予を設ける。
- D&D の旧 API（`DragEventArgs.Data` / `DataFormats.*`）は Avalonia 11.3 で非推奨警告が出る。製品では `DataTransfer` / `DataFormat` を使う。

## macOS 26.6.2 (arm64) / .NET 10.0.11 / Avalonia 11.3 / SharpHook 5.3.9 → 8.0.0 — 2026-10-02

**結論: SharpHook を 8.0.0 に上げれば全項目 OK（D の Dock 非表示と E の Retina は未確認）。** 画面 2560×1440 ×1 枚（scale=1）。`dotnet run` で Terminal.app から起動。

| # | 結果 | メモ |
|---|---|---|
| A | OK | 権限なしで `HOOK FAILED: ErrorAxApiDisabled (40)`。**アクセシビリティだけ**許可で `hook enabled`、キー・マウスとも取得・抑止できた（入力監視は求められず）。権限は起動元に付く: `dotnet run` ではホスト端末（今回 Terminal.app。Claude.app を許可しても効かない）。`.app` 化後に再確認する |
| B | OK | ⌃⌥Space で表示／トグル、抑止（`suppressed=True`、エディタにスペースが入らない）、キーリピート無視とも Win と同じ |
| C | OK（SharpHook 8 で） | **SharpHook 5.3.9 は macOS で中ボタン（Button3）の離上を MousePressed として通知し、MouseReleased が来なかった**（左右ボタンは正常）。長押しが離上で再発火してトグルで閉じる、抑止した押下に対の離上を抑止できない、等の不具合になる。**8.0.0 で正しく `mouse up Button3` が来ることを確認**。8.0 は API 変更あり（`SharpHook.Native` → `SharpHook.Data`、`ModifierMask` → `EventMask`、`SimpleGlobalHook(runAsyncOnBackgroundThread:)` 廃止）。⌃+中クリック（抑止も OK）/ ⌥+中クリック / 中長押し / 左右同時 とも OK |
| D | 一部 OK | タイトルバー無し・半透明（`transparency=Transparent`）OK。Dock 非表示は `.app`（LSUIElement）が要るため未確認。**ポップアップを前面化すると同じアプリの他のウィンドウ（コントロールパネル）も一緒に前に出る**（macOS はアプリ単位でアクティブ化するため）。製品でも設定ウィンドウを開いたままだと同じことが起きる |
| E | OK（scale=1 のみ） | フック座標と Avalonia の座標・CGWindowList の座標が一致（左上原点）。Retina（scale=2）環境が無く pt / px の差は未確認 |
| F | OK（修正後） | (1) Mac では `Hide()` の途中で `Deactivated` が来るため、Esc で閉じても「外へフォーカスが移った」と誤判定して返却を飛ばしていた → 自分で隠している間の `Deactivated` を無視。(2) 表示前に `NSWorkspace.frontmostApplication` の pid を記録し、閉じたら `NSApp yieldActivationToApplication:` → `NSRunningApplication activate` で返却 → **エディタ（Sublime Text）にフォーカスが戻る**ことを確認。`NSApp hide:`（全ウィンドウが隠れる）は不採用。外クリックで閉じたときの返却省略は Win と同じでよい |
| G | OK | Finder からファイル 1 件・複数件・フォルダ（複数）・`.app`（**`DROP folder:` として末尾 `/` 付きで届く**）、Safari / Chrome の URL は `DROP text:`。Win と同じ受け口で足りる |
| H | OK | トリガー → 表示 0〜4 ms |
| I | OK（方式変更） | §13.1-1。**CGWindowList の前面順では判定できない**: Dock の全画面透明ウィンドウ（layer 18〜20）、カーソル（Window Server, layer 2147483630）、他アプリの透明フローティング窓（ChatGPT layer 3）が壁紙より手前に常駐し、alpha も 1.0。デスクトップ本体は Finder layer -2147483603、壁紙は Dock "Wallpaper-" layer -2147483624、ウィジェットは Notification Center layer -2147483601。**AX の `AXUIElementCopyElementAtPosition`（system-wide）ならクリックが実際に届く要素が取れる**: 壁紙上 → app=Finder, role=AXGroup, desc=desktop, 親ウィンドウの role=**AXScrollArea**（通常ウィンドウは AXWindow）。エディタ・Finder ウィンドウ・メニューバー（AXMenuBar）・ウィジェットは別判定になる。所要 中央値 0.4〜0.8 ms、最大 47 ms（初回）。アクセシビリティ権限はフックで必須なので追加の権限は不要 |

### アイコン取得（NSWorkspace、scratchpad の使い捨て検証、96px）

- `NSWorkspace iconForFile:` → `CGImageForProposedRect:context:hints:`（要求サイズに最適な表現 128px を選ぶ）→ BGRA 乗算済みの CGBitmapContext に描画。フォルダ（特殊フォルダの意匠込み）・`.app`・ファイル・ボリューム とも OK。1 件 3〜140 ms（初回が遅い。Win より遅めだがキャッシュ前提で問題なし）。
- 存在しないパス: `iconForFileType:@"txt"` 等の拡張子・UTI から種類アイコンが取れる（3 ms）。
- URL: `URLForApplicationToOpenURL:` で既定ブラウザ（今回 Chrome）の `.app` → `iconForFile:`。
- 画像サムネイル: `CGImageSourceCreateThumbnailAtIndex`（MaxPixelSize・Transform 指定）で heic も可。初回 280 ms、2 件目 8 ms。
- コードは `src/spikes/MacIconSpike/`（M3 Mac 版の移植元。`dotnet run -- 96` で `bin/.../out/` に PNG）。
- **注意: `/Applications/Safari.app` はシンボリックリンク**（Cryptex 内の実体を指す）で、`iconForFile:` はエイリアス矢印付きのアイコンを返す。シンボリックリンクは実体パスに解決してから取得するのがよい（Finder エイリアスは .lnk と同じく自身のアイコンでよい）。
