# ポップアップの「カーソル位置」でカーソルを置く場所を 9 点から選ぶ（`popup.cursorAnchor`）

2026-10-10 ユーザー要望「ポップアップ項目で、カーソル位置のカスタマイズをしたい。いまはカーソルが上中央になっているが、左上、中上、右上、左、中、右、下左、下中、下右を 3×3 のボタンで選べるようにして」（原文の最後の「下中」は「下右」と解釈）。
同日の追加要望「マウスとキーボード用に分けてもらったけど、共通項目として独立させて」で、組ごとの 2 項目 → キーボード・マウス共通の 1 項目に変更（実装済み）。

正となる文書: SPEC §3.3「カーソルを盤面のどこに置くか（アンカー）」、`spec/SETTINGS.md` ポップアップタブ「盤面上のカーソルの位置」、`spec/TERMS.md`（`Settings_Popup_CursorAnchor`）。テスト観点は `issue/UI_TESTS.md`「カーソルのアンカー」。

## 決めたこと（要約）

- JSON: `popup.cursorAnchor`（キーボード・マウス共通の 1 項目）。enum `CursorAnchor` = `topLeft` / `top` / `topRight` / `left` / `center` / `right` / `bottomLeft` / `bottom` / `bottomRight`、既定 `top`（従来）。`schemaVersion` は 3 のまま。旧 `popup.keyboard.cursorAnchor` / `popup.mouse.cursorAnchor` は未リリースのためマイグレーションなし（残っていても読み飛ばす）。
- 位置: ウィンドウ左上 = `cursor − (ax, ay)`。`i = round(20 × s)`（s = Win `RenderScaling` / Mac 1）。左列 `ax = i`・中列 `w / 2`・右列 `w − i`、上段 `ay = i`・中段 `h / 2`・下段 `h − i`。そのあと `ClampInto`。= 見えている枠の 20 外側（中央は中心）。
- 常駐の `moveToCursorOnTrigger` は対象外（常に `top`）。

## タスク

1. [x] **Core `Model/AppSettings.cs`**
   - `public enum CursorAnchor { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }`（`PopupPosition` の下。XML コメント「カーソル位置表示で盤面のどの点をカーソルに合わせるか（SPEC §3.3）」）。値の並びは 3 × 3 の行優先（UI のグリッドの並びと同じ。`(int)anchor % 3` = 列、`/ 3` = 行で使えるようにする。コメントで明記）。
   - `PopupSettings.CursorAnchor { get; set; } = CursorAnchor.Top;`（共通の 1 項目。組の `PopupPlacementSettings` には持たせない。型名と同名のプロパティ。曖昧になる箇所は `Model.CursorAnchor` で修飾）。JSON は既存の camelCase の enum 変換で `popup` 直下の `"cursorAnchor": "topLeft"` 等になる。
   - `Normalize` への追加は不要（enum は不正値で読み込み失敗、SPEC §9.3）。1 → 2 のマイグレーションも触らない（キーが無ければ既定 `top`）。旧キー（組の中の `cursorAnchor`）は未知のキーとして読み飛ばされる。
2. [x] **Core `Popup/PopupPlacement.cs`**
   - `CursorOffsetFromTop` を **`CursorInset = 20`** に改名（XML コメント: 「カーソル位置表示で、辺・角のアンカーのときカーソルがウィンドウ外形からどれだけ内側に来るか（拡大率 1 のときの値）。ウィンドウには発光の余白 40 があるので、見えている枠の 20 外側になる。SPEC §3.3」）。
   - 新規 `public static int InsetFor(double scaling) => (int)Math.Round(CursorInset * scaling, MidpointRounding.AwayFromZero);`
   - 新規 `public static ScreenPoint CursorOrigin(ScreenPoint cursor, int width, int height, CursorAnchor anchor, int inset)`: 補正前のウィンドウ左上を返す純粋関数。列 = `(int)anchor % 3`（0: `inset`、1: `width / 2`、2: `width − inset`）、行 = `(int)anchor / 3`（同じく `height`）。`switch` で書いてもよいが式は SPEC §3.3 どおり。
   - `Compute` の末尾に引数 `CursorAnchor anchor = CursorAnchor.Top, double scaling = 1.0` を足し（シグネチャ `Compute(settings, cursor, w, h, workAreaAt, primary, anchor, scaling)`。アンカーは組の設定ではなく引数で受ける）、`default`（Cursor と座標未保存の退避）を `ClampInto(CursorOrigin(cursor, width, height, anchor, InsetFor(scaling)), width, height, workAreaAt(cursor))` に。
3. [x] **App `PopupController.Show`**: `PopupPlacement.Compute(..., primary, _settings.Popup.CursorAnchor, scaling)`。キーボード・マウスどちらの組で開いても同じ共通のアンカーを渡す。`scaling` は既に求めている `WindowScaling(...)`（Mac は 1）。
4. [x] **App `ResidentController.BringToFront`**: `PopupPlacement.Compute(new PopupPlacementSettings(), …)` をやめ、`PopupPlacement.ClampInto(PopupPlacement.CursorOrigin(c, w, h, CursorAnchor.Top, PopupPlacement.InsetFor(Scaling)), w, h, wa)` にする（常駐はアンカー設定の対象外であることをコメントで SPEC §3.3 を指して明記。既定値に頼らない）。
5. [x] **App `Settings/AnchorPicker.cs`（新規）**: 3 × 3 の選択コントロール。
   - `internal sealed class AnchorPicker : UserControl`。中身は `UniformGrid { Rows = 3, Columns = 3 }` に `ToggleButton` を 9 個（`Classes = { "anchor-cell" }`、26 × 26 DIP、間隔 2）。中身は 6 × 6 の `Border`（点。名前 `Dot`）を、そのボタンの位置に寄せて置く（左上のボタンなら点を左上寄せ = 盤面の縮図に見せる。`HorizontalAlignment` / `VerticalAlignment` を列・行から）。
   - `public CursorAnchor Value { get; set; }`（設定すると該当ボタンだけ `IsChecked = true`）と `public event Action<CursorAnchor>? ValueChanged`。ボタンのクリックで `Value` を変えて `ValueChanged`。**選択中のボタンを押しても外れない**（`IsChecked` を true に戻す。ラジオボタンの振る舞い）。
   - 各ボタン: `ToolTip.Tip` と `AutomationProperties.Name` = `EnumNames.Of(anchor)`。
   - キーボード: コンテナに `KeyboardNavigation.TabNavigation = Once`（Tab で 1 回だけ止まる。止まる先は選択中のボタン = `KeyboardNavigation.TabOnceActiveElement` を選択に合わせる）。矢印キーで隣へ選択とフォーカスを移す（端で止まる、折り返さない）。Space / Enter はフォーカス中のボタンを選ぶ（`ToggleButton` の既定のクリック）。
   - `IsEnabled = false` のとき全体が無効表示（親の `IsEnabled` で足りる）。
6. [x] **App `Themes/AppTheme.cs` `ChromeStyles`**: `ToggleButton.anchor-cell` のスタイル（`TabItem` のアウトラインと同じ流儀）: `BorderThickness 1`・`BorderBrush FlTabOutline`・`Background` 透明・`CornerRadius 0`・`Padding 3`。`:checked` で `BorderBrush FlAccent`・`Background FlAccentSoft`（Fluent の `:checked` は `PART_ContentPresenter` に主色の塗りを入れるので、`chrome-close` と同じく `Template().OfType<ContentPresenter>().Name("PART_ContentPresenter")` に対して上書きする。`:checked:pointerover` も同様）。点 `Border#Dot` の `Background` は未選択 `FlTabOutline`・選択中 `FlAccent`。`:disabled` は Fluent の既定の薄さで可（全体が `Opacity` で薄くなればよい）。`:focus-visible` でフォーカスの枠が見えること（Fluent 既定のフォーカス枠で可）。
7. [x] **App `Settings/SettingsWindow.cs` ポップアップタブ**
   - キーボード・マウスの 2 組（`PlacementGroup`）の下に、独立した 1 行 `Row(Strings.Settings_Popup_CursorAnchor, picker, Strings.Settings_Popup_CursorAnchor_Note)` を置く（`Sub` の字下げなし。組の中には置かない）。
   - `picker.ValueChanged += v => { if (!_refreshing) _hub.Update(s => s.Popup.CursorAnchor = v, SettingsChange.Popup); }`、`_refreshers` で `picker.Value = _hub.Current.Popup.CursorAnchor`（既存の `Combo` / `Number` ヘルパーと同じ再入防止に合わせる）。
   - 有効・無効: **キーボード・マウスの表示位置のどちらかが `PopupPosition.Cursor` なら有効、どちらも違えば無効表示**。どちらの組の表示位置の変更・`_refreshers`・`Activated` でも追従する。
   - 2 組の見た目と、共通の注記 `Settings_Popup_PositionNote` は変えない。
8. [x] **App `Localization/EnumNames.cs`**: `Localized` に `typeof(CursorAnchor)` を足す。
9. [x] **リソース**（両 resx、文言は `spec/TERMS.md` が正）: `Settings_Popup_CursorAnchor`（"Pointer spot on board" / 「盤面上のカーソルの位置」）、`Settings_Popup_CursorAnchor_Note`（"Used when “At pointer” is selected for either the keyboard or the mouse above (shared by both). Choose where on the board the pointer ends up." / 「上の表示位置で「カーソル位置」を選んだときに使います（キーボード・マウス共通）。盤面のどこにカーソルが来るように出すかを選びます。」）、`Enum_CursorAnchor_TopLeft` 〜 `_BottomRight` の 9 個（Top left / Top center / Top right / Middle left / Center / Middle right / Bottom left / Bottom center / Bottom right ／ 左上 / 上中央 / 右上 / 左中央 / 中央 / 右中央 / 左下 / 下中央 / 右下）。
10. [x] **テスト**: `issue/UI_TESTS.md`「カーソルのアンカー」の観点。既存の `PopupPlacementTests` の `CursorOffsetFromTop` 参照は `CursorInset` に置き換えるだけで通るはず（既定 `top`・`scaling` 省略 = 1 で従来と同じ値）。
11. [ ] **ユーザー確認（Mac）**: 共通の 1 項目で 9 点を切り替え、ホットキーとマウストリガーの両方で開いて、カーソルが選んだ位置（枠のすぐ外 / 中心）に来る / 画面の四隅の近くで開いてもはみ出さない / 既定（上中央）が今までと同じ位置 / 設定画面ではグリッドが 2 組の下に 1 つだけあり、Tab と矢印キーで選べる / キーボード・マウスの表示位置を両方「画面中央」にするとグリッドが無効表示、片方だけなら有効のまま。Windows は SPEC §13.3 の一括確認に含める（`issue/MAC_SUPPORT.md` Mac-0 に記載済み。150% で上中央が数 px 変わるのは仕様）。

（2026-10-10 タスク 1〜10 実装済み（同日、組ごと → 共通の 1 項目へ変更も実装済み）。残りはタスク 11 のユーザー確認）

## 完了条件

タスク 1〜10 が済み `dotnet test` が通り、タスク 11 のユーザー確認が済んだら、SETTINGS.md の「実装済み（…ユーザー確認待ち…）」から「ユーザー確認待ち」と issue への参照を外してこのファイルを削除する。
