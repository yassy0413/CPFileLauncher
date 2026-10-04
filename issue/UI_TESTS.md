# UI の自動テスト（`FileLauncher.Desktop.Tests`、Avalonia.Headless）

目標: App 層（`FileLauncher.Desktop`）のウィンドウ・コントロールを画面に出さずに動かし、「設定画面が開ける・落ちない・設定が盤面に反映される・テーマを切り替えても落ちない」を `dotnet test` で保証する。グローバルフック・OS の前面化・Explorer / Finder からの D&D は対象外（ユーザー確認で代える）。

正となる文書: SPEC §10.1 / §10.6。見た目・配色・HUD は SPEC §3.6 / §3.9 / §10.5（2026-10-04 サイバーパンク専用化。2026-10-05 までに実装済み・Mac でユーザー確認済み。テストのカルチャ・`Strings` 参照の規則は従来どおり）。

設計日: 2026-10-03（designer）。プロジェクトの骨組み（`TestApp` / `FakePlatform` / `SettingsUiTests` 5 件）はメインが着手済み。

---

## 1. 仕組み（着手済みの構成を正とする）

- `Avalonia.Headless.XUnit`。`[assembly: AvaloniaTestApplication(typeof(TestApp))]`、テストは `[AvaloniaFact]`（UI スレッドで実行。`Dispatcher.UIThread.RunJobs()` で保留中の処理を流す）。
- `TestApp : Application` は `FluentTheme` + **`AppTheme.Install(this)`**（テーマ実装 T1 で追加。トークン辞書が無いと `DynamicResource` が解決できず、テーマのテストができない）。本物の `App` は起動時に入力フック・トレイ・単一インスタンス制御を始めるので使わない。
- `FakePlatform : IPlatformServices`（OS に触らず呼び出しを記録）。`HookRunning` で「フックが動いている / いない」を切り替えられる。
- データは `Path.GetTempPath()/FileLauncherUiTests/<guid>` に `DataPaths` を向け、テストごとに削除。
- 描画: 既定は `UseHeadlessDrawing = true`（描かない。`RenderTargetBitmap` は使えない）。グリッチ演出のスナップショットを試すテストだけ `UseSkia()` + `UseHeadlessDrawing = false` の**別フィクスチャ**（別の `AvaloniaTestApplication` は 1 アセンブリに 1 つなので、描画ありのテストは別テストプロジェクト `FileLauncher.Desktop.RenderTests` にするか、`TestApp` を描画ありに統一して全テストを Skia で動かすかを T2 で決める。推奨: 全テストを Skia で動かす方が単純。遅くなるようなら分ける）。
- `InternalsVisibleTo("FileLauncher.Desktop.Tests")` 済み。
- 時間に依存するもの（`SettingsHub` の 500 ms デバウンス、演出の `Task.Delay`）は実時間で待たず、`Flush()` や「演出なし（`Animation = false`）」で確定させる。実時間が要るテストは `Task.Delay` を 1 回に抑える。

## 2. テスト観点

実装状況: [x] = 済み、[ ] = これから。テスト名は日本語で「何を保証するか」を書く（CLAUDE.md）。

### 設定画面（`SettingsWindow`）

- [x] すべてのタブを開いても落ちない（タブ数 ≥ 6）。
- [x] ホットキー記録欄: フック停止中はキー入力で記録する / 記録中にどの物理キーを押しても落ちない / フック動作中はフックで記録し終わったら止める / 記録を 2 回したあと Esc で中止すると最後に記録した値が残る。
- [x] 設定画面を閉じると予約中の保存が書かれる（`Flush`）。
- [ ] 各タブのコントロールを操作すると `hub.Current` が変わり、変更種別付きの `Changed` が 1 回出る（ToggleSwitch / ComboBox / Slider / NumericUpDown を代表 1 つずつ）。
- [ ] `hub.Replace`（リセット）で全コントロールが既定値に描き直され、再入防止で `Update` が呼ばれない。
- [ ] 読み取り専用（`IsReadOnly`）のとき上部に帯が出て、変更しても保存ファイルが書かれない。
- [ ] マウス操作リスト: 修飾キーなしの左クリックは赤枠になり保存されない / 「追加」で既定行（中クリック + Ctrl）が増える。
- [ ] 「演出の調整…」: 行数 = `EffectCatalog.All.Count`、種類 ComboBox の選択肢 = その演出の `AllowedKinds`、「既定に戻す」で `Effects` から ID が消える、glitch 行はイージングが無効（SPEC §3.6、SETTINGS.md「演出の調整…」）。
- [ ] 常駐タブ（M5 ステップ 3 の後）: 表示モードがポップアップでも項目が出る。
- [x] ポップアップタブの表示位置 2 組（SPEC §3.3。`SettingsUiTests`、2026-10-03）: 表示位置 `ComboBox` が 2 つある / キーボード側を「固定座標」にするとキーボード側の X / Y だけ有効になりマウス側は無効のまま / 「今の盤面の位置を使う」でそのグループの `x` / `y` だけが変わる。

### 即時反映（`SettingsHub` → 盤面）

- [ ] 不透明度を変えると `BoardWindow.Opacity` が追従する（`Render` は呼ばれない。`Render` 回数をカウントできる内部フックか、`SlotGrid.Children` のインスタンスが変わらないことで確認）。
- [ ] ボタンサイズ / ラベル表示を変えると盤面が描き直され、ウィンドウの `Width` / `Height` が期待どおり（`label = none` と `below` で高さが `LabelHeight × 行数` 違う）。
- [ ] `Animation = false` で `PlayShowAsync` / `PlayHideAsync` が即完了し `Frame.Opacity` が 1 / 0 になる。
- [ ] ~~テーマを変えると `RequestedThemeVariant` が変わる~~（2026-10-04 テーマ廃止で削除。配色の観点は「配色（主色・副色）」節へ）。
- [ ] `lastPosition` の保存が `hub.Update(..., None)` 経由で `Changed` を出さない（`PopupSettings.RememberLastPosition`。前回位置を選んでいる組だけに書かれる規則は Core テスト）。

### 盤面（`BoardWindow`）

- [ ] `Render` で行 × 列のスロットが作られ、アイテムの数だけ `Button.slot`（`.empty` なし）、残りが `.empty`。
- [ ] 存在しないパスのアイテムはアイコンが薄表示（`Opacity 0.4`）で警告バッジが付く。
- [ ] Esc で閉じる要求（`CloseRequested` 相当のイベント）が出る。`Ctrl+,`（Win）で `SettingsRequested` が出る。
- [ ] ページタブをクリックすると `PageIndex` が変わる。
- [x] 余白（`FlGlowMargin`）: ウィンドウが `盤面 + 34 × 2` 大きい（SPEC §3.6「発光のウィンドウ余白」。2026-10-04 のテーマ廃止で「ダークに戻すと 0」の半分は削除し、`ThemeTests` の該当テストは 34 固定の検証に書き換える）。
- [x] ステータス行（SPEC §3.9 H1、HUD 段階 A。実装済み 2026-10-04、`HudTests` 2 件 + Core `CyberColorsTests` の `StatusLine` 検証）: `hud.statusBar = true` で `Height` が OFF より 17 大きく、`StatusBar` の文字が `PAGE 01/01` / `ITEMS n`（アイテム数どおり）/ 日時（`clock = minutes` + `date` で `yyyy/MM/dd HH:mm` の形）。`SwitchPageTo(1)` で `PAGE 02/0N`。`clock = off` + `date = false` で右の `TextBlock` が非表示でタイマーが動かない。`Show()` で時刻のタイマーが動き、`Hide()` / `AmbientSuspended = true` で止まる。Core `StatusLine.Format` はゼロ埋め・100 ページ以上・`seconds`・`off`・日付 + 時刻・日付だけ、`NextTick` は分 / 秒 / 日付だけ（次の 0:00）。
- [ ] ステータス行の日付（`appearance.hud.date`。2026-10-04 追加。Core は上で済み、UI は未）: `clock = off` + `date = true` で右の `TextBlock` に `yyyy/MM/dd` だけが出てタイマーが動いている（間隔は翌 0:00 まで）。`date = false` で `HH:mm` だけ。設定画面の表示タブで「ステータス行」を OFF にすると「時刻」と「日付（年/月/日）」の両方が無効表示になる（`SettingsUiTests`）。
- [x] 空きスロットの角マーカー（H2。実装済み 2026-10-04、`HudTests` 1 件）: `.empty` スロットの中身に `CornerTicks` があり全周の枠線は描かない、`Button.slot.empty` の `Bounds` 全体がヒットテストに掛かる（空きスロットの中心への `MouseDown` × 2 で `RegisterRequested` 相当のイベント）。
- [x] 背景グリッド（H6、段階 C。実装済み・Mac でユーザー確認済み 2026-10-04、`HudTests.背景グリッドは設定で出し入れできる`）: `hud.grid = true` で `GridLayer.IsVisible` かつ `Opacity == 0.03`（既定 `gridOpacity = 3`）、`grid = false` で非表示。
- [ ] 背景グリッドの細目（未）: `gridOpacity = 0` で `IsVisible == false`、`gridOpacity = 10` で `Opacity == 0.10` / 読み込み時に 11 → 10、-1 → 0 に丸まる（Core `AppSettingsTests`）/ `Frame` の中で `Overlay` より後・`DockPanel` より前の子にある / 設定画面の表示タブで「背景グリッド」を OFF にすると「グリッドの濃さ」スライダーが無効表示になり、ON で戻る / スライダーの変更で `Changed(Hud)` が 1 回出る（`SettingsUiTests`）。

### トースト（`Toast`）

- [ ] `Toast.Show` が例外なく表示され、`Animation = false` ならクリックで即閉じる。ヘッドレスの `Screens.Primary` が null のとき位置決めをスキップして落ちない（要確認: ヘッドレスに主画面があるか）。

### 見た目「サイバーパンク」と配色（SPEC §1.3 / §3.6 / §10.5。2026-10-04 のサイバーパンク専用化で書き換え。[x] は書き換え前に実在したテストで、専用化（2026-10-04 実装済み）で次のように直した）

- [x] → 直す: 全 `Fl*` トークン（`AppTheme.TokenKeys`）が辞書にある（`ThemeTests`。3 辞書 → **1 辞書**。プリセット 5 組 + 任意の 2 色（例 `#FFFFFF` / `#000000` を `Normalize` した値）で `Cyber(Derive(…))` が網羅する）。
- [x] → 直す: ~~4 テーマ往復~~ → **プリセット 5 組と任意色を往復**しても設定画面・盤面・トーストが落ちない（`ThemeTests`）。`RequestedThemeVariant` は常に `AppTheme.Cyberpunk`。
- [ ] 解決した種類が `Glow` のとき `Button.slot` に `.glow` クラスが付き、`Highlight` では付かない（未。該当テストが見当たらない）。
- [x] glitch（描画ありフィクスチャ、`GlitchTests`）: `PlayShowAsync` が完了し `Frame.Opacity = 1`・`GlitchLayer.IsVisible = false` / キャンセルで同じ最終状態 / 描画なしでは fade にフォールバック / 種を固定した帯の矩形が盤面内（ずれ ±16 px は `Root` の余白に収まる）。
- [x] → 直す: 配色（`AccentAndColorTests` → `ColorsTests`）: シアンの導出結果が SPEC §3.6 の規則（`FlBoardBorder` = `#CC00E5FF`、`SystemAccentColorDark1` = `#00B8CC`、背景は HSL(H, 0.38, 0.065) を逆変換して ±2 / 255）/ `SetColors(赤系)` で表示中の盤面の `Frame.BorderBrush` が赤系になる / プリセット往復で落ちない。
- [x] 配色の設定画面（ステップ 2、`SettingsUiTests`。Mac でユーザー確認済み 2026-10-04）: 「配色」`ComboBox` でレッドを選ぶと `hub.Current.Appearance.Colors` が 2 色とも変わり `Changed(Colors)` が 1 回 / 主色の `ColorPicker.Color` を変えると「配色」が「カスタム」/ 「⇄」で 2 色が入れ替わる / hex 表示が `#RRGGBB` 大文字。~~暗い色を入れると `Normalize` 後の明るい色が入る~~ → **暗い色（`#0000CC`）を入れてもその値のまま `hub.Current` に入る**（自動補正は 2026-10-04 ユーザー判断で取りやめ。未テストなら 1 件足す）。
- [x] Core（`FileLauncher.Tests`、ステップ 1）: `ColorMath` の RGB ⇄ HSL 往復（代表 8 色、±1）と `Contrast`（白黒 = 21）/ `CyberDerivation.Derive` の規則・無彩色（S < 0.15）で背景 S = 0 / `ColorSettings.Normalize`（`ColorSettingsNormalizeTests`）: ~~`#0000CC` をコントラスト 3.0 以上に~~ → **`#0000CC` を変えない**・小文字 `#00e5ff` を `#00E5FF` に・8 桁はアルファを捨てる・不正な hex はシアンに戻して「戻した」ことを返す / `ColorPresets.Match` が 5 組を当てて近い色で null / `RgbColor.Parse` の 6 桁・8 桁・小文字・不正 / 2 → 3 のマイグレーション（`accent: red` + `theme: dark` → `colors` = レッドの 2 色、`theme` / `accent` キーが無い、`schemaVersion` 3）/ `EffectCatalog` から `ThemeMode` が消え `Default` が glitch / glow。
- [x] アイテムの色帯（`AccentAndColorTests` → `ColorsTests`）: `Color = Red` で帯の `Border` があり色が `FlItemColorRed` / `null` で帯なし / 発光あり / 8 色で落ちない（「ライトは発光なし」「3 テーマ」は削除）。
- [ ] 製品名（ステップ 3）: lint 「旧名 FileLauncher が UI の文言・配布物の設定に残っていない」（`Strings*.resx` の値、`Info.plist`、`publish-mac.sh`、Desktop の `.cs` / `.axaml` の文字列リテラル。`CPFileLauncher` の一部と名前空間 `FileLauncher.` / `avares://` 以外の `FileLauncher` を列挙）/ `ChromeWindow` のタイトル行が `AppInfo.TitlePrefix` を除く / `AboutWindow` に「CPFileLauncher」と「CyberPunk FileLauncher」/ Core `DataPaths.AppFolderName == "CPFileLauncher"`、`ExecutableDirectory` が `CPFileLauncher` / `CPFileLauncher.exe` のときだけ exe のフォルダを返す / `DataPaths.MigrateLegacyRoot`: 旧だけ → 改名、両方 → 何もしない、新だけ → 何もしない、旧が空 → 何もしない（一時フォルダで）/ `DataArchive.DefaultFileName` が `CPFileLauncher-backup-` 始まり。
- [x] タグ（段階 B。実装済み・Mac でユーザー確認済み 2026-10-04、`ChromeWindowTests.タグのコードを付けるとタイトル行の左に縦棒とコードが出て_無ければ出ない`）: `TagCode = null` の `ChromeWindow` に可視の `"CFG"` の `TextBlock` が無く、`TagCode = "CFG"` にすると 9 px の可視な `TextBlock` が出る。
- [ ] タグとトーストの細目（未）: `SettingsWindow` の `TagCode == "CFG"`、各ウィンドウの割り当て（ITEM / PAGE / AUTH / SYS / CONFIRM / INFO）/ `Toast.Show(error: true)` で状態コードが `!`、情報で `>`（`FontSize 12`・太字・`FlMonoFontFamily`）。
- [x] 設定画面のタブ（段階 C。実装済み・Mac でユーザー確認済み 2026-10-04、`SettingsUiTests.設定画面のタブはアウトラインで_7つとも1行に収まる`、`en` / `ja` の Theory）: `TabItem` が 7 つ、全部 `BorderThickness 1`、全部 `Bounds.Y` が同じ（折り返していない）、末尾のタブの右端が窓の幅の内側。
- [ ] 設定画面のタブの細目（未）: 選択中の `BorderBrush` が `FlAccent` の値で `Background` が `FlAccentSoft`、未選択は `FlTabOutline` / `PART_SelectedPipe` が非表示 / `FontSize == 14`。
- [x] 面取り（段階 D。2026-10-04 正式採用、`ChromeWindowTests.面取りの枠は八角形の面と線で描き_中身も同じ形で切り抜く`）: `ChromeWindow` を出すと `Chrome.Chamfer == FrameChrome.DefaultChamfer`（10）、八角形 `Face` が可視で `Data` があり、`Frame.BorderBrush` が透明 / `Chamfer = 0` にすると `Face` が非表示で `OutlineBrush`（`Frame.BorderBrush`）が透明でない。
- [ ] 面取りの細目（未）: `_content.Clip` が八角形（`Chamfer = 0` で null）/ 角ブラケットの `Data` が面取り時は 4 点の折れ線、0 では L 字 / `SetSurfaceVisible(false)` で `SurfaceBrush`（`Face.Fill`）が透明、`true` に戻すと `FlBoardBackground` に結び直る / 盤面（`BoardWindow`）でも `Chrome.Chamfer == 10`。

### 盤面の編集（M4。2026-10-03 完了、SPEC §6.2〜§6.7）

ポインタ操作は Headless の `MouseDown` / `MouseMove` / `MouseUp` 拡張（`Avalonia.Headless.HeadlessWindowExtensions`）で、座標は `_slots[(r,c)]` の `Bounds` の中心を `TranslatePoint(window)` で求める。`Edit` 後の保存は実時間を待たず `BoardEditor.Flush()` で確定させる。

テストは `BoardEditingUiTests.cs`（`BoardEditor` を通した統合。盤面・エディタ・`FakeController`・一時フォルダの `AppDataStore` を 1 組立てる）、`ItemEditorTests.cs`、`AboutWindowTests.cs`。2026-10-03 M4 ステップ 5 時点で済んだ観点を [x] にし、未着手の細目は別行に残す。

- [x] 盤面内 D&D（ステップ 1）: 空スロットへのドラッグで移動し `ItemInvoked` が出ず `IsDragging` が戻る / 別アイテムへで入替（両方の座標）/ Ctrl（Mac は ⌥）を押した `MouseUp` で複製（新しい Id、元は残る）/ 盤面の外（負の座標）で離すと確認なしで削除（`ConfirmResult = false` でも消える）/ 動かさずに離すと起動（クリック扱い）。
- [x] ゴーストの初動（SPEC §6.2、2026-10-03 ユーザー報告「初動でゴーストがワープする」の修正）: 「ドラッグを始めた最初の移動からゴーストがカーソルの中心に来る」= レイアウト前（`Bounds` が 0）の最初の移動でもゴーストの位置がつかんだ位置どおり（ゴーストは `DesiredSize`、層は `Root` の大きさを使う）。修正はユーザー確認待ち。
- [ ] 盤面内 D&D の細目（未）: ドラッグ中 `DragLayer` にゴーストがあり元スロットの中身が `Opacity 0.35` / `CancelDrag()` でゴーストが消え `ItemDropped` が出ない / 4 px 未満の移動はクリック扱い（閾値ちょうど）。
- [x] コンテキストメニュー（ステップ 1 / 2）: 右クリックで `ContextMenuRequested(Kind = Item)` が出て `BoardEditor.LastMenu` に「色 ▸」があり、「紫」を選ぶと `Color = Purple` / タブのメニュー「新規ページ」で「Page 2」ができて表示が移り、末尾で「右へ移動」が無効、「左へ移動」で並びが変わる / 「元に戻す」が Undo の有無で有効 / 無効（ステップ 5）。
- [ ] コンテキストメニューの細目（未）: アイテムの項目の並びが SPEC §6.3、Win 以外で「管理者として起動」が無い、URL / コマンドで「格納フォルダを開く」が無い、「色 ▸」が 9 項目で現在の色だけ `IsChecked`、最後の 1 枚で「ページを削除」が無効、`ShowMenuAsync` が `RunModalAsync` を通ること（`FakeController` の `ModalDepth` が 1 → 0）。
- [x] 保存のまとめ書き（ステップ 1）: 色を変えたあと `Flush()` で `board.json` に書かれる（一時フォルダの `AppDataStore` を読み直して確認）。
- [ ] 保存のまとめ書きの細目（未）: 連続 3 回の `Edit` で `Flush` まで `board.json` が更新されない / 読み取り専用のときトーストが 1 回だけ（`Toast` の表示回数を数えるフック）。
- [x] ページ（ステップ 2）: Ctrl+Tab / Ctrl+Shift+Tab（`KeyPressQwerty`）で `PageIndex` が往復 / ホイール（`MouseWheel`）で切り替わり、`board.wheelSwitchesPage = false` なら変わらない / タブへのドロップでそのページの最初の空きへ移る / タブの上でマウスを止めて待つとページが切り替わり、そのまま別ページのマスに置ける。
- [x] タブの自動スクロール（SPEC §3.1 / §6.5、2026-10-04 ユーザー要望。Mac でユーザー確認済み 2026-10-04。手動の横スクロール手段は付けない = ユーザー確定、テスト対象外）: 「ページが多くてタブがはみ出しても_切り替えたページのタブが見える位置までスクロールする」= 長い名前のページを 12 枚にして `Render` → `TabsPanel` を包む `ScrollViewer` の `Extent.Width > Viewport.Width`（はみ出している）で `Offset.X == 0` / `SwitchPageTo(11)` + `RunJobs` で末尾タブの `Bounds.Right <= Offset.X + Viewport.Width`（+0.5 の丸め許容）かつ `Bounds.X >= Offset.X` / `SwitchPageTo(0)` で `Offset.X` が 0 に戻る。
- [ ] タブの自動スクロールの細目（未）: 左右 24 px の余白（中ほどのページへ切り替えたとき隣のタブが `Peek` ぶん見える = `Bounds.X - 24 >= Offset.X` かつ `Bounds.Right + 24 <= Offset.X + Viewport.Width`）/ 既に見えているタブへ切り替えても `Offset.X` が変わらない / 隠した状態（`Hide()`）で `SwitchPageTo(11)` → `Show()` + `RunJobs` で見える位置に来る（`IsVisible` での再調整）/ ホイール・Ctrl+Tab 経由でも同じ（`SwitchPageTo` 以外の経路は描き直しが共通なので代表 1 つで可）。
- [ ] ページの細目（未）: タブのダブルクリックで `PageSettingsRequested` / `PageSettingsWindow` の行列数を入りきらない値にすると OK が無効で注記が出る / `SwitchPageBy(+1)` が末尾で止まる / Alt+2（`PhysicalKey.Digit2` + Alt）。
- [x] 編集ダイアログ（ステップ 3、`ItemEditorTests`）: 対象が空なら OK 無効、表示名が空なら既定の名前で登録 / キャンセルで null を返し元のアイテムは変わらない / 色見本を押すとその色になる / 同じページで使われているショートカットキーは注記が出る / Mac ではファイルの引数欄を出さず、アプリにすると出る / `CopyEditable` で Id と位置は変わらない。
- [ ] 編集ダイアログの細目（未）: 種別 URL で参照…が非表示 / Mac で起動方法・管理者の行が無い / ショートカットキーに "ab" を入れると空に正規化、"a" は "A"（`ItemHotkey.Normalize` 自体は Core テスト済み）。
- [x] キーボード（ステップ 4）: 選択なしで ↓ → `SelectedCell = (0,0)`、→ で (0,1) に `.selected` / Enter で `ItemInvoked`、F2 で `EditRequested`、Backspace（Mac の Delete）で `DeleteRequested`、`ClearSelection()` で null / `Hotkey = "K"` のスロット右上に 9 px の "K" があり、`K` で起動、割り当てのない `J` では起動しない。
- [ ] キーボードの細目（未）: 端で止まる / `.selected` が 1 つだけ / Shift+F10 で `ContextMenuRequested(Kind = Item or EmptySlot)` / 別ページのショートカットキーでは起動しない / `Key.Delete`（fn+Delete）でも `DeleteRequested`。
- [x] 元に戻す（ステップ 5）: 2 回移動 → Ctrl+Z / ⌘Z で 2 回目だけ戻り（1 回目は残る）、`CanUndo` が false になりもう一度押しても変わらない / 盤面外ドラッグの削除を戻すと元の座標に戻る / アイテム入りのページを削除 → 戻すとページとアイテムが戻り、表示ページも削除前の番号に戻る。メニュー「元に戻す」が戻す前は有効・戻した後は無効。
- [x] 「FileLauncher について」（ステップ 5、`AboutWindowTests`）: 「バージョン …」の文字、データフォルダの `TextBox`、`Expander` 内に OFL の本文がある / 二度 `Show` しても同じウィンドウ 1 枚。
- [x] ウィンドウの移動（SPEC §3.1、2026-10-03 `BoardWindow.Move.cs`）: 「空きスロットやタブをドラッグするとウィンドウが動き_タブは切り替わらない」= 空きスロットを押して 4 px 未満の移動では `IsMovingWindow` が false、越えると true になり `Position` が差分どおりに動く / タブからも同じく動き `PageIndex` が変わらず `ItemInvoked` も出ない。「アイテムのドラッグではウィンドウは動かず_位置ロック中は動かない」= アイテムのドラッグは盤面内 D&D（アイテムが移動、`Position` 不変）/ `AllowMove = false` では空きスロットをドラッグしても `Position` 不変。
- [ ] ウィンドウの移動の細目（未）: ダブルクリック（`ClickCount = 2`）の押下では移動を始めない / 右ボタン・中ボタンでは動かない / Mac の ⌃+左では動かない / 盤面内 D&D 中（`IsDragging`）は動かない / `PointerCaptureLost` で `IsMovingWindow` が戻る。
- [x] テーマ: `FlSlotFocusBorder` / `FlSlotFocusGlow` / `FlDragGhostBorder` が辞書にある（`ThemeTests` の網羅テストに自動で入る）。
- [ ] 見た目の細目（未）: `MenuFlyoutPresenterBackground` が窓背景 96%（導出値のアルファ `F5`）。

### 多言語（M5 ステップ 5、SPEC §7、`spec/TERMS.md`。2026-10-04 設計・同日実装、Mac でユーザー確認済み）

テストのカルチャは `en` 固定。UI 要素の探索は日本語リテラルではなく `Strings.*` を参照する（実施済み）。resx 2 本はテスト出力にリンクして読む。テストは `StringsTests.cs`（`[Fact]`、UI 不要）、`SettingsUiTests.cs`、`NoHardcodedJapaneseTests.cs`、Core は `FileLauncher.Tests/SettingsPeekLanguageTests.cs`。

- [x] `Strings.resx` と `Strings.ja.resx` のキー集合が一致する（差分を列挙）/ 各キーの `{n}` 引数の集合が一致する / 空の値が無い（`Common_ListSeparator` だけ空白を許す）（`StringsTests` 「英語と日本語のキーが一致する」「書式の引数が英語と日本語で同じで_空の値が無い」）。
- [x] `EffectCatalog.All` の全 ID に `Effect_<id>` がある / `EnumNames.Localized` の全 enum の全値に `Enum_<型>_<値>` がある（`Of<T>` は `ResourceManager` で引く汎用メソッドなので、例外ではなくキーの有無を en resx で検査する）（`StringsTests` 「すべての演出とenumの値に表示名がある」）。
- [x] `Strings.Culture = ja` で `Settings_Tab_General` が「一般」、`en` に戻すと "General"（`finally` で必ず戻す）（`StringsTests` 「カルチャを日本語にすると日本語_戻すと英語になる」）。
- [ ] `Loc.Count(1, one, other)` が `one`、`2` が `other` / `Loc.Shortcut` が Win「Ctrl+Shift+Tab」・Mac「⌃⇧Tab」（OS 判定を差し替え可能にするか、実行 OS 側だけ検証）。未着手（転送トーストの単複は Mac のユーザー確認で代えた）。
- [x] 設定画面「言語」: 今と違う値（`ja`）を選ぶと `hub.Current` が変わり「今すぐ再起動」が見えて押すと `SettingsContext.Restart` が呼ばれる / 今の表示言語（`en`）に戻すと消える / `Flush` で `settings.json` に `"language": "en"`（`SettingsUiTests` 「言語を今と違う値にすると今すぐ再起動が出て_保存される」）。
- [x] Core `SettingsPeek.Language`: キーなし → `system`、`"en"` → `en`、不正値 → `system`、壊れた JSON → `system`（`SettingsPeekLanguageTests`）。
- [x] lint: `src/FileLauncher.Desktop/**/*.cs`（`obj/` / `bin/` 除く）の文字列リテラルに日本語が残っていない。`AppLog.` / `Commit(` / `i18n:ignore` を含む行と `//` コメントは除外。違反は `ファイル:行: 内容` で列挙（`NoHardcodedJapaneseTests` 「画面側のコードに日本語の文字列を直書きしていない」）。

### 常時の演出（`frameOrb` / `glowPulse`。`spec/EFFECTS.md`「常時の演出の詳細」、`issue/AMBIENT_EFFECTS.md`。2026-10-04 設計）

Core（`FileLauncher.Tests`）は `AmbientEffectsCoreTests.cs`、Desktop は `AmbientEffectsTests.cs`（`OrbLayer.OrbPositions` と `PulseLayer.Level` を直接見る。`RequestAnimationFrame` はヘッドレスでは呼ばれないので、`AmbientAnimator.Tick(elapsedMs)` で進める。OS の「視差効果を減らす」は `BoardWindow.ReducedMotion`（`Func<bool>`）で差し替える）。実装済み・ユーザー判断（2026-10-04 案 A: `frameOrb` / `glowPulse` ともどのテーマでも既定 `none`。テストは `Effects` に明示的に `orb` / `pulse` を入れて動かす）反映済み。

- [x] Core `OrbPath`: 角丸矩形の周長（`2(w+h) − 8r + 2πr`）/ `PointAt(0)` が左上の角、時計回りに進む / 1 周で戻る / 大きさ 0 や角丸が大きすぎても例外にならない（`AmbientEffectsCoreTests` 「角丸矩形の周長と_周に沿った位置が時計回りに進む」「大きさ0や角丸が大きすぎても例外にならない」）。
- [x] Core `AmbientMath`: `PulseLevel` は 0 から始まり半周期で最大、周期 0 でも 0 を返す（「明滅は0から始まり半周期で最大になる」）。
- [x] Core `EffectCatalog`: **`frameOrb` / `glowPulse` の既定がサイバーパンクでもダークでも `none`** / `Normalize` が `frameOrb` の 50000 を 20000 に、`glowPulse` の 10 を 1000 に丸め、`UsesEasing = false` で `easing` が `linear` に / `orb` を `boardShow` に書くと捨てられる（「常時の演出はどのテーマでも既定で動かず_時間は演出ごとの範囲に丸める」）。
- [x] Desktop: サイバーパンクで `frameOrb = orb` を設定し `Render` → `Show()` で `AmbientRunning == true`、`Hide()` で false、`Show()` で true に戻る（`AmbientEffectsTests` 「サイバーパンクで盤面が見えている間だけ動き_隠すと止まる」）。
- [x] Desktop: 既定（`Effects` 空）では動かない / `AmbientSuspended = true` で false・false に戻すと true / `ReducedMotion` が true で `ApplyEffects` すると false・false に戻すと true / `Animation = false` で false（「既定では動かず_アニメーションOFFや収納中やOSの設定でも止まる」）。
- [x] Desktop: `Tick(0)` で玉が上辺、`Tick(4000)`（8 秒で 1 周）で下辺 / `glowPulse = pulse / 4000` を設定し `Tick(2000)` で `PulseLayer.Level ≈ 0.45`（「玉は枠線に沿って一周の半分で反対側へ進み_明滅は半周期で最大になる」）。
- [x] Desktop: `Overlays` の子の順が `AmbientRoot`（`Orbs` を含む `Grid`）→ `GlitchLayer` → `DragLayer`（「層の順は明滅と玉がグリッチとドラッグの層より下」）。
- [x] → 直す: 設定画面: `glowPulse` の行が見える / `Settings_Appearance_Effects_AmbientNote` がある（旧「設定画面の発光の明滅の行はサイバーパンクのときだけ出る」。2026-10-04 のテーマ廃止で「ダークにすると見えない」の半分を削り、常に見える検証に）。
- [ ] 未着手: `PlayHideAsync` を始めた瞬間に `AmbientRunning == false`（完了を待たない）/ `orbTwin` で 2 個目が半周先 / `pulseStrong` で `Level` 0.9 / `Tick(4000)`（周期 4000）で `Level ≈ 0`。
- [ ] 未着手（設定画面）: `frameOrb` の行のスライダーが `Minimum 2000` / `Maximum 20000` / `TickFrequency 500` で、種類を `orb` にしたとき表示が「8.0 s」/ イージングが無効 / 注記 `Settings_Appearance_Effects_AmbientNote` はテーマによらず常に見える / `FakePlatform.ReducedMotion = true` で設定画面を開くと `Settings_Appearance_Effects_ReducedMotionNote` が見える / 「既定に戻す」で `Effects` から ID が消える / 「演出の調整…」の行数が `EffectCatalog.All.Count`。
- [ ] 未着手: `PulseLayer.Refresh` が大きさ・テーマが同じなら焼き直さず、`FlBoardGlow` が変わる（基調色の切替）と焼き直す（画像の参照が変わることで確認。`Level = 0` のときは `Render` が何も描かない）。

### 権限ガイド・通知

- [ ] `PermissionGuideWindow` を `FakePlatform`（`HookRunning = false`）で開いて閉じても落ちない。状態表示の色が `FlError` / `FlSuccess` のトークン値。
- [ ] `Notice.Show` が例外なく表示される。

### フレームレスの枠と窓（SPEC §3.8 / §10.5。`ChromeWindowTests.cs`、`ThemeTests.cs`。Mac でユーザー確認済み 2026-10-03）

- [x] `FrameChrome` を切り出した後も盤面のウィンドウサイズが前と同じ（既存の余白テスト: サイバーパンク = 盤面 + 34 × 2、ダーク = 余白 0 がそのまま通る）。
- [x] `ChromeWindow`（`タイトルバーなしで枠に囲まれ_大きさは中身と余白から決まる`）: `SystemDecorations == None`、タイトル行の文字が `Title` から「FileLauncher — 」を除いたもの、`Width = BodyWidth + GetMargin() × 2`、`Height = BodyHeight + TitleBarHeight + 1 + 余白 × 2`。
- [x] × クリックで `CancelResult` の値（false）が `ShowDialog<bool?>` に返る / Esc で `Closed` が出る（`バツとEscで閉じ_ダイアログはキャンセルの結果を返す`）。
- [x] `SettingsWindow` に下部の「閉じる」ボタンが無く、Esc で閉じて `Flush` が呼ばれる。全タブを開いても落ちない（既存テストが `ChromeWindow` 化後も通る）。権限ガイドも `SystemDecorations == None` で開ける。
- [x] `FlChromeShadow` / `FlChromeMargin` / `FlChromeMarginThickness` が辞書にある（`ThemeTests` の網羅テストに自動で入る）。`SettingsWindow.Chrome.Frame.Background` が `FlWindowBackground` の値（2026-10-04 から導出値。固定の `#0B0F1A` ではなくリソースの値と比べる）。
- [ ] 細目（未）: `DragDrop.GetAllowDrop(board.Chrome.Frame)` が true / Ctrl+W（Mac は ⌘W）で `Closed` / `BodyWidth = 560` の `Width` が 628 / `Dialogs.ConfirmAsync` 経由の Esc → false / `PageSettingsWindow` / `ItemEditorWindow` の × → null / `IsCancel` ボタンのあるダイアログで Esc が二重に閉じない。
- [x] ダイアログのボタン（SPEC §3.8「ボタン」。2026-10-04、`ChromeWindowTests.OKとキャンセルのボタンは同じ高さで文字が中央`）: `Dialogs.ActionButton(OK)` と `ActionButton(キャンセル)` を横に並べて表示すると `Bounds.Height` が等しく、`HorizontalContentAlignment` / `VerticalContentAlignment` が `Center`。
- [x] 窓のグリッチ（SPEC §3.8「演出」、EFFECTS.md「glitch の詳細」13.。2026-10-04、`ChromeWindowTests.グリッチで閉じても_ダイアログの結果はそのまま返る`）: `ChromeWindow.ShowEffect` / `HideEffect` に glitch / 150 を入れて `ShowDialog<bool?>` → `Close(true)` した直後は `Task` が未完了（演出中）で、3 秒以内に完了して結果が `true`。`finally` で static を元に戻す。
- [ ] 窓のグリッチの細目（未）: `HideEffect` が fade のとき `Close()` で即 `Closed` / `Close()`（結果なし）でも閉じる / 演出中に `IsHitTestVisible == false` / `× → CancelResult` の値がグリッチ経由でも返る / `ShowEffect` が glitch のとき `Show()` 直後の `Chrome.Opacity == 0` で演出後に 1（描画ありフィクスチャ。描画なしでは静止画が撮れず即 1 になることを確認）/ `ApplicationShutdown` の `CloseReason` では `Closing` をキャンセルしない（ヘッドレスで再現できるかは要確認）。

### 盤面の背景画像（SPEC §3.7 / §10.5。`BackgroundTests.cs`（Desktop）、`BackgroundImageTests.cs`（Core）。Mac でユーザー確認済み 2026-10-04）

- [x] `ApplyBackground`（`盤面に画像を敷くと覆いが重なり_並べるは原寸のタイル_なしに戻すと消える`）: `fill` で `Backdrop.Background` が `ImageBrush`（`Stretch.UniformToFill`）、`Overlay.IsVisible` で `Opacity == 0.4` / `tile` で `TileMode.Tile` と `DestinationRect == (0,0,64,32, Absolute)`（原寸）、`overlay = 0` で `Overlay` 非表示 / `bitmap = null` で `Backdrop.Background == null`・`Overlay` 非表示。
- [x] `BoardBackgroundLoader`（`小さい画像は原寸で_長辺が2048を超える画像は縮小して読む` / `見つからない画像と壊れた画像は状態だけ返して落ちない`）: 300×200 は原寸、1000×3000 は高さ 2048 に縮む、同じファイルの 2 回目は同じ `Bitmap` インスタンス。存在しないパス → null + `NotFound`、テキストを .png として保存 → null + `Failed`、null → `None`。どれも例外を出さない。
- [ ] 細目（未）: `fit` / `stretch` / `center` の `Stretch` / `Alignment` / 前後でウィンドウの `Width` / `Height` と `SlotGrid.Children` のインスタンスが変わらない（`Render` されていない）/ 横長 3000×600 の `DecodeToWidth` 側 / `LastWriteTimeUtc` が変わったら読み直す / `Dispose` で古い `Bitmap` が破棄される。
- [x] 画像の不透明度（B′、2026-10-04。`BackgroundImageTests` の丸め・既定 100、`BackgroundTests` の面が Transparent・`Backdrop.Opacity`・null で元の面のブラシに戻る。**テーマ切替での追従と、設定画面のスライダーの無効表示・通知回数は未テスト**）: Core `Normalize` が `imageOpacity` を 10〜100・5 刻みに丸める（0 → 10、103 → 105 ではなく 100、47 → 45、キーなし → 100）/ Desktop `ApplyBackground` で画像があるとき `Chrome.Frame.Background` が `Transparent` で `Backdrop.Opacity == imageOpacity / 100`、`bitmap = null` に戻すと `Frame.Background` が `FlBoardBackground` の値に戻る（配色を切り替えると追従する = `DynamicResource` で結び直されている）/ 設定画面に「画像の不透明度」スライダーがあり画像なしで無効、変更で `Changed(Background)` が 1 回。
- [x] 設定画面 表示タブ（`設定画面で画像を選ぶとコピーして設定し_クリアで消える`）: 画像なしで「クリア」が無効 / `ApplyBackgroundFileAsync` で `background/pick.png` にコピーされ `Image` が相対パスになり「クリア」が有効 / `BackgroundStatus = NotFound` のとき注記「画像が見つかりません: background/pick.png」が見える / 「クリア」で `Image = null` になりコピーが消える。
- [ ] 設定画面 表示タブの細目（未）: 画像なしのとき表示方法 `ComboBox` と覆い・画像の不透明度のスライダーが無効で、`hub.Update` で `image` を入れると有効になる / 覆いスライダーの変更で `Changed(Background)` が 1 回出る / `hub.NotifyBackgroundLoaded()` でプレビューの `Image.Source` が `BackgroundBitmap()` の値に変わる / `Failed` の注記「画像を読み込めません: …」。
- [x] Core（`FileLauncher.Tests` `BackgroundImageTests.cs`）: `BackgroundImageStore.Import` がデータフォルダの `background/` にコピーして `background/<名前>` を返し、既存のコピーを消す / `Remove` は managed のときだけ消す（絶対パスは消さない）/ `Normalize` が `overlay` を 0〜90・5 刻みに丸め既定が `fill` / 40 / JSON 往復とキーなし → 既定 / `DataArchive.Export` に `background/<名前>` が入り `Read` → `Import` で書き戻る。
- [ ] Core の細目（未）: `Resolve` の絶対パス（`~` / 環境変数の展開）と null / 絶対パス参照のときエクスポートに入らない / `ArchiveContents.Read` が `..` を含むエントリ名を捨てる / コピー元が既に `background/` の中なら `Import` が何もしない。

## 3. 実装時の注意

- `GetVisualDescendants()` でコントロールを探すときはヘッダー文字列に依存する。M5 ステップ 5（リソース化、2026-10-04 実施済み）で文字列は `Strings` に移り、テストも日本語リテラルではなく `Strings.XXX`（`Strings.Settings_Tab_Triggers`、`Strings.Common_RestartNow` 等）を参照している。テストのカルチャは `en` 固定。**新しいテストでも UI の文言を直書きしない**（`NoHardcodedJapaneseTests` は Desktop 本体だけを見るので、テスト側は自分で守る）。
- ヘッドレスではウィンドウの `Show()` 後も `Screens` が仮想（1 画面）。`PopupPlacement` の画面内補正は Core のテストで足りるので、UI テストでは位置の値を検証しない。
- `DispatcherTimer` はヘッドレスでも動くが実時間。デバウンスの検証は `SettingsHub` に「保存までの時間」を注入できるようにして短くするか、`Flush` で代える。
- 失敗したテストのスクリーンショットが欲しければ描画ありフィクスチャで `window.CaptureRenderedFrame()` を PNG に保存する（任意）。
- ファイル選択ダイアログは UI テストで使わない。背景画像は `SettingsWindow.ApplyBackgroundFileAsync(path)` または `BackgroundImageStore.Import` + `hub.Update` で入れる。テスト用の画像は `RenderTargetBitmap` で描いて `Save` する（`BackgroundTests.Png`）。`BoardBackgroundLoader` の非同期結果は `Dispatcher.UIThread.RunJobs()` を回しながら待つ（`BackgroundTests.Wait`）。
- `ChromeWindow` のテストでは `CloseButton` に `Button.ClickEvent` を `RaiseEvent` し、Esc は `KeyPressQwerty(PhysicalKey.Escape, …)`。`ShowDialog<T>` の結果は `Task` のまま受け取り、閉じたあと `IsCompleted` を見る。`ChromeWindow.ShowEffect` / `HideEffect` は static で既定 `EffectSpec.None`（演出なし）なので、他のテストは閉じる演出を待たずに済む。グリッチを試すテストだけ値を入れ、**必ず `finally` で元に戻す**（戻さないと後続のテストの `Close()` が演出を待って遅くなる / `IsCompleted` の前提が崩れる）。演出の完了は `Dispatcher.UIThread.RunJobs()` + `Thread.Sleep` を回して実時間で待つ（`ChromeWindowTests.Pump`）。
- CI（GitHub Actions、SPEC §10.4）では Windows / macOS の両方で `dotnet test FileLauncher.sln` を回す。

## 完了条件

§2 の観点がすべて [x] になり、`dotnet test FileLauncher.sln` が Windows / macOS の両方で通る。新しい UI 機能を足すときはこの一覧に観点を 1 行足してからテストを書く。
