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
- [x] 余白（`FlGlowMargin`）: ウィンドウが `盤面 + 40 × 2` 大きい（2026-10-06 に 34 → 40）（SPEC §3.6「発光のウィンドウ余白」。2026-10-04 のテーマ廃止で「ダークに戻すと 0」の半分は削除し、`ThemeTests` の該当テストは 34 固定の検証に書き換える）。**2026-10-06 設計で 34 → 40**（`issue/CHAMFER_GLOW.md`。`ThemeTests.盤面には発光の余白34が付く` を 40 に直し名前も変える。トーストは新トークン `FlToastMargin` = 34 のまま）。
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

### 八角形に沿う枠の発光（SPEC §3.6「発光の作り方」の「枠の発光」/ §3.9 H8。2026-10-06 設計・実装、`issue/CHAMFER_GLOW.md`）

Core は `FileLauncher.Tests/NeonProfileTests.cs`（描画なし）、Desktop は `NeonGlowTests.cs`（**描画ありフィクスチャ** = `UseSkia` + `UseHeadlessDrawing = false`。黒地の窓に面を透明にした `FrameChrome`（240 × 160、`Chamfer` 10、角ブラケットは隠す）を置いて `CaptureRenderedFrame` し、主色シアンの G（= 不透明度 × 229）を読む。ヘッドレスは scale 1 だけ。scale 2 は Mac の実機確認（issue ステップ 5）で見る）。静的な層は配置の丸め（枠の太さ 1.5 → 整数）で最大 0.5 px ずれるので、画素の比較は ±4 と ±20% の大きい方で許容する。

- [x] Core `NeonProfile`: `Erfcは0で1_3で十分小さく_単調に減る` / `σはAvaloniaのBoxShadowと同じ換算` / `合成は辺で各層の半分を重ねた値で_外へ単調に減る` / `盤面の発光は明滅の強で明るくしても30px以内で消えきる`（`Extent(…, 1.9)` が 24〜30）/ `帯は外側から内側へ隙間なく並び_帯の中央の合成で塗る`（**入れ子の塗り `Rings` は採らず、重ならない帯 `Bands` にした**: 入れ子に重ねると外側の薄い部分で 1 枚ごとの増分が 8 bit の刻みより小さくなり、計算より早く消えた。2026-10-06 実装時に画素テストで発見）。
- [x] Core `Octagon`: `八角形は8点で上辺の始まりから時計回り_面取りは辺の半分に丸める` / `オフセットした八角形の斜辺は元の斜辺から距離dだけ離れる`。
- [x] Core `OrbPath.Octagon`: `光の玉の八角形の経路は枠の辺の上を時計回りに一周する`（周長・始点・右上の斜辺の中点・1 周で戻る・全点が辺の上）。既存の角丸矩形のテストもそのまま通る。
- [x] Desktop 構造: `面取りでは発光をBorderのBoxShadowでなく八角形の層で描き_面取りなしでは従来どおり`。
- [x] Desktop 画素: `発光は辺から外へ単調に弱まり_窓の余白の内側で消えきる`（窓の端の 2 px 手前で ≤ 1、余白の規則 `Extent + 2 ≤ GetMargin()` も）/ `角で四角く光らず_斜辺の外も辺と同じ断面で光る`（切り落とした角が辺の 1 px 外の半分未満、斜辺の法線上の 3 点が上辺の同じ距離の値と一致）/ `内側の光は枠線のすぐ内側に染みて内へ弱まる` / `明滅の画像は静的な発光と同じ八角形の形`（`PulseLayer.BakedImage` と窓の画素が 5 点で一致）。
- [x] Desktop `OrbLayer`: `玉は面取りした角で斜辺の上を通る`、既存の `玉は枠線に沿って一周の半分で反対側へ進み_明滅は半周期で最大になる`（始点・半周の位置を面取りの枠線の中心 = 1.5 + 0.75 px に更新）。
- [x] 地の色を消す（背景画像あり）ときに結び付けが残り、表示時のリソースの解決で地の色に戻っていた不具合を修正（`FrameChrome.BindSurface`。画素テストで発見）。

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
- [ ] 未着手: `PulseLayer.Refresh` が大きさ・配色が同じなら焼き直さず、`FlBoardGlow` が変わる（配色の切替）と焼き直す（`BakedImage` の参照が変わることで確認。`Level = 0` のときは `Render` が何も描かない）。2026-10-06 から焼き直しの判定に `Chamfer` と余白も入る（`issue/CHAMFER_GLOW.md`）。
- [x] 2026-10-06 実装（`NeonGlowTests` / `AmbientEffectsTests.玉は面取りした角で斜辺の上を通る`）: 玉の経路が八角形（`OrbPath.Octagon`）で斜辺に乗る / `PulseLayer` の焼いた画像が静的な発光（`NeonGlowLayer`）と同じ断面。観点は上の「八角形に沿う枠の発光」節。

### 走査線（SPEC §3.9 H14「走査線の仕様」、EFFECTS.md `scanBeam`。2026-10-07 設計・同日ユーザー判断で確定（静止 ON・25% / 帯 **既定 ON** `beam / 4000` / 間隔 2・3・4 px 既定 3 / 帯はスロットの上）、`issue/SCANLINES.md`。未実装）

静止した走査線は `HudTests`（`ScanlineLayer` を `BoardWindow` から内部公開して見る）、帯は `AmbientEffectsTests`（`BoardWindow.ScanBeam` を内部公開し、`Ambient.Tick(ms)` で進めて子の `TranslateTransform.Y` を見る）、設定画面は `SettingsUiTests`、Core は `FileLauncher.Tests`。文言は `Strings.*` 参照。描画なしフィクスチャで足りる（画素は見ない）。**帯が既定 ON になるので、「既定では常時の演出が動かない」を前提にしている既存テストを先に直す**（下の「既存テストの書き換え」）。

- [ ] Core `AppSettings.Normalize`: `scanlineOpacity` を 0〜50・5 刻みに丸める（53 → 50、12 → 10、13 → 15、−5 → 0）/ `scanlinePitch` が 2 / 3 / 4 以外（0、1、5、−3）なら 3 に戻り、2 と 4 はそのまま / キーなし → `scanlines = true` / `scanlineOpacity = 25` / `scanlinePitch = 3`（JSON 往復）。
- [ ] Core `EffectCatalog`: **`scanBeam` の既定が `beam / 4000 / linear`**（`DefaultFor(ScanBeam)`、`Resolve` が `Effects` 空で `IsActive`）/ `Normalize` が `beam / 4000` を捨て（既定と同じ）、`none` は残し、500 → 2000、20000 → 12000 に丸め、`easing` が `linear` に / `beam` を `boardShow` に書くと捨てられる / `IsAmbient = true`、`DurationStepMs = 500` / `Animation = false` なら `Resolve` が `None`。
- [ ] Core `AmbientMath.SweepOffset(phase, faceHeight, bandHeight)`: phase 0 → `−bandHeight`、phase 0.5 → `(faceHeight − bandHeight) / 2`、phase 1 → `faceHeight`（帯の上端が面の下端）。
- [ ] Desktop `HudTests`: `hud.scanlines = true`（既定）で `ScanlineLayer.IsVisible` かつ `Opacity == 0.25` かつ `Pitch == 3`、`scanlines = false` で非表示、`scanlineOpacity = 0` で非表示、`50` で `Opacity == 0.5`、`scanlinePitch = 4` で `Pitch == 4` / `Frame` の中の `Grid` で `GridLayer` の直後・`DockPanel` の前の子にある / `IsHitTestVisible == false` / ウィンドウの `Width` / `Height` が走査線の ON / OFF・間隔で変わらない。
- [ ] Desktop `AmbientEffectsTests`: **既定（`Effects` 空）で `Render` → `Show()` すると `AmbientRunning == true`（帯だけで動く。玉・明滅は `none` のまま = `Orbs` の 2 個が非表示、`PulseLayer.Level == 0`）、`Hide()` で false** / `scanBeam = none` を明示すると既定では動かない（既存テストの前提の置き換え）/ `Tick(0)` で帯の `TranslateTransform.Y == −60`、`Tick(2000)` で `(面の高さ − 60) / 2`、`Tick(4000)` で `−60` に戻る（折り返し）/ 帯の `Rectangle.Width == ScanBeamLayer.Bounds.Width`、`Height == 60` / `AmbientRoot` の子の順が `PulseLayer` → `ScanBeamLayer` → `OrbLayer`（既存の「層の順」テストを拡張）/ `ScanBeamLayer.Clip` が `Chamfer > 0` で非 null（`FrameChrome.FaceClip` と同じインスタンス）/ `ScanBeamLayer` が `Frame` の子孫ではない（グリッチの静止画に写らないことの構造的な保証）/ `Animation = false`・`AmbientSuspended`・`ReducedMotion` で帯も止まる（既存テストに帯の観点を足す）。
- [ ] Desktop `SettingsUiTests`: 表示タブに「走査線」ToggleSwitch、「走査線の濃さ」`Slider`（`Minimum 0` / `Maximum 50` / `TickFrequency 5`）、「走査線の間隔」`ComboBox`（3 項目、表示が `string.Format(Strings.Common_PixelValue, 2/3/4)`、既定で 3 px が選択）があり、走査線を OFF にするとスライダーと `ComboBox` が無効・ON で戻る / スライダーの変更で `Changed(Hud)` が 1 回出て `ScanlineOpacity` が変わる、`ComboBox` で 2 px を選ぶと `ScanlinePitch == 2`・`Changed(Hud)` 1 回 / 「演出の調整…」に `Strings.Effect_scanBeam` の行があり、種類 `ComboBox` の選択肢が none / beam の 2 つで**既定の選択が `beam`**、スライダーが `2000`〜`12000` で表示が「4.0 s」、イージングが無効、「既定に戻す」が既定（`beam`）では無効で `none` にすると有効 / 注記 `Settings_Appearance_Effects_AmbientNote` が見える（文言は `Strings` 参照なので既存テストのまま）。
- [ ] 自動で掛かるもの: `StringsTests`（`Effect_scanBeam` / `Enum_EffectKind_Beam` / `Settings_Appearance_Hud_Scanlines(_Note)` / `Settings_Appearance_Hud_ScanlineOpacity` / `Settings_Appearance_Hud_ScanlinePitch` / `Common_PixelValue` が両 resx にあり、`{0}` の引数集合が一致）、`ThemeTests`（`FlHudScanline` が `TokenKeys` に入り辞書にある）、`NoHardcodedJapaneseTests`（`ScanlinePreview.cs` を削除したので `i18n:ignore` の行が減る）。
- [ ] **既存テストの書き換え**（帯の既定 ON に伴う）: Core `AmbientEffectsCoreTests.常時の演出はどのテーマでも既定で動かず_時間は演出ごとの範囲に丸める` → 「光の玉と明滅は既定で動かず_走査線の帯だけ既定で動き_時間は演出ごとの範囲に丸める」（`frameOrb` / `glowPulse` = none、`scanBeam` = beam / 4000 を検証）/ Desktop `AmbientEffectsTests.既定では動かず_アニメーションOFFや収納中やOSの設定でも止まる` → 既定で動く（帯）ことを検証する形に直し、「`scanBeam = none` なら既定では動かない」を 1 件足す。`Effects` 空のまま `Show()` している他のテスト（`HudTests` の時刻タイマー、`GlitchTests`、`BackgroundTests` 等）は `AmbientRunning` を見ていなければそのままでよいが、**`RequestAnimationFrame` が呼ばれないヘッドレスでも `Start()` で `Stopwatch` が動く**ので、`AmbientRunning` が true になって困るテストが無いか `dotnet test` で確かめる。

### フォルダを開く先（SPEC §5.2 / §6.2 / §6.3 / §6.6。2026-10-05 実装・Windows でユーザー確認済み。テストは `BoardEditingUiTests` 4 件・`ResidentTests` 1 件・`SettingsUiTests` 1 件・`FolderOpeningTests` 3 件。Windows のみの機能なので、OS で期待が分かれるテストは `OperatingSystem.IsWindows()` の分岐で両方の期待を書く）

- [x] Ctrl を押したままフォルダアイテムを動かさずにクリック → `ItemInvoked` が `newWindow = true` で 1 回、`FakePlatform.LastFolderTarget == NewWindow`。Ctrl なし → `false` / `ExistingTab`。ファイルアイテムの Ctrl+クリック → 通常の起動（`Launched` に入る）。Ctrl+4 px 以上のドラッグ → 複製で `ItemInvoked` は出ない（既存）。Mac では ⌃クリック = 右クリックで `ContextMenuRequested`。
- [x] 選択セルがフォルダのとき Ctrl+Enter → `newWindow = true`（Windows）。Mac では何も起きない。
- [x] 右クリックメニュー: Windows でフォルダアイテムに `Strings.Menu_OpenNewWindow` があり「起動」の直後、ファイルアイテムには無い。Mac では無い。「格納フォルダを開く」で `FakePlatform.Revealed` に `(LinkPath ?? Target, 設定値)` が入る。
- [x] 設定画面 一般タブ: Windows では `Strings.Settings_General_FolderOpenTarget` の行があり、`ComboBox` の 3 項目が `EnumNames.Of<FolderOpenTarget>` の順、「新しいウィンドウ」で `hub.Current.General.FolderOpenTarget == NewWindow`・`Changed(None)` 1 回・`Flush` で `"folderOpenTarget": "newWindow"`。Mac では行が無い。
- [x] Core（`FileLauncher.Tests/FolderOpeningTests.cs`）: `Resolve` / `PickWindow`（cloak・不可視を飛ばす、最小化は選ぶ、無ければ null）/ 設定の JSON 往復（キーなし → `existingTab`）。

### ターミナルで開く（SPEC §6.3「ターミナルで開く」/ §10.3 `IShellService.OpenTerminal`。2026-10-07 設計・同日実装、Windows でユーザー確認済み（Mac は `issue/MAC_SUPPORT.md` Mac-2）。Core は `FileLauncher.Tests/TerminalLocationTests.cs`、UI は `BoardEditingUiTests`。`FakePlatform.OpenedTerminals` に記録する。両 OS で同じ期待（OS 分岐なし））

- [x] Core `TerminalLocation.For`: フォルダ → `Target` そのもの（末尾の区切り `C:\work\` / `/Users/x/` は除く、ルート `C:\` / `/` はそのまま）/ ファイル・アプリ → `WorkingDir` が空なら `Target` の親、あれば `WorkingDir`（`~` / `%USERPROFILE%` が展開される）/ `.lnk` から登録した形（`LinkPath` あり）でも `Target` 側を使う（`LinkPath` は見ない）/ コマンド → `WorkingDir` があればそれ、空なら null / URL → null / 親の取れないパス（`"x.exe"` だけ）→ null。ファイル I/O をしない（存在しないパスでも値を返す）。
- [x] UI メニュー: フォルダ・ファイル・アプリのアイテムに `Strings.Menu_OpenTerminal` があり、`Loc.Os(Strings.Menu_Reveal_Win, Strings.Menu_Reveal_Mac)` の直後・`Separator` の直前に並ぶ / URL アイテムには無い / コマンドアイテムは `WorkingDir` が空だと無く、入れると出る / クリックで `FakePlatform.OpenedTerminals` に `TerminalLocation.For(item)` と同じ値が 1 件入る。
- [x] UI 失敗: `FakePlatform` に `OpenTerminalResult`（既定 `Ok`）を持たせ `Fail(NotFound, dir)` にしてクリック → トーストが 1 回（既存の `Toast` の表示回数のフックが無ければ、`Launched` / `OpenedTerminals` だけ見て落ちないことで代える）。盤面（`board.json`）は変わらない（`Flush` 後も同じ内容。`Commit` を通らない）。
- [x] 自動で掛かるもの: `StringsTests`（`Menu_OpenTerminal` / `Toast_OpenTerminalFailed` が両 resx にあり、後者の `{0}` `{1}` が一致）、`NoHardcodedJapaneseTests`。

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

### 背景画像のドロップ（SPEC §3.7「ドロップで設定」/ §10.5。2026-10-07 設計・同日実装、Windows でユーザー確認済み（`issue/BACKGROUND_DROP.md` は完了・削除）。Core は `BackgroundImageTests.cs`、UI は `BackgroundTests.cs`。OS の D&D セッションはヘッドレスで再現しないので、UI は `SettingsWindow.ApplyDroppedPathsAsync(paths)` から入る。両 OS で同じ期待）

- [x] Core `BackgroundImageStore.IsSupported`: `.png` / `.JPG` / `.webp` → true、`.svg` / `.txt` / 拡張子なし / フォルダ風のパス（`C:\pics\`）→ false。I/O をしない（存在しないパスでも判定する）。`FirstSupported([txt, png, jpg])` → png、`FirstSupported([txt, dir])` → null、空 → null。
- [x] UI ドロップ（`設定画面に画像を落とすと選択と同じように設定され_対応ファイルが無ければ変わらない`）: `ApplyDroppedPathsAsync([png])` で `Image == "background/<名前>"`・`background/` にコピー・「クリア」が有効 / `[txt, png]` で png が入る / `[txt]`・`[存在するフォルダ]`・`[]` で `Image` が変わらず例外が出ない（トーストは落ちないことで代える）/ 画像ありで別の png を落とすと差し替わり、`background/` のファイルが 1 つ。
- [x] UI 構造: 「背景画像」行のプレビュー（`Width == 120 && Height == 68` の `Border`）だけが `DragDrop.GetAllowDrop(...) == true`（「選択…」「クリア」のボタンとその親は false）/ 行の下に `Strings.Settings_Appearance_Background_Note` の `TextBlock` が見える / `Strings.Settings_Appearance_Background_DropHint` の `TextBlock` は通常は非表示。
- [x] 強調の切り替え: ヘッドレスで `DragEventArgs` / `DataTransfer` は組み立てないので、`internal void SetDropHighlight(bool)` を直接呼んで、プレビューの `BorderBrush` が `FlDropBorder` / `FlEmptySlotBorder` の値（リソース辞書から引いて比べる）と `DropHint` ⇄ `Common_None` の表示切替を見る。`DragOverEvent` を `RaiseEvent` する細目（`DragEffects == Copy`）は未（やるなら Avalonia の `DataTransfer` を組める版になってから）。
- [x] 自動で掛かるもの: `StringsTests`（`Settings_Appearance_Background_Note` / `Settings_Appearance_Background_DropHint` / `Toast_BackgroundDropUnsupported` が両 resx にある）、`NoHardcodedJapaneseTests`。

### 描画方式の固定と「ハードウェアアクセラレーション」の廃止（SPEC §10.7 / SETTINGS.md 詳細タブ。2026-10-07 ユーザー確定、未実装。Core は `AppSettingsTests` か `DataArchiveTests`、UI は `SettingsUiTests`）

- [ ] **既存テストの書き換え**: Core `DataArchiveTests.ハードウェアアクセラレーションの先読みは_falseのときだけOFFになる` は `SettingsPeek.HardwareAcceleration` ごと削除 / Desktop `SettingsUiTests.設定をリセットすると確認の後に既定値へ戻り_キャンセルなら戻らない` は `s.Advanced.HardwareAcceleration = false` を **`s.Advanced.Logging = true`** に、リセット後の検証を `Assert.False(_hub.Current.Advanced.Logging)` に、画面の読み直しは `ToggleSwitch` の `IsChecked == false`（詳細タブに残る ToggleSwitch は「ログを出力する」だけ）に直す。
- [ ] Core: `{ "advanced": { "hardwareAcceleration": false, "logging": true } }` の settings.json を `AppDataStore` で読むと `Status == Ok`（未知のキーで失敗しない）で `Advanced.Logging == true`、保存し直したファイルに `hardwareAcceleration` が無い（`schemaVersion` は 3 のまま。`editing.confirmDelete` の廃止と同じ規則）。
- [ ] UI: 詳細タブの `ToggleSwitch` が 1 つ（「ログを出力する」）/ `Strings.Common_RestartNow` の `Button` が詳細タブに無い（一般タブの「言語」にはある。既存の「言語を今と違う値にすると今すぐ再起動が出て_保存される」で担保）。
- [ ] 自動で掛かるもの: `StringsTests`（`Settings_Advanced_Hardware` / `_Note` を両 resx から消せばキー一致はそのまま通る）、`NoHardcodedJapaneseTests`。

### 常時の演出の CPU: 30 fps 化と背景画像の作り置き（SPEC §3.7「作り置き」/ §10.5 / §10.7、EFFECTS.md「時計」。2026-10-07 設計、未実装。`issue/AMBIENT_CPU.md`。Core は `BackgroundLayoutTests.cs`（新規）、Desktop は `AmbientEffectsTests.cs` と `BackgroundTests.cs`）

`RequestAnimationFrame` はヘッドレスでは呼ばれないので、間引きは `AmbientAnimator` の判定を直接呼んで見る（`internal bool ShouldTick(double frameMs)` のような純粋な判定を切り出す）。作り置きは `TestApp` が `UseSkia` + `UseHeadlessDrawing = false` なので `RenderTargetBitmap` が作れる = `BakedImage` の有無・大きさ・参照の変化で検証できる。画素は見ない。

- [ ] Desktop `AmbientEffectsTests`: `AmbientAnimator.MinFrameMs == 30` / 前回の更新から 29 ms のフレームでは更新せず 30 ms 以上で更新する（`ShouldTick`）/ `Start` 直後の最初のフレームは必ず更新する / `Tick(ms)` を直接呼ぶ既存テスト（玉の位置・帯の Y・`Level`）は間引きの影響を受けず従来どおり通る。
- [ ] Core `BackgroundLayout.Compute`（面 400×300、画像 800×400 で）: `fill` → `Dest` が面全体、`Source` が中央の 600×400（倍率 0.75 → 幅を切る）/ `fit` → `Source` が画像全体、`Dest` が 400×200 で縦中央（y = 50）/ `stretch` → 画像全体 → 面全体 / `center` → 画像のほうが大きいので `Source` が中央の 400×300、`Dest` が面全体。小さい画像（200×100）なら `Dest` が (100, 100, 200, 100) / `tile` → `Dest` が (0, 0, 800, 400)、`TileColumns == 1`、`TileRows == 1`。画像 64×32 なら `TileColumns == ceil(400/64) == 7`、`TileRows == ceil(300/32) == 10` / 面か画像の辺が 0 なら空（描かない）。
- [ ] Desktop `BackgroundTests`: `ApplyBackground(fill, 64×32 の画像)` → `Show()` の後 `Backdrop.BakedImage` が非 null で `PixelSize == ceil(Backdrop.Bounds × RenderScaling)`（ヘッドレスは scale 1）、`BakeCount == 1` / もう一度同じ設定で `ApplyBackground` しても `BakedImage` の参照と `BakeCount` が変わらない / `Fit` を `tile` に変えると参照が変わり `BakeCount == 2` / `ImageOpacity` だけ変えても変わらない / 行列数の違うページへ `Render` → `Bounds` が変わり参照が変わる / `bitmap = null` で `BakedImage == null` / `UsesFallback == false`。
- [ ] Desktop: 既存の「`fill` で `Backdrop.Background` が `ImageBrush`（`Stretch.UniformToFill`）」「`tile` で `TileMode.Tile` と `DestinationRect`」の検証は `Backdrop` が `Control` になるので **`Fit` / `Source` のプロパティと `BakedImage` の検証に書き換える**（`盤面に画像を敷くと覆いが重なり_並べるは原寸のタイル_なしに戻すと消える`）。`Backdrop.Opacity` を見ている画像の不透明度のテストは `Backdrop.ImageOpacity` に。
- [ ] 自動で掛かるもの: `NoHardcodedJapaneseTests`（ログ文字列は日本語のままでよい = `AppLog` は対象外）。

### VSync と常時の演出 3 つの既定オン（SETTINGS.md `appearance.vsync`、EFFECTS.md「時計」/「既定値の経緯」。2026-10-07 ユーザー判断、未実装。`issue/AMBIENT_SETTINGS.md`。Core は `AmbientEffectsCoreTests.cs` / `AppSettingsTests`、Desktop は `AmbientEffectsTests.cs` / `SettingsUiTests.cs`）

- [ ] Core `AmbientMath.FrameIntervalMs`: 1 → 13、2 → 30、3 → 47。範囲外（0、4）は丸めてから引く側（`Normalize`）の責任なので、ここでは 1 未満を 1、3 超を 3 として扱う（例外にしない）。
- [ ] Core `AppSettings.Normalize`: `appearance.vsync` 0 → 1、4 → 3、−1 → 1、2 → 2、キーなし → 2（JSON 往復）。`"vsync": 2` は保存されない？ → **他の int 項目と同じく常に書き出す**（`effects` の「既定と同じは保存しない」は演出の辞書だけの規則。`opacity` 等と同じ扱い）。
- [ ] Core `EffectCatalog`: **`frameOrb` の既定が `orb / 8000 / linear`、`glowPulse` が `pulse / 4000 / linear`、`scanBeam` が `beam / 4000 / linear`**（`DefaultFor`、`Resolve` が `Effects` 空で 3 つとも `IsActive`）/ `Normalize` が `orb / 8000` と `pulse / 4000` を捨て（既定と同じ）、`none` と `orbTwin / 8000` は残す。**既存テスト `光の玉と明滅は既定で動かず_走査線の帯だけ既定で動き_時間は演出ごとの範囲に丸める` → 「常時の演出 3 つは既定で動き_時間は演出ごとの範囲に丸める」に書き換え**。
- [ ] Desktop `AmbientEffectsTests`: `ShouldTick(last, frame, minFrameMs)` が `minFrameMs = 13` で 12.9 → false・13 → true・16.67 → true、`30` で 29 → false・30 → true・33.3 → true、`47` で 46 → false・50 → true・33.3 → false / `AmbientAnimator.Vsync` 既定 2、`ApplyEffects` で `appearance.Vsync = 1` を渡すと `Ambient.Vsync == 1`（`MinFrameMs == 13`）/ **既存テスト `既定では走査線の帯だけが動き_帯をなしにすると動かない` → 「既定では 3 つとも動き_すべてなしにすると動かない」**（`Effects` 空で `Render` → `Show()` → `AmbientRunning`、`Tick(0)` で `Orbs.OrbPositions` が 1 個・`PulseLayer.Level == 0`（位相 0）→ `Tick(2000)` で `Level ≈ 0.45`、`ScanBeam.BandVisible`。3 つとも `none` にすると `AmbientRunning == false`）/ `既定では動かず_アニメーションOFFや収納中やOSの設定でも止まる` → 名前を「3 つなしなら動かず_アニメーションOFFや収納中やOSの設定でも止まる」に（中身の `Board(orbOn: false, beamOn: false)` は `pulseOn: false` も要る）。`MinFrameMs == 30` の定数テストは `FrameIntervalMs(2) == 30` に。
- [ ] Desktop `SettingsUiTests`（**演出タブ**。SPEC §7「演出タブの構成」。リソースキーは改名後の `Settings_Effects_*`）: `TabControl` の `TabItem` が 8 つで 3 番目の見出しが `Strings.Settings_Tab_Effects` / 演出タブを選ぶと、折りたたみを開かずに `Strings.Effect_frameOrb` / `Effect_glowPulse` / `Effect_scanBeam` の `TextBlock` が `IsEffectivelyVisible`（= 普通の行）で、その 3 行には `EasingKind` の `ComboBox` が無い / `Expander` が 1 つ（見出し `Strings.Settings_Effects_Advanced`）で、開くと `Strings.FormatSettings_Effects_Row(...)` の行が `EffectCatalog.All.Count(d => !d.IsAmbient)`（= 11）個、常時の演出 3 つの行は中に無い / 表示タブに `Strings.Settings_Effects_Animation` と `Expander` が無い / `Strings.Settings_Effects_Vsync` の行があり `ComboBox` が 3 項目（表示は `string.Format(Strings.Settings_Effects_Vsync_Item, n, fps)`）で既定の選択が 2 番目（値 2）、1 を選ぶと `Changed(Effects)` が 1 回出て `Appearance.Vsync == 1` / 「すべて既定に戻す」（`Strings.Settings_Effects_ResetAll`、`Expander` の外）で `Vsync == 2` に戻り `Effects` が空、`Animation` は変わらない / 注記 `Settings_Effects_Vsync_Note` と `Settings_Effects_AmbientNote` が見える / 「アニメーション」OFF で 3 行・VSync・`Expander`・ボタンが `IsEnabled == false`、注記の `TextBlock` は `IsEnabled` のまま、ON で戻る / 常時の演出 3 行の種類 `ComboBox` の既定の選択が `orb` / `pulse` / `beam`、「既定に戻す」が既定では無効 / **英語カルチャで 8 つの `TabItem` の右端が本体の幅（620）に収まる**（`TranslatePoint` で測る。既存のはみ出しテスト（行の中身）と同じ流儀でタブ見出しにも適用）。
- [ ] Desktop `AmbientEffectsTests.設定画面に光の玉と発光の明滅の行と注記がある` → **演出タブを選び、`Expander` を開かずに** `Strings.Effect_glowPulse` の `TextBlock` が見えることと `Strings.Settings_Effects_AmbientNote` があることを見る形に書き換え（今は表示タブの `Expander` を開いて `FormatSettings_Appearance_EffectRow(...)` を探している）。
- [ ] 自動で掛かるもの: `StringsTests`（`Settings_Tab_Effects` / `Settings_Effects_AmbientHeading` / `Settings_Effects_Advanced` / `Settings_Effects_Vsync` / `_Item` / `_Note` と改名した `Settings_Effects_*` が両 resx にあり、旧 `Settings_Appearance_Animation*` / `Settings_Appearance_Effect*` が残っていない、`{0}` `{1}` の引数集合が一致）、`NoHardcodedJapaneseTests`。
- [ ] `Effects` 空のまま `Show()` している他のテスト（`HudTests` / `GlitchTests` / `BackgroundTests` 等）は、既定で玉・明滅も動くようになるが `AmbientRunning` を見ていなければそのままでよい。`PulseLayer.Refresh` が既定で呼ばれるようになる（`DispatcherPriority.Loaded`）ので、描画ありフィクスチャで例外が出ないことを `dotnet test` で確かめる。

### 静止した走査線の 3 行を演出タブへ（SPEC §3.9「走査線の仕様」の「設定 UI」/ §7 (7)、SETTINGS.md 演出タブ。2026-10-07 夕方のユーザー指摘、未実装。`issue/AMBIENT_SETTINGS.md` ステップ 3b。設定画面だけの変更なので Core と `HudTests`（盤面の `ScanlineLayer`）はそのまま）

- [ ] Desktop `SettingsUiTests.走査線の行は外観タブにあり_オフで濃さと間隔が無効になり_間隔を選ぶと保存される` → **「走査線の行は演出タブの末尾にあり_オフで濃さと間隔が無効になり_間隔を選ぶと保存される」に書き換え**: `SelectTab(w, Strings.Settings_Tab_Effects)` に変え、行名は `Strings.Settings_Effects_Scanlines`（旧 `Settings_Appearance_Hud_Scanlines`）で探す。中身（`ComboBox` 既定 3 px → 4 px で `ScanlinePitch == 4`・`"scanlinePitch": 4` が保存・ToggleSwitch OFF で `Scanlines == false` かつ `pitch.IsEnabled == false`）はそのまま。
- [ ] Desktop `SettingsUiTests`（新規 1 件）「静止した走査線は演出タブの末尾にあり_表示タブには無く_アニメーションOFFでも有効で_すべて既定に戻すで変わらない」: 演出タブの `StackPanel` の子の順で、`Strings.Settings_Effects_ResetAll` の `Button` より**後**に `Strings.Settings_Effects_ScanlinesHeading` の `TextBlock` と `Strings.Settings_Effects_Scanlines` の行がある / 表示タブを選んで `Strings.Settings_Effects_Scanlines` / `_ScanlineOpacity` / `_ScanlinePitch` の `TextBlock` が無い / `Appearance.Animation = false` にして `RefreshAll`（ToggleSwitch を OFF）→ 走査線の ToggleSwitch・濃さの `Slider`・間隔の `ComboBox` は `IsEffectivelyEnabled == true` のまま（常時の演出 3 行・VSync・`Expander`・ボタンは false）/ `ScanlineOpacity = 10`・`ScanlinePitch = 2` にしてから「すべて既定に戻す」を押すと `Effects` が空・`Vsync == 2` になるが **`Hud.Scanlines` / `ScanlineOpacity == 10` / `ScanlinePitch == 2` は変わらない**。
- [ ] 自動で掛かるもの: `StringsTests`（`Settings_Effects_Scanlines` / `_Note` / `Settings_Effects_ScanlineOpacity` / `Settings_Effects_ScanlinePitch` / `Settings_Effects_ScanlinesHeading` が両 resx にあり、旧 `Settings_Appearance_Hud_Scanline*` が残っていない）、`NoHardcodedJapaneseTests`、既存のはみ出しテスト（全タブ・全 `Expander` を開いて右端を見る。演出タブの末尾に 3 行増えても自動で対象）。
- [ ] 上の「走査線」節（2026-10-07 午前の設計）の `SettingsUiTests` の項目で「表示タブに…」とある箇所と `StringsTests` の旧キー名は、この節の内容で読み替える（表示タブ → 演出タブの末尾、`Settings_Appearance_Hud_Scanline*` → `Settings_Effects_Scanline*`）。

### 常時の演出の負荷削減（A + B。SPEC §3.6「作り置き」/ §10.5 / §10.7 の 2026-10-08 の項、EFFECTS.md `glowPulse` の「作り方」。2026-10-08 設計、未実装。`issue/AMBIENT_RENDER_OPT.md`。Desktop は `NeonGlowTests.cs`（描画ありフィクスチャ）と `AmbientEffectsTests.cs`）

画素テストは既存の「八角形に沿う枠の発光」節と同じ流儀（黒地の窓、主色シアンの G、許容 ±4 / ±20%）。`Draw(setup, bake)` ヘルパーに焼き込みの有無を足す。`UseRegionDirtyRectClipping` はアプリの設定なのでテストしない。

- [ ] 既存の画素テスト 3 件（`発光は辺から外へ単調に弱まり_窓の余白の内側で消えきる` / `角で四角く光らず_斜辺の外も辺と同じ断面で光る` / `内側の光は枠線のすぐ内側に染みて内へ弱まる`）を `[AvaloniaTheory]` + `[InlineData(false)]` / `[InlineData(true)]`（`FrameChrome.BakeGlow`）にして**ベクタと焼き込みの両方で通す**。
- [ ] `焼いた発光はベクタ描画と同じ画素`: bake なし / あり を撮り、上辺の法線 10 点・左上の斜辺の法線 3 点・内側 3 点で一致。
- [ ] `焼き込みは大きさ_面取り_影_拡大率が変わったときだけ作り直す`: `OuterGlow.BakeCount` / `BakedImage` の参照が `InvalidateVisual` + `RunJobs` では変わらず、`Width` 変更・`Shadows` を別の `BoxShadows` に・`Chamfer = 12` で変わる（それぞれ 1 回ずつ増える）。
- [ ] `焼いた画像の余白は発光が消えきる距離以上`: `OuterGlow.BakePad >= NeonProfile.Extent(外側 3 層, AmbientAnimator.MaxPulseGain)`、`BakedImage.PixelSize == ceil((W + 2 pad) × scale)`、`InnerGlow.BakePad == 1`。
- [ ] `面取りなしと窓から外したときは焼き込みを持たない`: `Chamfer = 0` で層が非表示、`window.Close()` 後に `BakedImage == null`。
- [ ] `盤面の枠は焼き_設定画面の枠は焼かない`: `BoardWindow` を `Show` → `Chrome.BakeGlow == true`・`Chrome.OuterGlow.BakedImage != null`。`ChromeWindow` → `Chrome.BakeGlow == false`・`BakedImage == null`。
- [ ] `明滅の帯は重ならず隙間なく枠を囲み_中央は描かない`: `PulseLayer.Refresh` 後、`Strips` が 4 本、`Bounds` の交差が空、上下の幅 = 画像の論理幅、左右の高さ = 画像の論理高さ − 2 × `StripDepth`、`StripDepth × scale` が整数、`StripDepth − margin >= NeonGlowPlan.Extents(FlBoardGlow).Inner`。`PulseLayer` 自身の `Render` は何も描かない（`Level = 1` で `PulseLayer` だけを `Overlays` に置き、帯を全部 `IsVisible = false` にすると窓が黒いまま）。
- [ ] `明滅の帯の描画は焼いた画像と同じで継ぎ目に線が出ない`: 静的な発光（`OuterGlow` / `InnerGlow`）を隠し、`PulseLayer`（`Level = 1`）だけを描いて撮る。上辺の法線上 5 点・**左右の帯と上の帯の継ぎ目（`y = top − margin + StripDepth` の ±1 px、x は左辺の 2 px 外）**・角 `(top, top)` で窓の画素 == `BakedImage` の画素。中央 `(W/2, H/2)` は 0。
- [ ] `明滅のLevelが0なら帯は何も描かない`: `Level = 0` で上辺の外側の画素が 0。
- [ ] 既存 `明滅の画像は静的な発光と同じ八角形の形`（`BakedImage` を見る）と `AmbientEffectsTests` の `PulseLayer.Level` のテストはそのまま通る。`GlitchTests`（静止画に焼いた発光を `DrawImage` する経路）と `BackgroundTests` が描画ありフィクスチャで例外を出さない。
- [ ] 自動で掛かるもの: `NoHardcodedJapaneseTests`（ログ「枠の発光の作り置き」は `AppLog` なので対象外）。`grep "MEASURE-TEMP\|CPFL_"` が 0 件であることは実装者が手で確認（テストにはしない）。

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
