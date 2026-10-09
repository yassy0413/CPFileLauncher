# グリッチの強さ（`appearance.glitchIntensity`）

2026-10-10 ユーザー要望「boardShow / boardHide の下に、グリッチの強度みたいな設定を入れられる？」→ 同日ユーザー判断: (1) 表示・非表示で共通の 1 項目、(2) スライダー、(3) 既定は今の見た目（100%）。

正となる文書: `spec/SETTINGS.md` 演出タブ「グリッチの強さ」、`spec/EFFECTS.md`「glitch の詳細」の「強さの倍率」、SPEC §3.6 / §3.8 / §7、`spec/TERMS.md`（`Settings_Effects_GlitchIntensity`）。テスト観点は `issue/UI_TESTS.md`「グリッチの強さ」。

## タスク

1. [x] **Core `src/FileLauncher.Core/Effects/GlitchLook.cs`（新規）**
   - `public readonly record struct GlitchLook(double ShadowOffset, double ShadowOpacity, double BandShiftMin, double BandShiftMax, double FinalShift)`。
   - 定数 `MinIntensity = 0` / `MaxIntensity = 200` / `IntensityStep = 10` / `DefaultIntensity = 100`。
   - `public static readonly GlitchLook Base = new(5, 0.55, 6, 16, 6);`（2026-10-03 確定の値。コメントで EFFECTS.md を指す）。
   - `public static GlitchLook Scaled(int intensityPercent)`: `k = Math.Clamp(intensityPercent, 0, 200) / 100.0`、`new(5k, Math.Min(1, 0.55k), 6k, 16k, 6k)`（`Base` の各値 × k で書く）。
   - `public bool IsStill => ShadowOffset == 0 && BandShiftMax == 0;`
   - `public static int NormalizeIntensity(int v) => (int)Math.Round(Math.Clamp(v, 0, 200) / 10.0, MidpointRounding.AwayFromZero) * 10;`
2. [x] **Core `AppSettings`**: `AppearanceSettings.GlitchIntensity { get; set; } = GlitchLook.DefaultIntensity;`（XML コメント: 「グリッチのブレの強さ %（0〜200、10 刻み。既定 100）。spec/EFFECTS.md「強さの倍率」」。置き場所は `Vsync` の近く）。`Normalize` に `Appearance.GlitchIntensity = GlitchLook.NormalizeIntensity(Appearance.GlitchIntensity);`。JSON キーは camelCase で `appearance.glitchIntensity`（既存のシリアライザ設定のまま）。`schemaVersion` は上げない・マイグレーションなし。
3. [x] **App `Effects/GlitchPlayer.cs`**: 定数 `ShadowOffset` / `ShadowOpacity` と `Shuffle` の 6 / 10 / 6 を削除し、`Start` に `GlitchLook look` 引数（`spec` の直後）を足して影の `dx` / `Opacity` と `Shuffle` のずれに使う。`Shuffle` のずれは `final ? look.FinalShift : look.BandShiftMin + r * (look.BandShiftMax - look.BandShiftMin)`。**乱数の呼び出し回数・順序は変えない**（`h` / `y` / `shift` / 符号の 4 回）。`look.IsStill` のときは影 2 枚と帯 3 本を作らず本体の `Image` だけを置く（`Shuffle` も呼ばない。乱数を消費しないのは 0% のときだけなので可）。クラスの XML コメントの「左右 5 px」を「`GlitchLook` のずれ」に。
4. [x] **App `BoardWindow`**: `ApplyEffects(AppearanceSettings)` で `_glitchLook = GlitchLook.Scaled(appearance.GlitchIntensity)` を保持し、`_glitch.Start` の 3 か所（`PlayShowCoreAsync` / `PlayHideCoreAsync` / `PlayGlitchAsync`）に渡す。テスト用に `internal GlitchLook GlitchLook => _glitchLook;` を出してよい。
5. [x] **App `Themes/ChromeWindow`**: `public static GlitchLook GlitchLook { get; set; } = GlitchLook.Base;` を `ShowEffect` / `HideEffect` の隣に足し、`_glitch.Start` の 2 か所に渡す。
6. [x] **App `App.axaml.cs`**: `ApplyWindowEffects` で `ChromeWindow.GlitchLook = GlitchLook.Scaled(appearance.GlitchIntensity);`。`SettingsChange.Effects` の経路（`_board.ApplyEffects` + `ApplyWindowEffects`）は既存のまま。
7. [x] **App `Settings/SettingsWindow.cs` `EffectsTab`**:
   - 「詳細設定」の `rows` を組むループで、`def.Id == EffectCatalog.BoardHide` の `EffectRow` の直後に `Row(Strings.Settings_Effects_GlitchIntensity, PercentSlider(GlitchLook.MinIntensity, GlitchLook.MaxIntensity, GlitchLook.IntensityStep, s => s.Appearance.GlitchIntensity, (s, v) => s.Appearance.GlitchIntensity = v, SettingsChange.Effects), Strings.Settings_Effects_GlitchIntensity_Note)` を足す（`Sub` の字下げは付けない。右の値は既存の「100 %」表示のまま）。
   - 有効・無効: `boardShow` / `boardHide` の現在の種類（`Effects.GetValueOrDefault(id) ?? def.Default` の `Kind`）のどちらかが `Glitch` なら有効。`_refreshers` に入れるのに加え、**種類の `ComboBox` の変更では `RefreshAll` が走らない**ので、`EffectRow` に省略可能なコールバック（例 `Action? onChanged`。`kind.SelectionChanged` で `Apply` の後に呼ぶ）を足して boardShow / boardHide の行から呼ぶ（`_refreshing` 中は呼ばなくてよい）。
   - 「すべて既定に戻す」の `_hub.Update` に `s.Appearance.GlitchIntensity = GlitchLook.DefaultIntensity;` を足す。
8. [x] **リソース**: `Strings.resx` / `Strings.ja.resx` に `Settings_Effects_GlitchIntensity`（"Glitch intensity" / 「グリッチの強さ」）と `Settings_Effects_GlitchIntensity_Note`（"How far the show/hide glitch shifts and splits colors. At 0%, it only fades." / 「表示・非表示のグリッチの横ずれと色にじみの大きさです。0% にするとずれずにフェードだけになります。」）。文言は `spec/TERMS.md` が正。
9. [x] **テスト**: `issue/UI_TESTS.md`「グリッチの強さ」の観点。
10. [ ] **ユーザー確認（Mac）**: 0 / 50 / 100 / 200% で盤面のポップアップ表示・非表示と設定画面の開閉を見る（200% でずれた帯が窓の端で切れないこと、0% がフェードだけに見えること）。Windows は SPEC §13.3 の一括確認に含める（`issue/MAC_SUPPORT.md` Mac-0 に 1 行足す）。

（2026-10-10 タスク 1〜9 実装済み。テスト Core 266 / Desktop 128。ChromeWindow の倍率は Core の `Scaled` のテスト + 目視で代えた。残りはタスク 10 のユーザー確認）

## 完了条件

タスク 1〜9 が済み `dotnet test` が通り、タスク 10 のユーザー確認が済んだら、SETTINGS.md / EFFECTS.md / SPEC の「未実装」を「実装済み」に直してこのファイルを削除する。
