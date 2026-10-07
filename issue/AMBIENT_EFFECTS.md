# 常時の演出: 光の玉（`frameOrb`）と発光の明滅（`glowPulse`）

ユーザー要望（2026-10-04、Mac で進める）: 「外側フレームや、内側でも可能なところに、光の玉が移動しているアニメーションを入れたい。又、全般的なグローに柔らかい明滅を入れたい」。

正となる文書: **`spec/EFFECTS.md`「常時の演出の詳細」**（パラメータ・規則・方式・「CPU の計測と既定の判断」）、SPEC §3.1 / §3.6（見た目）/ §10.3（`PrefersReducedMotion`）/ §11（CPU の目標と計測値）/ §12（軽くする方法）/ §13.3 C17、`spec/SETTINGS.md`「演出の調整…」、`spec/TERMS.md`（訳語）、`issue/UI_TESTS.md`「常時の演出」（テスト観点）。

設計日: 2026-10-04（designer）。**ステップ 1〜3・5 実装済み、CPU 計測済み、ユーザー判断（案 A: 2 つとも既定なし）反映済み（同日）。仕様・テスト・文言は同期済み。** 残りはユーザーの見た目の確認（ステップ 4。値の確定は任意）だけで、済んだらこのファイルを削除し、SPEC §13.3 C17（Windows 側）だけを残す。
**2026-10-06: 枠の発光を八角形に沿わせる修正（実装・Mac 確認済み、`issue/CHAMFER_GLOW.md` は完了・削除。明滅が焼く形・光の玉の経路・余白 34 → 40 が変わる）が入るので、ステップ 4 の見た目の確認はその修正後に行う。** ステップ 4 の文面は同日、テーマ廃止（2026-10-04、サイバーパンク専用・配色 = 主色 / 副色）後の形に直した。

---

## 決めたこと（要約）

- 演出 ID を 2 つ足す: `frameOrb`（枠線の上を周回する光の玉）と `glowPulse`（枠の発光 `FlBoardGlow` の柔らかい明滅）。どちらも `appearance.effects` の中なので **settings.json に新しいキーは増えない**（`schemaVersion` 2 のまま）。
- 種類: `frameOrb` = none / `orb`（1 個、主色）/ `orbTwin`（2 個、主色 + 副色が対角）。`glowPulse` = none / `pulse`（弱、発光 +45%）/ `pulseStrong`（強、+90%）。「時間」= 1 周 / 1 周期の長さ（秒表示。スライダーの初期位置は 8000 / 4000）。イージングは使わない。
- 既定（**2026-10-04 ユーザー判断 案 A、確定**）: **2 つともどのテーマでも `none`**。ユーザーが「演出の調整…」でオンにする扱い。2 行の下の共通の注記 `Settings_Appearance_Effects_AmbientNote` を「光の玉と発光の明滅は、盤面が見えている間ずっと動き、CPU を多く使います（20〜30% 程度）。既定ではオフです。」に変更し、明滅専用の注記（`PulseCpuNote`）は削除。`glowPulse` の行は引き続きサイバーパンクのときだけ出す。オンにしたときは SPEC §11 の CPU 目標の対象外。
- 動く条件: 種類が none 以外 かつ アニメーション ON かつ 盤面が見えている かつ 常駐の収納中でない かつ 表示 / 非表示演出中でない かつ OS の「視差効果を減らす」がオフ（ステップ 5、採用済み）。表示演出の後に 250 ms でフェードイン、非表示演出の開始で即座に消える。止まっている間はフレーム要求を出さない（CPU 0）。
- 方式（実装後の形）: `OrbLayer : Canvas` + 玉 1 個 = 部品 `OrbSprite`（`RenderTransform` で動かす）、`PulseLayer : Control`（枠の発光を `RenderTargetBitmap` に焼き、`Level` を描画時の `PushOpacity` で掛ける。2% 以上変わったときだけ更新。**2026-10-06 設計: 焼く元は `FlBoardGlow` の矩形の `Border` から、盤面と同じ八角形のリング描画 `NeonGlowPlan` に変える**、`issue/CHAMFER_GLOW.md`）、`AmbientAnimator`（`TopLevel.RequestAnimationFrame`、16 ms 以上）。数式は Core（`OrbPath`（2026-10-06 から八角形の経路も）/ `AmbientMath`、形は `Octagon`、発光の断面は `NeonProfile`）。
- 対象にしないもの（v1）: 文字の発光、タブ・セル・ホバーの発光、トースト、設定画面・ダイアログの枠、内側（区切り線など）の玉。理由は EFFECTS.md。
- CPU（SPEC §11）: 既定構成（常時の演出なし）で表示中 3% 以下・隠した後 0.5% 以下を満たす。オン時は目標外。軽くする方法（別の透明ウィンドウに玉を描く、コマ数を落とす、文字の発光の描き方の見直し）は SPEC §12。

---

## ステップ 1: Core（演出の定義と数式）— 完了

- [x] `Core/Effects/EffectSpec.cs`: `EffectKind` に `Orb` / `OrbTwin` / `Pulse` / `PulseStrong` を追加（JSON は camelCase）。
- [x] `Core/Effects/EffectCatalog.cs`: `EffectDefinition` に `IsAmbient` / `MinDurationMs` / `MaxDurationMs` / `DurationStepMs` / `UsesEasing`。`FrameOrb`（2000〜20000、500 刻み）、`GlowPulse`（1000〜10000、250 刻み）。**どちらも `CyberpunkDefault` なし = `None`**。`Normalize` は `def` の範囲で丸め、`!UsesEasing` なら `Linear` 固定。
- [x] `Core/Effects/OrbPath.cs`（角丸矩形の周上の経路、時計回り、左上始点、8 区間）、`Core/Effects/AmbientMath.cs`（`Phase` / `PulseLevel`）。
- [x] テスト `FileLauncher.Tests/AmbientEffectsCoreTests.cs`（4 件。「常時の演出はどのテーマでも既定で動かず_時間は演出ごとの範囲に丸める」を含む）。

## ステップ 2: App（層と駆動、盤面への組み込み）— 完了

- [x] `Effects/OrbLayer.cs`: `OrbLayer : Canvas`（`IsHitTestVisible = false`、`ClipToBounds = false`）に `OrbSprite` 2 個（2 個目は `orbTwin` のときだけ可視）。`Advance(ms)` が `Phase` → 経路上の位置 → 部品の `RenderTransform`（`TranslateTransform`）と尾の相対座標 `SetTail` を更新。経路は `Bounds` / `FlBoardBorderThickness` / `FlBoardCornerRadius` から作り、大きさ・角丸が同じなら使い回す。色は毎回 `FlAccent` / `FlAccent2` を引き、変わったときだけ `SetColor`（ブラシ作り直し）。テスト用 `OrbPositions`。
- [x] `Effects/PulseLayer.cs`: `Control` 派生。`Refresh()` が `FlBoardGlow` / `FlBoardCornerRadius` / `FlGlowMargin` を引き、`Background = Transparent` の `Border` を `RenderTargetBitmap`（`RenderScaling` 倍）に焼く（大きさ・スケール・影の値が前回と同じなら何もしない）。`Render` は `PushOpacity(Level)` で画像を `-FlGlowMargin` だけ外へはみ出させて描く。`OnDetachedFromVisualTree` で `Dispose`。
- [x] `Effects/AmbientAnimator.cs`: `Start` / `Stop` / `IsRunning` / `PulseSpec`、`OnFrame` は 16 ms 未満なら更新せず再登録のみ、`Tick(ms)`（テスト用）。`Level` は `PulseStep = 0.02` 以上変わったとき（と 0 ⇄ 非 0）だけ更新。`Stop` は `Stopwatch` を止めるだけ（再開は続きから）。
- [x] `BoardWindow`: `Overlays` の順 `AmbientRoot`（`PulseLayer` → `OrbLayer`）→ `GlitchLayer` → `DragLayer`。`UpdateAmbient()` が動く条件を判定し、`AmbientRoot` の `Opacity` 0 → 1（250 ms）で出して `Start`、満たさなければ即座に非表示 + `Stop`。表示開始時に `PulseSpec` が有効なら `DispatcherPriority.Loaded` で `PulseLayer.Refresh`。`AmbientSuspended`、`ReducedMotion`（`Func<bool>`、App が Platform から渡す）、演出中カウンタ `_transitions`（`DuringTransition`）。テスト用 `AmbientRunning` / `Ambient` / `Orbs` / `PulseLayer`。
- [x] `ResidentController`: 収納中 `AmbientSuspended = true`、復帰・`Deactivate` で false。
- [x] 文言（`Strings.resx` / `Strings.ja.resx`）: `Effect_frameOrb` / `Effect_glowPulse` / `Enum_EffectKind_*` / `Settings_Appearance_Effects_AmbientNote`（案 A の文言）/ `..._ReducedMotionNote`。
- [x] テスト `FileLauncher.Desktop.Tests/AmbientEffectsTests.cs`（5 件）。

## ステップ 3: 設定画面 — 完了

- [x] `SettingsWindow.EffectRow`: スライダーの範囲・刻みを `EffectDefinition` から、`IsAmbient` なら秒表示、イージングの有効 / 無効は `UsesEasing`。
- [x] `EffectsExpander`: `glowPulse` の行を `Theme == Cyberpunk` のときだけ可視（テーマ変更の `RefreshAll` で切替）→ **2026-10-04 のテーマ廃止で常に可視**。2 行の後ろに `AmbientNote`（常に）、`PrefersReducedMotion` が true なら `ReducedMotionNote`。
- [x] テスト: `AmbientEffectsTests`「設定画面の発光の明滅の行はサイバーパンクのときだけ出る」→ テーマ廃止で「行が常に見える + `AmbientNote` がある」の検証に書き換え済み（`issue/UI_TESTS.md`）。

## 実装メモと CPU の計測（2026-10-04、Mac `.app` Release、`top` の % CPU、盤面表示中）

- ステップ 1〜3・5 を実装（UI テスト 5 件、Core テスト 4 件）。ステップ 5（OS の「視差効果を減らす」）は推奨どおり採用。
- 設計からの変更: `OrbLayer` は `Canvas` で、玉 1 個 = 小さな部品 `OrbSprite`（芯 + 暈 + 尾）を `RenderTransform` で動かす（層全体を `InvalidateVisual` すると 29%。部品方式でも下は変わらなかった）。`PulseLayer` は枠の発光を 1 枚の `RenderTargetBitmap` に焼いて `Level`（描画時の `PushOpacity`）で明るさを変える（大きさ・テーマで焼き直し）。明るさは 2% 以上変わったときだけ更新。
- **計測（確定値）**: 演出なし 0.5〜1.5% / **光の玉 22〜28%** / **明滅 11〜18%** / **両方 25〜42%** / 隠した後はどれも 0.3〜1.0%。
  - 最初に記録した「光の玉だけ 1.0〜1.9%」は誤り（玉が描かれていなかった可能性が高い）。同日に再計測して訂正。
  - 原因: `sample` では描画スレッド（RenderTimerLoop）が常に忙しく、Skia → AppleMetalOpenGLRenderer の描画が並ぶ。macOS の Avalonia（OpenGL）は**一部が動くだけでも毎フレーム窓全体を描き直している**とみられ、発光（ぼかし）の多いサイバーパンクの盤面では 1 フレームが重い。明滅ではさらに文字の発光（`FlTextGlow`、文字ごとのぼかし）を毎回計算し直す（文字の発光を消すと明滅だけで約 2%）。
  - 試して効かなかったもの: `CompositionOptions.UseRegionDirtyRectClipping`（光の玉が 28% に悪化）、`AvaloniaNativeRenderingMode.Metal`（20〜30%、両方 36〜42% で改善なし）。どちらも元に戻した。
- **ユーザー判断（2026-10-04、案 A、確定）**: `frameOrb` / `glowPulse` ともどのテーマでも既定「なし」。ユーザーが「演出の調整…」でオンにする。共通の注記を「CPU を多く使います（20〜30% 程度）。既定ではオフです。」に変更、明滅専用の注記は削除。オン時は SPEC §11 の目標外。軽くする方法は SPEC §12（別の透明ウィンドウに玉を描く、コマ数を落とす等）。→ コード・文言・テスト・仕様（SPEC §3.1 / §3.6 / §11 / §12 / §13.3 C17、EFFECTS.md、SETTINGS.md、TERMS.md、UI_TESTS.md）に反映済み。

## ステップ 4: Mac でのユーザー確認（残り。見た目の確認で閉じられる）

Release `.app`（`publish-mac.sh`）で確認する。CPU の計測は済んでいるので、ここでは見た目だけ。**`issue/CHAMFER_GLOW.md`（枠の発光を八角形に沿わせる。2026-10-06 設計）の実装後に行う**（明滅の焼く形・玉の経路・余白が変わるため。CHAMFER_GLOW のステップ 5 と同じ回で見てよい）。見た目はサイバーパンク専用（2026-10-04）で、色は配色（主色・副色）で変える。

- [ ] 既定: 新規の settings.json（または「すべて既定に戻す」）で盤面を出しても玉も明滅も動かない。「演出の調整…」の末尾 2 行（`frameOrb` / `glowPulse` は常に出る）の下に注記「光の玉と発光の明滅は…CPU を多く使います（20〜30% 程度）。既定ではオフです。」がある。
- [ ] 光の玉をオンにして: (1) `frameOrb` を「1 個」にすると、盤面を出したときグリッチの後に玉が現れ（250 ms のフェードイン）、枠線の中心を時計回りに 8 秒で 1 周、尾が付く。**角では面取りの斜辺の上を通る**（横切らない。CHAMFER_GLOW）。(2) Esc で消すとき玉が先に消え、グリッチに映り込まない。(3) 配色をレッド（プリセット）にすると玉と暈が主色の赤になる（「2 個」なら 2 個目が副色の水色）。主色をカスタムで動かしても追従する。(4) 背景画像ありで玉が画像の上に見える。不透明度 50% で玉も薄くなる。(5) 常駐 + 自動で隠すで、収納中は止まり、出てくると再開する。常駐のトリガー前面化（グリッチ）の間だけ止まる。(6) Retina で玉の大きさ・位置が論理 px どおり（枠線の中心に乗る）。(7) 「アニメーション」OFF で止まる。(8) システム設定 → アクセシビリティ → ディスプレイ → 「視差効果を減らす」オンで止まり、設定画面の末尾に注記が出る。
- [ ] 明滅をオンにして: `glowPulse` を「弱」にすると枠の発光が 4 秒周期で呼吸する。**形は八角形のまま**（角で四角く光らない。窓の端で切れない。CHAMFER_GLOW）。配色を変えると発光の色が追従する（焼き直し）。盤面の大きさ（ページの行列数）を変えても発光の形が合う。「なし」に戻すと静的な発光だけが残る。「強」+ `orbTwin` が 2026-10-06 の報告の組み合わせなので、これも一度見る。
- [ ] 値の確定（**任意**。今の「仮」の値で良ければ飛ばす）: 玉の大きさ（芯 2.5 / 暈 9 px）・尾（48 px、10 点）・速さ（スライダー初期値 8 秒）・明滅の強さ（0.45 / 0.9）と周期（初期値 4 秒）を見て変えたければ、`spec/EFFECTS.md` の表の「仮」を確定値に書き換え、`EffectCatalog` / `OrbSprite` / `AmbientAnimator` の定数を合わせる。変えなければ EFFECTS.md の「仮」を「確認済み」に書き換えるだけ。
- [ ] 任意: ユーザーに「内側の玉」（ヘッダー区切り線の往復）と「他の発光（タブ・選択セル・設定画面の枠）の明滅」が要るかを聞く → 要るなら SPEC §12 の候補として残す（v1 では着手しない）。

## ステップ 5: OS の「視差効果を減らす」に従う — 採用・完了（2026-10-04 ユーザー判断）

- [x] `Platform/Interfaces.cs` `IWindowService.PrefersReducedMotion => false`（既定実装。SPEC §10.3）。
- [x] `Platform.MacOS`: `MacWindowService.PrefersReducedMotion` = `AutoreleasePool` 内で `[[NSWorkspace sharedWorkspace] accessibilityDisplayShouldReduceMotion]`（`SendRetBool`）。
- [x] `Platform.Windows`: `SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, out bool on, 0) && !on`。
- [x] `App` が `BoardWindow.ReducedMotion = () => platform.Window.PrefersReducedMotion` を結び、`UpdateAmbient` が読む。`FakePlatform.ReducedMotion` で差し替え可。設定画面は true のとき `Settings_Appearance_Effects_ReducedMotionNote`。
- [x] テスト: `AmbientEffectsTests`「既定では動かず_アニメーションOFFや収納中やOSの設定でも止まる」。
- Windows での動作確認は SPEC §13.3 C17 (6)。

## 将来の検討（SPEC §12 に記載済み。このファイルを閉じても残る）

- 常時の演出を軽くする: 光の玉を盤面に追従する別の透明ウィンドウに描いて盤面本体を静止させる / コマ数を落とす（16 → 33 / 50 ms）/ 文字の発光 `FlTextGlow` の描き方の見直し / 窓全体の再描画を避ける Avalonia 側の設定の調査。軽くなればサイバーパンクの既定（`orb` / `pulse`）を再検討。
- 拡張: ヘッダー区切り線の往復する玉（`OrbPath` に「開いた線分」の派生）、設定画面・ダイアログの枠（`ChromeWindow`）への同じ 2 層、選択タブ・選択セルの発光の明滅。

## 完了条件

- ステップ 4 の見た目の確認がユーザー OK（値の確定は任意）。`dotnet test FileLauncher.sln` が通っている（済み）。
- Windows 側の確認は SPEC §13.3 C17 に残す。完了したらこのファイルを削除する。

## 実装時の注意（値の確定・拡張で触るとき）

- **`PulseLayer` は `Frame` の `BoxShadow` を変えない**（アニメーション OFF の見た目を今のまま保つため）。`BoxShadow` の値や色を毎フレーム変える方式は採らない（再ラスタライズで重い）。
- **`OrbLayer` 全体を `InvalidateVisual` しない**。動かすのは `OrbSprite` の `RenderTransform` と `SetTail` だけ。`OrbSprite.Render` 内でブラシを `new` しない（`SetColor` のときだけ）。`Pen` も使わない（塗りだけ）。
- **`PulseLayer` の要素 `Opacity` を 1 未満にしない**（上を玉が通るたびに層全体の合成になる）。明るさは `Level` → `PushOpacity`。更新は `AmbientAnimator.PulseStep`（2%）以上の差があるときだけ。
- `PulseLayer.Refresh` は盤面の大きさが決まってから（`DispatcherPriority.Loaded`）。焼き直しの判定は大きさ・`RenderScaling`・`FlBoardGlow` の値（・2026-10-06 から面取り・余白）の比較なので、配色の切替は `FlBoardGlow` が変わることで追従する。
- **焼く元は `NeonGlowPlan`**（2026-10-06 設計、`issue/CHAMFER_GLOW.md`）。`PulseLayer` に独自の描き方を持たせない（静的な発光と「同じ形」はコード共有で保証する）。`Chamfer = 0` のときだけ従来の `Border` を焼く。
- `RequestAnimationFrame` のコールバックは UI スレッドで来る。`Stop()` 後に遅れて来た 1 回は `IsRunning` を見て何もしない。
- ヘッドレステストでは `RequestAnimationFrame` が呼ばれない。`AmbientRunning` は `IsRunning` フラグで判定し、位置・明るさは `Ambient.Tick(ms)` で進める（`PulseLayer.Level` を見る）。既定が `none` なので、テストでは `Effects` に `orb` / `pulse` を明示的に入れる。
- グリッチの静止画（`GlitchPlayer`）は `Frame` だけを撮る。`AmbientRoot` を `Frame` の中に入れないこと。
- 表示遅延（SPEC §11「表示まで n ms」）が導入前と変わらないことをログで見る（層は構築時に作り、`Show` では何もしない。`PulseLayer.Refresh` は `glowPulse` が `none` なら呼ばれない）。
- `CompositionOptions.UseRegionDirtyRectClipping`（光の玉が 28% に悪化）と `AvaloniaNativeRenderingMode.Metal`（改善なし）は有効にしない。
- **CPU を測るときは玉が実際に描かれていることを目で確認してから**（最初の計測の誤りの原因）。
