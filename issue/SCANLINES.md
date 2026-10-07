# 走査線（静止した走査線 + 走査線の帯）の正式採用

ユーザー要望（2026-10-04「後ほど検討」）→ 2026-10-06〜07 に一時的な試し表示 `src/FileLauncher.Desktop/Themes/ScanlinePreview.cs`（環境変数で起動時だけ有効）で Mac（Retina）の実物を比べ、ユーザーが「今の感じ良いです」と選んだ組み合わせ（録画で確認済み）を製品の形にする。

正となる文書: **SPEC §3.9 H14 と「走査線の仕様」**（置き場所・値・層・高 DPI）、`spec/EFFECTS.md`「常時の演出の詳細」の **`scanBeam`** の表（帯のパラメータ・駆動・既定）、`spec/SETTINGS.md` 表示タブ（`appearance.hud.scanlines` / `scanlineOpacity` / `scanlinePitch`）と「演出の調整…」、`spec/TERMS.md`（訳語・注記）、`issue/UI_TESTS.md`「走査線」（テスト観点と既存テストの書き換え）、SPEC §11（CPU 目標の適用先）、SPEC §13.3 **C23**（Windows 側の確認）。

設計日: 2026-10-07（designer）。**同日、要ユーザー判断 4 件に回答済み（下の「ユーザー判断（確定）」）。ステップ 1〜4 実装済み（同日）。残りはステップ 5 の Mac 確認。** 実装メモ: 帯の切り抜き `FrameChrome.FaceClip` は中身（`_layers`）の位置と大きさから作る。表示遅延のログ確認（ステップ 4 の 3 項目め）は Mac 確認のときに見る。

---

## 決めたこと（要約。ユーザー判断を反映済み）

- 走査線は **2 つの独立した要素**。設定の置き場所も違う:
  - **静止した走査線 = HUD の見た目**（`appearance.hud.scanlines` bool **既定 true** / `appearance.hud.scanlineOpacity` int % **0〜50、5 刻み、既定 25** / **`appearance.hud.scanlinePitch` int px 2 / 3 / 4、既定 3**（それ以外は 3 に戻す））。層 `Themes/ScanlineLayer.cs` は `GridLayer` と同じ作りで、**`BackgroundGrid` の直後・`DockPanel` の下**（面・背景画像・覆い・グリッドの上、アイコン・文字の下。八角形の切り抜きは `_content.Clip` で自動）。新トークン **`FlHudScanline`** = 主色（アルファなし）。静的で CPU は増えない。`appearance.animation` とは無関係。
  - **走査線の帯 = 常時の演出 `scanBeam`**（`appearance.effects.scanBeam`。種類 none / **`beam`**、時間 = 1 回流れ切る長さ **2000〜12000 ms、500 刻み**、イージングなし、**既定 `beam / 4000` = 既定 ON**）。帯は高さ **60 px**・幅 = 面の幅、主色の縦グラデーション（上端 α0 → 85% で α28 → 下端 α60）、`y = −60 + phase × (面の高さ + 60)` で上から下へ一定速度、途切れず繰り返す。層 `Effects/ScanBeamLayer.cs` は **`AmbientRoot` の中、`PulseLayer` と `OrbLayer` の間**（= `Overlays`。`Frame` の外なのでグリッチの静止画に写らない。**スロット・タブ・ステータス行の上**）。**`Clip` は面の八角形**（`FrameChrome.FaceClip` を新設）。駆動は **`AmbientAnimator`**（Avalonia `Animation` は使わない）。動く条件・フェードイン・停止は玉・明滅と同じ規則。
- **帯の既定 ON は 2026-10-04 の案 A（常時の演出は既定オフ）の唯一の例外**（ユーザー判断。CPU が光の玉並みの 20% 台でも既定オン）。帰結: `EffectCatalog` の `Default` が `beam / 4000 / linear`、「既定と同じは保存しない」規則により帯を止めた settings.json には `scanBeam: none` が保存される、既存ユーザー（キーなし）も次回起動から帯が流れる（マイグレーションなし）、注記 `AmbientNote` を「…走査線の帯以外は既定ではオフです。」に、**SPEC §11 の「表示中 3% 以下」は「常時の演出をすべて「なし」にした構成」に適用し、既定構成（帯あり）の表示中 CPU は目標なしで実測を記録**（「隠した後 0.5%」は既定構成のまま適用）。
- 設定画面・ダイアログ（`ChromeWindow`）・トーストには付けない。
- 試し表示 `ScanlinePreview.cs` と `BoardWindow` の呼び出し 1 行は**ステップ 4 で削除**する。

## ユーザー判断（確定、2026-10-07）

| # | 問い | 回答 | 反映先 |
|---|---|---|---|
| 1 | 静止した走査線の既定 | **ON・25%**（推奨どおり） | `HudSettings.Scanlines = true` / `ScanlineOpacity = 25` |
| 2 | 帯 `scanBeam` の既定 | **ON（`beam / 4000`）**。CPU が光の玉並みでも既定オン。案 A の唯一の例外として記録 | `EffectCatalog` の `Default`、注記の文言、SPEC §11 の目標の適用先、既存テストの書き換え |
| 3 | 間隔 | **選べる（2 / 3 / 4 px）**、既定 3 | `appearance.hud.scanlinePitch`、`ScanlineLayer.Pitch` を `StyledProperty` に、設定画面に `ComboBox`、注記から「3 px ごとに」を外す |
| 4 | 帯の位置 | **スロットの上**（推奨どおり） | `AmbientRoot` の中（変更なし） |

---

## ステップ 1: Core

- [x] `Core/Model/AppSettings.cs` `HudSettings`: `bool Scanlines { get; set; } = true;`、`int ScanlineOpacity { get; set; } = 25;`、`int ScanlinePitch { get; set; } = 3;`、`public static readonly int[] ScanlinePitches = [2, 3, 4];`（XML コメントに SPEC §3.9 H14）。`AppSettings.Normalize` に `Appearance.Hud.ScanlineOpacity = (int)Math.Round(Math.Clamp(…, 0, 50) / 5.0, MidpointRounding.AwayFromZero) * 5;`（`Opacity` / `Overlay` と同じ書き方）と `if (!HudSettings.ScanlinePitches.Contains(Appearance.Hud.ScanlinePitch)) Appearance.Hud.ScanlinePitch = 3;`（`ButtonSize` と同じ書き方）。
- [x] `Core/Effects/EffectSpec.cs` `EffectKind` に **`Beam`**（XML コメント「常時の演出専用: 走査線の帯が面を上から下へ流れ続ける（scanBeam）」）。JSON は camelCase（既存の `JsonStringEnumConverter` の設定どおり `beam`）。
- [x] `Core/Effects/EffectCatalog.cs`: `public const string ScanBeam = "scanBeam";`、`All` の末尾（`GlowPulse` の後）に `new(ScanBeam, Spec(EffectKind.Beam, 4000, EasingKind.Linear), [EffectKind.None, EffectKind.Beam], IsAmbient: true, MinDurationMs: 2000, MaxDurationMs: 12000, DurationStepMs: 500, UsesEasing: false)`。コメントに「**既定 ON は 2026-10-07 ユーザー判断で、案 A（常時の演出は既定オフ）の唯一の例外**。SPEC §3.9 H14」。`Normalize` の変更は不要（既定と同じ `beam / 4000` は捨てられ、`none` は残る = 望みどおり）。`DefaultFor` / `Resolve` のコメント「そのテーマでの既定値」はテーマ廃止後の名残なので直してよい。
- [x] `Core/Effects/AmbientMath.cs`: `public static double SweepOffset(double phase, double faceHeight, double bandHeight) => -bandHeight + phase * (faceHeight + bandHeight);`。
- [x] テスト `FileLauncher.Tests`: `AppSettingsTests`（濃さの丸め 53 → 50、12 → 10、13 → 15、−5 → 0 / 間隔 0・1・5・−3 → 3、2 と 4 はそのまま / キーなし → true / 25 / 3）、`AmbientEffectsCoreTests`（**既存テスト「常時の演出はどのテーマでも既定で動かず_時間は演出ごとの範囲に丸める」を「光の玉と明滅は既定で動かず_走査線の帯だけ既定で動き_…」に書き換え**: `scanBeam` の既定 `beam / 4000`、`Normalize` が `beam / 4000` を捨て `none` を残す、範囲の丸め、`beam` を `boardShow` に書くと捨てられる、`SweepOffset` の 3 点）。観点は `issue/UI_TESTS.md`「走査線」。

## ステップ 2: App（静止した走査線）

- [x] `Themes/AppTheme.cs`: 辞書に `["FlHudScanline"] = B(p.Accent)`（`FlHudGrid` の隣。コメント「濃さは設定 appearance.hud.scanlineOpacity（ScanlineLayer.Opacity。既定 25%）」）。`TokenKeys` は自動で拾う。SPEC §3.6 のトークン表は更新済み。
- [x] `Themes/ScanlineLayer.cs`（新規。`GridLayer` を写して横線だけに）: `StyledProperty<IBrush?> Stroke` と **`StyledProperty<double> Pitch`（既定 3、`AffectsRender` に両方）**、`IsHitTestVisible = false`、`ClipToBounds = true`、`Render` で `double p = Math.Max(1, Pitch); for (double y = 0; y < Bounds.Height; y += p) DrawLine(pen, (0, y + 0.5), (Bounds.Width, y + 0.5))`。`Pen` はブラシが差し替わったときだけ作り直す（`GridLayer` と同じ）。`GridLayer.cs` のクラスコメント「主色 7%」は古いので、ついでに「濃さは設定 `gridOpacity`」に直してよい。
- [x] `BoardWindow.axaml`: `<local:GridLayer x:Name="BackgroundGrid" …/>` の**直後**に `<local:ScanlineLayer x:Name="Scanlines" Stroke="{DynamicResource FlHudScanline}" />`（`DockPanel` より前）。コメント「静止した走査線（SPEC §3.9 H14）: グリッドの上、タブとスロットの下」。
- [x] `BoardWindow.axaml.cs` `RenderPage`: `BackgroundGrid` の 2 行の隣に `Scanlines.IsVisible = _appearance.Hud.Scanlines && _appearance.Hud.ScanlineOpacity > 0; Scanlines.Opacity = _appearance.Hud.ScanlineOpacity / 100.0; Scanlines.Pitch = _appearance.Hud.ScanlinePitch;`。ウィンドウの大きさの計算は変えない。テスト用に `internal ScanlineLayer ScanlineLayer => Scanlines;`。
- [x] `Settings/SettingsWindow.cs` `AppearanceTab`: 「グリッドの濃さ」の直後に `Toggle`（`Strings.Settings_Appearance_Hud_Scanlines` + `_Note`、`SettingsChange.Hud`）、`Sub(Strings.Settings_Appearance_Hud_ScanlineOpacity)` の `ValueSlider(0, 50, 5, "%", …, SettingsChange.Hud)`、`Sub(Strings.Settings_Appearance_Hud_ScanlinePitch)` の `Combo(HudSettings.ScanlinePitches.Select(p => (string.Format(Strings.Common_PixelValue, p), p)), s => s.Appearance.Hud.ScanlinePitch, …, SettingsChange.Hud)`（`ButtonSize` の `Combo` と同じ形）。走査線 OFF でスライダーと `ComboBox` を無効（`UpdateGridEnabled` と同じ形で `UpdateScanlinesEnabled` を `_refreshers` に）。
- [x] 文言（`Strings.resx` / `Strings.ja.resx`。訳は `spec/TERMS.md`）: `Settings_Appearance_Hud_Scanlines` = "Scanlines" / 「走査線」、`Settings_Appearance_Hud_Scanlines_Note` = "Thin horizontal lines in the primary color behind the slots." / 「スロットの下に主色の横線を敷きます。」、`Settings_Appearance_Hud_ScanlineOpacity` = "Scanline strength" / 「走査線の濃さ」、`Settings_Appearance_Hud_ScanlinePitch` = "Scanline spacing" / 「走査線の間隔」、**`Common_PixelValue` = "{0} px"（両言語同じ値。既に同等のキーがあればそれを使う）**。
- [x] テスト `HudTests` / `SettingsUiTests`（観点は `issue/UI_TESTS.md`「走査線」）。

## ステップ 3: App（走査線の帯）

- [x] `Themes/FrameChrome.cs`: `public Geometry? FaceClip { get; private set; }` と `public event EventHandler? FaceClipChanged;`。`UpdateChamferGeometry` で `_content.Clip` を作るときに、同じ八角形を **`Frame` 座標へ枠の太さ t だけ平行移動**したもの（`NeonGlowPlan.Octagon(new Rect(t, t, size.Width, size.Height), _chamfer)`。`size` は `_content.Bounds.Size`）を `FaceClip` に入れて `FaceClipChanged` を出す。`Chamfer = 0` のときは `RectangleGeometry(new Rect(t, t, w, h), r, r)`（`FlBoardCornerRadius`）。`Frame` と `Overlays` は同じセルで同じ大きさなので、この座標は `Overlays` の座標でもある。
- [x] `Effects/ScanBeamLayer.cs`（新規）: `sealed class ScanBeamLayer : Canvas`。`public const double BandHeight = 60;`、子 `Rectangle _band`（`Height = BandHeight`、`IsHitTestVisible = false`、`RenderTransform = new TranslateTransform()`）。`IsHitTestVisible = false`、`ClipToBounds = true`。`public EffectSpec Spec { get; set; } = EffectSpec.None;`、`public double Phase { get; private set; }`、テスト用 `internal double BandY`（`TranslateTransform.Y`）。`SizeChanged` で `_band.Width = Bounds.Width`。`public void Advance(double elapsedMs)`: `Phase = AmbientMath.Phase(elapsedMs, Spec.DurationMs)`; `_band.IsVisible = Spec.Kind == EffectKind.Beam && Bounds.Height >= 1`; 色の更新（`FlAccent` を `OrbLayer.Token` と同じ方法で引き、変わったときだけ `LinearGradientBrush`（`StartPoint (0,0) → EndPoint (0,1)` Relative、stops `α0 @0` / `α28 @0.85` / `α60 @1`）を `ToImmutable()` して `_band.Fill` に）; `((TranslateTransform)_band.RenderTransform).Y = AmbientMath.SweepOffset(Phase, Bounds.Height, BandHeight)`。層全体の `InvalidateVisual` はしない。
- [x] `Effects/AmbientAnimator.cs`: コンストラクタに `ScanBeamLayer beam` を足し、`Tick` で `beam.Advance(elapsedMs)`。クラスコメントを「光の玉・発光の明滅・走査線の帯」に。
- [x] `BoardWindow.axaml.cs`: `_beamLayer = new ScanBeamLayer()` を作り、`_ambientRoot.Children` を **`_pulseLayer` → `_beamLayer` → `_orbLayer`** の順に。`_beamLayer.Clip = Chrome.FaceClip` を初期化時と `Chrome.FaceClipChanged` で付け直す。`ApplyEffects` で `_beamLayer.Spec = EffectCatalog.Resolve(appearance, EffectCatalog.ScanBeam)`。`UpdateAmbient` の `any` に `|| _beamLayer.Spec.IsActive`。テスト用 `internal ScanBeamLayer ScanBeam => _beamLayer;`。コメント（「常時の演出（明滅 → 光の玉）」）を 3 層に直す。
- [x] 文言: `Effect_scanBeam` = "Scan beam" / 「走査線の帯」、`Enum_EffectKind_Beam` = "Beam" / 「帯」、**`Settings_Appearance_Effects_AmbientNote` を "Light orb, glow pulse, and scan beam run the whole time the board is visible and use a lot of CPU (about 20–30%). All but the scan beam are off by default." / 「光の玉・発光の明滅・走査線の帯は、盤面が見えている間ずっと動き、CPU を多く使います（20〜30% 程度）。走査線の帯以外は既定ではオフです。」に変更**。
- [x] 設定画面「演出の調整…」は `EffectCatalog.All` から行を作るので追加の実装は不要のはず（`IsAmbient` で秒表示・イージング無効が効く。既定が `beam` なので行の初期選択が「帯」、「既定に戻す」は無効で出る）。`EffectsExpander` に ID の直書き（`glowPulse` の行の出し分けの名残など）があれば `scanBeam` も通ることを確認。
- [x] テスト `AmbientEffectsTests` / `SettingsUiTests`（観点は `issue/UI_TESTS.md`「走査線」。**既存の「既定では動かず_アニメーションOFFや収納中やOSの設定でも止まる」は既定で帯が動く形に書き換え、「`scanBeam = none` なら既定では動かない」を足す**。「層の順は明滅と玉が…」を 3 層に拡張）。`Effects` 空のまま `Show()` している他のテストで `AmbientRunning` が true になって困るものが無いか `dotnet test` で確かめる。

## ステップ 4: 試し表示の削除と仕上げ

- [x] `src/FileLauncher.Desktop/Themes/ScanlinePreview.cs` を**削除**し、`BoardWindow.axaml.cs` の `ScanlinePreview.Attach(…)` の 1 行を消す。環境変数 `CPFL_SCANLINES*` / `CPFL_SCANBAND` への言及がコード・文書に残っていないことを grep で確認。
- [x] `cd src && dotnet build FileLauncher.sln && dotnet test FileLauncher.sln` が緑（`StringsTests` の両 resx 一致、`ThemeTests` の `FlHudScanline`、`NoHardcodedJapaneseTests`、書き換えた既存テスト）。
- [ ] Debug ログの「表示まで n ms」が導入前と変わらないことを 2〜3 回見る（層は構築時に作るので変わらないはず。帯は表示演出の後に動き出すので表示遅延には入らない）。
- [x] SPEC §3.9「走査線の仕様」/ §10.5、EFFECTS.md、SETTINGS.md、TERMS.md の「未実装」を「実装済み（ユーザー確認待ち）」に。

## ステップ 5: Mac でのユーザー確認（Release `.app`、`publish-mac.sh`）

- [ ] **再現**: 新規の settings.json（または設定のリセット）で盤面を出すと、試し表示で選んだ見た目になっている = 3 px おき・25% の横線がグリッドの上・アイコンの下にあり、**同時に 60 px の帯が 4 秒で上から下へ流れ続ける**（既定 ON。スロット・タブ・ステータス行の上、角で八角形に切れる）。
- [ ] **静止した走査線**: 「走査線」OFF で消え、「走査線の濃さ」0〜50% で即時に変わり、「走査線の間隔」2 / 3 / 4 px で間隔が変わる（2 px は Retina で密、4 px は粗い。濃さとの組み合わせで見え方が変わることを一度見る）。OFF の間はスライダーと `ComboBox` が無効。配色レッドで線が赤に。背景画像ありで画像の上に線、不透明度 50% で一緒に薄くなる。ラベル（アイコンの下の名前）が 25% の線の上で読める。Retina で線の太さ・間隔が一様。ドラッグでウィンドウが動き、右クリックが背景のメニューのまま。
- [ ] **帯**: Esc で消えるとき帯が先に消え、グリッチの静止画に写らない。トリガーで出したときはグリッチの後に 250 ms でフェードイン。常駐 + 自動で隠すの収納中に止まり、戻ると再開。「アニメーション」OFF と「視差効果を減らす」で止まる（静止した走査線は残る）。「演出の調整…」で「なし」にすると止まり、settings.json に `"scanBeam": { "kind": "none", … }` が書かれる。「既定に戻す」で `beam` に戻り、キーが消える。配色を変えると帯の色が追従。玉（`orb`）と同時に出すと玉が帯の上。
- [ ] **CPU**（`top -pid <pid> -stats cpu`、盤面表示中・何もしない 30 秒、表示直後のグリッチの山を除く）: (a) **既定構成（走査線 ON + 帯あり）** = 目標なし、記録のみ、(b) **`scanBeam` を「なし」にした構成** = SPEC §11 の「表示中 3% 以下」の対象。導入前（0.5〜1.5%）と変わらないこと、(c) 帯 + 玉。隠した後はどれも 0.5% 以下。値を SPEC §11 と EFFECTS.md「CPU の計測と既定の判断」の表に追記する。(a) が 20% 台でも既定は変えない（ユーザー判断済み）が、数値は注記の「20〜30% 程度」と合うかを見る。
- [ ] 済んだら SPEC の「ユーザー確認待ち」を確認済みにし、このファイルを削除する。Windows 側は SPEC §13.3 C23 に残す。

---

## 実装時の注意

- **帯は `Frame` の中（`Body`）に置かない。** `PlayGlitchAsync` / `PlayShowAsync` / `PlayHideAsync` は `DuringTransition(_glitch.Start(…))` の形で、引数の `Start` が先に静止画を撮ってから `_transitions++` が走る。`Frame` の中に動く層があると静止画に写る。`Overlays` の `AmbientRoot` なら撮られない（玉・明滅と同じ理由。EFFECTS.md「グリッチとの関係」）。
- **八角形の切り抜きは帯側で持つ**（`ScanBeamLayer.Clip = FrameChrome.FaceClip`）。`Overlays` は `ClipToBounds = false`（玉の暈が枠の外へはみ出すため）なので、`Overlays` 自体を切り抜いてはいけない。
- **`ScanBeamLayer` 全体を `InvalidateVisual` しない。** 動かすのは子の `TranslateTransform.Y` だけ。ブラシは色が変わったときだけ `new`（`OrbSprite.SetColor` と同じ）。
- `ScanlineLayer.Render` でブラシ・ペンを毎回 `new` しない（`GridLayer` と同じ）。`Pitch` は `AffectsRender` の `StyledProperty` なので値を入れれば描き直る。線は論理 px（Mac Retina で確認済みの見え方）。Windows 125% / 150% の見え方は C23 で間隔 3 種とも見てから吸着（`RenderScaling`）を入れる。
- **帯の既定 ON は案 A の唯一の例外。** `frameOrb` / `glowPulse` の既定は `none` のまま変えない。`EffectCatalog.Normalize` は変えない（既定と同じ `beam / 4000` を捨て、`none` を残すのが望みどおりの挙動）。
- **既定で常時の演出が動く**ので、`AmbientRunning == false` を前提にした既存テストは書き換える（`issue/UI_TESTS.md`「既存テストの書き換え」）。ヘッドレスでは `RequestAnimationFrame` が呼ばれないが `Start()` で `IsRunning` は true になる。
- `appearance.animation` OFF で**静止した走査線は消えない**（見た目であって演出ではない）。帯だけ止まる。
- 設定項目は SETTINGS.md を先に直してから `AppSettings.cs` と設定画面を合わせる（CLAUDE.md）。SETTINGS.md は 3 行とも更新済み。
- 文言は両 resx に足し、`Strings` 経由で参照（直書きは `NoHardcodedJapaneseTests` が拾う）。`Common_PixelValue` の `{0}` は両言語で引数集合が一致していること（`StringsTests`）。試し表示の `i18n:ignore` はファイルごと消える。
- ヘッドレステストでは `RequestAnimationFrame` が呼ばれないので `Ambient.Tick(ms)` で進め、`ScanBeam.BandY` を見る。帯は既定 ON なので `Effects` を空のままでも動く。止めるテストでは `scanBeam = none` を明示する。
- **CPU を測るときは帯が実際に描かれていることを目で確認してから**（`issue/AMBIENT_EFFECTS.md` の玉の計測の誤りと同じ轍を踏まない）。
