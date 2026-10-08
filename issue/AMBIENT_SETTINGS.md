# 常時の演出: 「演出」タブの新設・設定「VSync」・注記の数値・光の玉と明滅の既定オン

設計日: 2026-10-07（designer）。前提: 同日の 30 fps 化 + 背景画像の作り置き（`issue/AMBIENT_CPU.md`）が実装・再計測済みで、ユーザーが 30 fps の見た目と背景画像を確認して OK。

ユーザー判断・要望（2026-10-07）:
1. **「VSync」を設定で選べるように。値は 1〜3、1 = 60 fps**（「1=60fps で、1-3 にしましょう」）。2 = 30 fps（今の既定の 30 ms 相当）、3 = 20 fps。既定 2。
2. 注記 `AmbientNote` の数値を **「15〜25% 程度」** に（日英）。
3. **光の玉 `frameOrb` と発光の明滅 `glowPulse` も既定オン**（2026-10-04 の案 A を撤回）。
4. **「光の玉・発光・走査線はこのアプリの肝なので既定を ON にして、演出設定を大項目化、その 3 つを普通に表示し、残りの項目を詳細設定として現状のように表示」** → 設定画面に「演出」タブを新設。

正となる文書: **SPEC §7「演出タブの構成」**（(7) 静止した走査線は 2026-10-07 夕方に追加）、**SPEC §3.9「走査線の仕様」の「設定 UI」**、**SETTINGS.md「演出タブ」**（アニメーション / 光の玉 / 発光の明滅 / 走査線の帯 / 詳細設定 / VSync / 注記 / すべて既定に戻す / **静止した走査線 3 行**）、**`spec/EFFECTS.md`「常時の演出の詳細」の「時計」/「設定画面での表示」/「既定値の経緯」/「CPU の計測と既定の判断」**、SPEC §3.1 / §3.9 H7 / §10.5 / §10.7 / §11 / §13.3 C14 (3) / C17 (0)(0') / C19 (5) / C23 (5)、`spec/TERMS.md`（タブ名・小見出し・詳細設定・VSync・注記・キーの改名規則）、`issue/UI_TESTS.md`「VSync と常時の演出 3 つの既定オン」。

---

## 決めたこと（要約）

- **「演出」タブ**（`Settings_Tab_Effects`、英 "Effects"）を表示とポップアップの間に新設（8 タブ目）。表示タブから「アニメーション」と折りたたみ「演出の調整…」を移す。**タブにした理由**: ユーザーの言葉「大項目化」。表示タブは既に長く（配色・背景画像・HUD で 20 行弱）スクロールしており、見出しで区切っても埋もれる。英語 UI で 8 タブを 1 行に収めるため **`SettingsWindow.BodyWidth` 560 → 620**（足りなければ 640。タブの文字 14 px は変えない。見積り: 8 見出しの文字幅 約 420 + 余白 18 × 8 + 間隔 4 × 8 ≈ 610）。
- **タブの中身**（上から）: (1) 「アニメーション」ToggleSwitch + 注記 → (2) 小見出し「常時の演出」→ **光の玉 / 発光の明滅 / 走査線の帯 の 3 行を折りたたまず普通の行として**（項目名は演出名だけ・ID なし、種類 `ComboBox` + 時間スライダー（秒）+ 「既定に戻す」。**イージング欄は出さない**）→ (3) 「VSync」`ComboBox` + 注記 → (4) 注記（CPU、視差効果）→ (5) 折りたたみ **「詳細設定」**（残り 11 演出を従来の行の形 = 演出名 + ID / 種類 / 時間 ms / イージング / 既定に戻す）→ (6) 「すべて既定に戻す」（折りたたみの外。`effects` を空に + `vsync` = 2。アニメーションは触らない）。**「アニメーション」OFF で (2)〜(6) を無効表示**（注記は読める）。
- **リソースキー**: 新タブの項目は `Settings_Effects_*`。旧 `Settings_Appearance_Animation` / `_Animation_Note` / `Settings_Appearance_Effects`（→ `Settings_Effects_Advanced`、文言「詳細設定」/ "Advanced effects" に変更）/ `Settings_Appearance_Effects_ResetAll` / `Settings_Appearance_Effects_Reset` / `Settings_Appearance_EffectRow`（→ `Settings_Effects_Row`）/ `Settings_Appearance_Effects_AmbientNote` / `_ReducedMotionNote` を **一括改名**（TERMS.md §4）。新規: `Settings_Tab_Effects`、`Settings_Effects_AmbientHeading`、`Settings_Effects_Vsync` / `_Item` / `_Note`。
- **設定キー `appearance.vsync`**（int、1〜3、既定 2。省略可能な追加、`schemaVersion` 3 のまま。範囲外は `Math.Clamp(1, 3)`）。意味 = 「60 Hz のフレーム何枚ぶんに 1 回更新するか」。
- **時間で間引く**: `MinFrameMs = {1: 13, 2: 30, 3: 47}`（`N × 16.67 ms` − 約 3 ms のマージン。Core `AmbientMath.FrameIntervalMs(int vsync)`、表で持つ）。理由: 「1 = 60 fps」を優先し、120 Hz でも 1 = 60 / 2 = 30 / 3 = 20 fps（間引き数だと 120 / 60 / 40）。144 Hz では 72 / 29 / 21 fps の近似で許容。マージン 3 ms は「33 ではなく 30」の一般化。
- **通知**: `SettingsChange.Effects`（既存）。`BoardWindow.ApplyEffects` が `AmbientAnimator.Vsync = appearance.Vsync` を入れ、次のフレームから効く。`Render` 不要。
- **既定オン**: `EffectCatalog` の `FrameOrb.Default = orb / 8000 / linear`、`GlowPulse.Default = pulse / 4000 / linear`（案 A 以前の設計値）。`Normalize` の「既定と同じは保存しない」はそのまま → `none` を選んだ人は `none` が保存され、キーなしの既存ユーザーは次回起動から玉・明滅が出る。マイグレーションは書かない。
- **注記**: 「光の玉・発光の明滅・走査線の帯は、盤面が見えている間ずっと動き、CPU を多く使います（15〜25% 程度）。種類を「なし」にすると止まり、「VSync」を大きくすると軽くなります。」/ "Light orb, glow pulse, and scan beam run the whole time the board is visible and use a lot of CPU (about 15–25%). Set the kind to None to stop them, or raise VSync to lighten the load."（数値はユーザー確定、後半は designer 案）。
- **SPEC §11**: 「表示中 3% 以下」は「常時の演出をすべて「なし」にした構成」に適用（変更なし）。既定構成（玉 1 個 + 明滅弱 + 帯、VSync 2、背景画像なし）は目標なしで実測を記録（見込み 20〜25%）。
- **変えないもの**: 動く条件・250 ms のフェードイン・停止中はフレーム要求なし・`scanBeam` の既定・演出 ID・JSON の形（`vsync` 以外）・Platform（変更なし）。
- **追加（2026-10-07 夕方、ユーザー指摘「走査線の設定が不自然に表示に残っている」）**: 静止した走査線の 3 行（`appearance.hud.scanlines` / `scanlineOpacity` / `scanlinePitch`）を表示タブから**演出タブの末尾**へ移す（ステップ 3b）。**JSON キーは変えない**（`appearance.hud.*` のまま。層・通知 `Hud`・`Normalize` も変わらない = 設定画面だけの変更）。置き場所は **「すべて既定に戻す」の下**に小見出し「静止した走査線」（`Settings_Effects_ScanlinesHeading`、「常時の演出」と同じ作り）+ 3 行。理由: (a) 「アニメーション」OFF で無効にする並び（常時の演出 3 行 → VSync → 詳細設定 → すべて既定に戻す）を途切れさせない（静止した走査線は演出ではないので OFF でも有効のまま。間に挟むと有効な島ができる）、(b) 「すべて既定に戻す」の対象（`effects` + `vsync`）がボタンの上の並びであることを形で示せる（静止した走査線は対象外。文言は変えない）、(c) 帯の行から見出し 1 つ + VSync + 注記 + 折りたたみ 1 行 + ボタンの距離で、同じタブ内なので「どこにあるか」は見れば分かる。退けた案: 帯の行と静止 3 行を 1 つの「走査線」グループにまとめる（帯は常時の演出で VSync・CPU 注記・「アニメーション」OFF の無効化・「すべて既定に戻す」の対象なので、同じグループ内で有効 / 無効が混ざる）、「アニメーション」と「常時の演出」の間に置く（注記「下の演出がすべて止まります」の直下に止まらないものが来る。肝の 3 行が下がる）。リソースキーは `Settings_Effects_Scanlines` / `_Note` / `Settings_Effects_ScanlineOpacity` / `Settings_Effects_ScanlinePitch`（TERMS.md §4）。注記に「動かないので CPU を使わず、「アニメーション」がオフでも消えません。」を足す（無効にならない理由を示す。designer 案）。

---

## ステップ 1: Core

- [x] `Core/Model/AppSettings.cs`: `AppearanceSettings.Vsync { get; set; } = 2`（JSON `appearance.vsync`。XML コメント「常時の演出の更新間隔。1 = 60 fps / 2 = 30 fps / 3 = 20 fps」）、`public static readonly int[] VsyncValues = [1, 2, 3]`（`ButtonSizes` と同じ流儀）。`Normalize` で `Vsync = Math.Clamp(Vsync, 1, 3)`。
- [x] `Core/Effects/AmbientMath.cs`: `public static double FrameIntervalMs(int vsync)` = `vsync <= 1 ? 13 : vsync == 2 ? 30 : 47`（コメント「60 Hz の N 枚ぶん − 約 3 ms のマージン。EFFECTS.md「時計」」）。
- [x] `Core/Effects/EffectCatalog.cs`: `FrameOrb` の `Default` を `Spec(EffectKind.Orb, 8000, EasingKind.Linear)`、`GlowPulse` を `Spec(EffectKind.Pulse, 4000, EasingKind.Linear)` に。`AllowedKinds` / 範囲 / `IsAmbient` は変えない。
- [x] テスト（`issue/UI_TESTS.md`）: `AmbientEffectsCoreTests` の `光の玉と明滅は既定で動かず_走査線の帯だけ既定で動き_…` → 「常時の演出 3 つは既定で動き_時間は演出ごとの範囲に丸める」（`orb / 8000` と `pulse / 4000` が `Normalize` で捨てられ、`none` / `orbTwin` は残る）。`FrameIntervalMs` の 3 値 + 範囲外。`Normalize` の `vsync` 丸めと JSON 往復（キーなし → 2）。

## ステップ 2: App（アニメーター・盤面・文言）

- [x] `Effects/AmbientAnimator.cs`: `internal const double MinFrameMs = 30` を削除し、`public int Vsync { get; set; } = 2` と `internal double MinFrameMs => AmbientMath.FrameIntervalMs(Vsync)` に。`ShouldTick(double lastFrameMs, double frameMs, double minFrameMs)`（static のまま第 3 引数を足す）。`OnFrame` は `ShouldTick(_lastFrameMs, frameMs, MinFrameMs)`。summary の「約 30 fps」を「設定「VSync」の間隔（既定 約 30 fps）」に。
- [x] `BoardWindow.ApplyEffects(appearance)`: `_ambient.Vsync = appearance.Vsync;`（`PulseSpec` の行の近く、`UpdateAmbient` の前）。`Stop` / `Start` は不要（次のフレームから新しい間隔で間引かれる）。
- [x] 文言（`Strings.resx` / `Strings.ja.resx`。TERMS.md）: 一括改名 `Settings_Appearance_Animation*` / `Settings_Appearance_Effect*` → `Settings_Effects_*`（`Settings_Appearance_Effects` → `Settings_Effects_Advanced` = 「詳細設定」/ "Advanced effects"、`Settings_Appearance_EffectRow` → `Settings_Effects_Row`）。新規: `Settings_Tab_Effects` = 「演出」/ "Effects"、`Settings_Effects_AmbientHeading` = 「常時の演出」/ "Ambient effects"、`Settings_Effects_Vsync` = "VSync"（両言語）、`Settings_Effects_Vsync_Item` = 「{0}（{1} fps）」/ "{0} ({1} fps)"（comment: `{0}` = 1〜3、`{1}` = 60 / 30 / 20）、`Settings_Effects_Vsync_Note`（TERMS.md の文）。`Settings_Effects_AmbientNote` を新しい文に（15–25、後半の差し替え。en ダッシュ）。
- [x] テスト `AmbientEffectsTests`: `ShouldTick` の 3 引数版（UI_TESTS の境界値）、`Ambient.Vsync` の既定 2 と `ApplyEffects` での反映、`MinFrameMs == 30` の定数テストを `FrameIntervalMs(2) == 30` に。**`既定では走査線の帯だけが動き_帯をなしにすると動かない` → 「既定では 3 つとも動き_すべてなしにすると動かない」**、`既定では動かず_…` → 「3 つなしなら動かず_…」（`Board(...)` ヘルパーに `pulseOn` を足す）。

## ステップ 3: 設定画面（「演出」タブ）

- [x] `SettingsWindow`: `BodyWidth = 620`。タブ列に `Tab(Strings.Settings_Tab_Effects, EffectsTab())` を表示の直後に挿入（8 タブ）。
- [x] `AppearanceTab()` から「アニメーション」の `Row` と `EffectsExpander()` を外す（「画像はサムネイルで表示」は残す）。
- [x] `EffectsTab()`（新規。他のタブと同じ `StackPanel` + `Scroll`）:
  1. `Row(Strings.Settings_Effects_Animation, Toggle(... SettingsChange.Effects), Strings.Settings_Effects_Animation_Note)`。
  2. 小見出し `Strings.Settings_Effects_AmbientHeading`（`Themed` に小見出しの作り方があればそれ、無ければ `TextBlock` SemiBold 13 px + `FlTextGlow` を `Themed.Heading(string)` として足し、他のタブでは使わない）。
  3. `foreach (var def in EffectCatalog.All.Where(d => d.IsAmbient))` → `AmbientEffectRow(def)`: 項目名 `EnumNames.Effect(def.Id)`（ID なし）、中身は `EffectRow(def)` と同じ `kind` / `slider`（秒表示）/ 「既定に戻す」で、**`easing` の `ComboBox` を作らない**。`EffectRow(def, showId: bool, showEasing: bool)` の引数で共通化してよい。
  4. `Row(Strings.Settings_Effects_Vsync, vsyncCombo, Strings.Settings_Effects_Vsync_Note)`: `Combo(AppearanceSettings.VsyncValues.Select(v => (Strings.FormatSettings_Effects_Vsync_Item(v, 60 / v), v)).ToArray(), s => s.Appearance.Vsync, (s, v) => s.Appearance.Vsync = v, SettingsChange.Effects)`（走査線の間隔の `Combo` と同じ作り。60 / 1 = 60、60 / 2 = 30、60 / 3 = 20）。
  5. 注記 `Settings_Effects_AmbientNote`（常に）、`PrefersReducedMotion` なら `Settings_Effects_ReducedMotionNote`（今の `EffectsExpander` の 2 行をそのまま）。
  6. `Expander { Header = Strings.Settings_Effects_Advanced }` に `EffectCatalog.All.Where(d => !d.IsAmbient)` の `EffectRow(def)`（従来の形。項目名は `Settings_Effects_Row`）。
  7. `Button(Strings.Settings_Effects_ResetAll)`: `_hub.Update(s => { s.Appearance.Effects.Clear(); s.Appearance.Vsync = 2; }, SettingsChange.Effects); RefreshAll();`。
  8. 2〜7 を 1 つの `StackPanel body` に入れ、`_refreshers.Add(() => body.IsEnabled = _hub.Current.Appearance.Animation)`（注記 5 は `body` の外に置くか、`IsEnabled` の影響を受けない `TextBlock` で）。
- [x] `RefreshAll` の既存の呼び出し（リセット・インポート・配色変更）で新タブの行も読み直されることを確認（`_refreshers` に載せていればそのまま）。
- [x] テスト `SettingsUiTests` / `AmbientEffectsTests`（`issue/UI_TESTS.md`「VSync と常時の演出 3 つの既定オン」）: 8 タブ・3 行が折りたたみ外で見える・イージング欄なし・`Expander` の中が 11 行・表示タブに無い・VSync・すべて既定に戻す・アニメーション OFF の無効化・英語で 8 タブが 620 に収まる・`設定画面に光の玉と発光の明滅の行と注記がある` の書き換え。既存のはみ出しテスト（全タブ・全 `Expander` を開いて右端を見る）は新タブも自動で対象になる。

## ステップ 3b: 静止した走査線の 3 行を演出タブの末尾へ（2026-10-07 夕方の設計。SPEC §3.9「走査線の仕様」の「設定 UI」/ §7 (7)、SETTINGS.md 演出タブ、TERMS.md）

Core・Platform・JSON は触らない。`SettingsWindow.cs` と両 resx とテストだけ。

- [x] 文言（`Strings.resx` / `Strings.ja.resx`）: **一括改名** `Settings_Appearance_Hud_Scanlines` → `Settings_Effects_Scanlines`、`Settings_Appearance_Hud_Scanlines_Note` → `Settings_Effects_Scanlines_Note`、`Settings_Appearance_Hud_ScanlineOpacity` → `Settings_Effects_ScanlineOpacity`、`Settings_Appearance_Hud_ScanlinePitch` → `Settings_Effects_ScanlinePitch`（行名の値は同じ）。`Settings_Effects_Scanlines_Note` の値を 日「スロットの下に主色の横線を敷きます。動かないので CPU を使わず、「アニメーション」がオフでも消えません。」/ 英 "Thin horizontal lines in the primary color behind the slots. They do not move, so they use no CPU and stay on even when Animation is off." に。**新規** `Settings_Effects_ScanlinesHeading` = 日「静止した走査線」/ 英 "Static scanlines"。
- [x] `SettingsWindow.AppearanceTab()`: 「静止した走査線（SPEC §3.9 H14）」のブロック（`scanlines` / `scanlineOpacity` / `scanlinePitch` の 3 行と `UpdateScanlinesEnabled`）を**丸ごと削除**（`UpdateClockEnabled` 以降はそのまま）。HUD の並びは「背景グリッド」「グリッドの濃さ」で終わる。
- [x] `SettingsWindow.EffectsTab()`: `p.Children.Add(resetAll)` の**後**に、(1) 小見出し `new TextBlock { Text = Strings.Settings_Effects_ScanlinesHeading, FontWeight = SemiBold, FontSize = 13, Margin = new Thickness(0, 6, 0, 0) }`（「常時の演出」の見出しと同じ指定。2 か所になるので `Heading(string)` のローカル関数か `private static` にまとめてよい）、(2) 削除したブロックをそのまま（`Row(Strings.Settings_Effects_Scanlines, scanlines, Strings.Settings_Effects_Scanlines_Note)`、`Row(Sub(Strings.Settings_Effects_ScanlineOpacity), scanlineOpacity)`、`Row(Sub(Strings.Settings_Effects_ScanlinePitch), scanlinePitch)`、`UpdateScanlinesEnabled` + `_refreshers.Add`）を `p` に直接足す。**`body` や `advanced` / `resetAll` の `IsEnabled` の連動には入れない**（「アニメーション」OFF でも有効のまま）。通知は従来どおり `SettingsChange.Hud`。`resetAll.Click` は変えない（`Hud.*` を触らない）。
- [x] `EffectsTab()` の summary コメントに「末尾に演出ではない静止した走査線の 3 行（アニメーション OFF でも有効、すべて既定に戻すの対象外）」を 1 行足す。
- [x] テスト（`issue/UI_TESTS.md`「静止した走査線の 3 行を演出タブへ」）: `SettingsUiTests.走査線の行は外観タブにあり_…` を演出タブ・新キーに書き換え、「静止した走査線は演出タブの末尾にあり_表示タブには無く_アニメーションOFFでも有効で_すべて既定に戻すで変わらない」を 1 件追加。`HudTests`（盤面の層）と Core のテストは変更なし。`StringsTests` は改名漏れ（旧キーの残り・両 resx の不一致）を自動で検出する。
- [ ] 確認（Mac、`dotnet run`）: 演出タブを下までスクロールして「すべて既定に戻す」の下に「静止した走査線」の見出しと 3 行があり、表示タブの「グリッドの濃さ」の下に無い。「アニメーション」OFF で 3 行だけ操作でき、盤面の線は残る。「すべて既定に戻す」で走査線の濃さ・間隔が戻らない。`--lang en` で "Static scanlines" / "Scanlines" / 注記の 2 文が左列 200 px・右列に収まる（注記は折り返し）。

## ステップ 4: 計測と記録（Mac、拡大率 1 と Retina の両方、Release `.app`、`top`、表示 3 秒後から 8 秒。メインの自動計測）

**2026-10-07 計測（実装後。Mac・拡大率 1 の画面（scale=1）・ソフトウェア描画・Release `.app`・Claude が自動で。背景画像 bg01.jpg・埋める・覆い 10%）**:

| 構成 | VSync 1（60 fps） | **VSync 2（30 fps、既定）** | VSync 3（20 fps） |
|---|---|---|---|
| 既定（玉 1 個・明滅「弱」・帯）・背景画像あり | 35.7% | **25.5〜25.9%** | 20.7% |
| 既定・背景画像なし | — | 18.2%（揺れ 30%） | — |
| 玉 2 個・明滅「強」・帯・背景画像あり | — | 28.3% | — |
| 隠した後（どれも） | 0.5% | 0.5% | 0.5% |
| 表示まで n ms（4 回） | 16〜40 ms | 18〜107 ms（初回だけ 107） | 16〜66 ms |
| footprint 表示中 | 122 MB | 120〜121 MB | 121 MB |

Retina（scale 2）は未計測。

**2026-10-08**: 負荷削減 A + B（`issue/AMBIENT_RENDER_OPT.md`）の設計が入ったので、以下の残りの計測は **その実装後に同 issue のステップ 4 とまとめて取る**（同じ手順・同じ構成に「既定・背景画像なし」を含める）。試作値は既定構成（背景あり）29.6 → 22.8%。

- [ ] **新しい既定構成**（`orb / 8000` + `pulse / 4000` + `beam / 4000`、VSync 2、背景画像なし / あり）の表示中 CPU と隠した後、表示まで n ms、footprint。EFFECTS.md 冒頭の実装後の表に「既定構成（2026-10-07 午後）」の行を足し、SPEC §11 (2) の見込み「20〜25%」を実測に置き換える。
- [ ] **VSync 1 / 2 / 3** を既定構成で比較。EFFECTS.md の表に列を足す。見た目: VSync 3 で玉（8 s / 周 ≈ 9.5 px / 更新）・帯（≈ 5 px / 更新）のコマ送りが「選べる範囲」として許容か（ユーザー確認、Mac 環境があれば）。
- [ ] 注記の数値「15〜25%」が新しい既定構成（3 つオン）の実測と大きくずれていないか確認（ずれていれば要ユーザー判断）。
- [ ] Windows 側: 次の Windows 確認（`issue/MAC_SUPPORT.md` Mac-0 / SPEC §13.3 C14 (3) / C17 (0)(0')(2) / C19 (5) / C23 (5)）で、新規の settings.json で 3 つ動くこと・「演出」タブと 8 タブの収まり・VSync 1 / 2 / 3 の CPU（ソフトウェア描画なので 1 桁 % 台の見込み）を記録する。

## 完了条件

- ステップ 1〜3・**3b** が済み `dotnet test FileLauncher.sln` が通る。ステップ 4 の値が EFFECTS.md / SPEC §11 に記録され、「演出」タブの見た目（8 タブの収まり・3 行・詳細設定・VSync・注記の文言・**末尾の静止した走査線 3 行**）と新規の settings.json で 3 つ動くことを Mac でユーザーが確認して OK。済んだらこのファイルを削除する（`issue/AMBIENT_CPU.md` も同時に削除。Windows 側は SPEC §13.3 に残る）。

## 要ユーザー判断（残り）

- 注記の後半の文言（designer 案「種類を「なし」にすると止まり、「VSync」を大きくすると軽くなります。」）でよいか。代案: 後半を削って数値だけにする。
- `ComboBox` の項目表記「1（60 fps）」でよいか（代案: 「1 — 60 fps」、「60 fps（1）」）。
- VSync 3（20 fps）の見た目を実物で見て、選択肢として残すか（残す前提。設計上は 1〜3 固定）。
- 折りたたみの見出し「詳細設定」/ "Advanced effects" でよいか（タブ名「詳細」= "Advanced" と紛れないよう英語に "effects" を付けた）。
- 常時の演出 3 行でイージング欄を出さない（詳細設定の行では無効表示のまま）でよいか。
- 本体の幅 560 → 620（8 タブを 1 行に収めるため）でよいか。嫌なら代案はタブ名を短くする（例 "Pinned" → "Pin"）か 2 行表示。
- **（ステップ 3b）静止した走査線の置き場所**: 「すべて既定に戻す」の**下**（推奨。理由は「決めたこと」の追加の項）でよいか。代案 A: 「アニメーション」と「常時の演出」の間（帯に近いが、OFF で無効にならない行が注記「下の演出がすべて止まります」の直下に来る）、代案 B: VSync・注記の直後・「詳細設定」の前（帯に最も近いが、OFF のとき無効の並びの中に有効な島ができ、「すべて既定に戻す」に含まれるように見える）。
- **（ステップ 3b）「すべて既定に戻す」に静止した走査線を含めない**（推奨: 含めない。ボタンは `effects` + `vsync` のまま。含めると「濃さを調整した人が演出の時間を戻したいだけで走査線まで戻る」）でよいか。含めるなら `Hud.Scanlines = true / ScanlineOpacity = 25 / ScanlinePitch = 3` も戻し、通知を `Effects | Hud` の 2 回（`SettingsChange` がフラグでなければ 2 回 `Update`）にし、3 行をボタンの上へ移す。
- **（ステップ 3b）小見出し「静止した走査線」/ "Static scanlines"** でよいか（行名「走査線」と重ならない語にした。代案: 「走査線（静止）」/ "Scanlines (static)"、見出しなし（「走査線」の行をそのまま先頭に。ボタンの直後に行が続くので区切りが弱い））。
- **（ステップ 3b）注記の後半「動かないので CPU を使わず、「アニメーション」がオフでも消えません。」** を足してよいか（演出タブで OFF でも無効にならない理由を示すため。代案: 従来の 1 文のまま）。

## 実装時の注意

- **間引きは時間で**（`FrameIntervalMs` の表）。`RequestAnimationFrame` の呼ばれた回数で数えない（120 Hz で fps が倍になる）。
- **`MinFrameMs` を `N × 16.67` ちょうどにしない**（マージン約 3 ms。EFFECTS.md「時計」）。表の値 13 / 30 / 47 を変えるときは 60 / 120 / 144 Hz の 3 つで N 枚目が通り N−1 枚目が通らないことを確かめる。
- `Vsync` の変更で `AmbientAnimator` を `Stop` / `Start` しない（世代番号の仕組みを増やさない）。
- `Normalize` の「既定と同じは保存しない」は `effects` の辞書だけ。`vsync` は `opacity` と同じく常に書き出す。
- 既定オンにしても `PulseLayer.Refresh` は `glowPulse` が有効なときだけ（`UpdateAmbient` の `DispatcherPriority.Loaded`）。表示遅延のログ「表示まで n ms」が変わらないことを見る。
- 文言は両 resx に足し、`Strings` 生成クラスで参照。"VSync" / "fps" は翻訳しない。キーの改名は IDE の一括置換で（テストも `Strings.*` 参照なので追従する。`FormatSettings_Appearance_EffectRow` → `FormatSettings_Effects_Row`）。
- 「アニメーション」OFF の無効化は `body.IsEnabled` 1 か所で（行ごとに `_refreshers` を増やさない）。
- 「すべて既定に戻す」は演出の並びの値だけ（`effects` + `vsync`）。**ボタンの下の静止した走査線（`appearance.hud.*`）は触らない**。データタブの「設定をリセット」（全項目）とは別。
- **（ステップ 3b）** 静止した走査線の 3 行は `SettingsChange.Hud` のまま（演出ではないので `Effects` にしない。`Effects` にすると `ApplyEffects` が走って `UpdateAmbient` まで呼ばれる）。「アニメーション」OFF の `IsEnabled` 連動（`body` / `advanced` / `resetAll`）に入れない。JSON キーの改名・マイグレーションはしない（`HudSettings` のまま。`schemaVersion` 3 のまま）。
- `issue/AMBIENT_CPU.md` の「設定項目にしない」はこの判断で上書き済み（AMBIENT_CPU 側に注記あり）。
