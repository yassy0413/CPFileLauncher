# 常時の演出の負荷削減: 静的な枠の発光の作り置き（A）+ 明滅の 4 本の帯 + 部分塗り直し（B）

設計日: 2026-10-08（designer）。経緯: astra の最適化レポート（`astra-optimize-report.md`、.gitignore 対象のローカルファイル）の候補 A〜E をメインが試作して自動計測 → **ユーザー判断（2026-10-08）: A と B（分割 + 部分塗り直しの組み合わせ）を採用**。C は効果なしで不採用、D / E は将来候補（SPEC §12）。試作は `// MEASURE-TEMP` として src に入っているので、このファイルの形に正式化し、計測用の分岐は削除する。

正となる文書: **SPEC §10.7「同時に入れた最適化」**（2026-10-08 の項）、**§3.6「発光の作り方」の「作り置き」**、§10.5（`FrameChrome` / `NeonGlowLayer` / `PulseLayer` の部品）、§11「常時の演出の CPU」「常駐メモリ」、§12（候補の消し込みと D / E の追加）、§13.3 **C28**（Windows 側の確認）、**`spec/EFFECTS.md`**（冒頭の 2026-10-08 の表、`glowPulse` の「作り方」、「負荷の目標と測り方」、「CPU の計測と既定の判断」の 2026-10-08 の項）、`issue/UI_TESTS.md`「常時の演出の負荷削減（A + B）」、`issue/MAC_SUPPORT.md` 記録欄 C28。

---

## 計測結果（2026-10-08、メインが自動計測。試作の値）

Mac・拡大率 1 の画面（scale=1、Retina ではない）・ソフトウェア描画・Release `.app`・VSync 2・背景画像 bg01.jpg あり。CPU = 20 秒間の `ps cputime` 増分の 1 コア換算、各 2 回。

| 構成 | 現状 | A のみ | A + 分割のみ | A + 部分塗り直しのみ | **A + 分割 + 部分塗り直し（採用）** |
|---|---|---|---|---|---|
| 既定（玉 1 個・明滅弱・帯） | 29.6 | 26.3 | — | — | **22.8** |
| 玉 2 個・明滅強・帯 | 34.5 | 28.1 | 28.5 | 27.6 | **24.2** |

- 切り分け: 静的な枠の発光（`FrameChrome` の `NeonGlowLayer` 外側・内側）を消すと「玉 2 個・明滅強・帯」で 34.4 → 26.5（約 8 ポイントが静的な発光の描き直し）。玉だけの構成では 19.8 → 18.8（小）。**明滅は毎フレーム枠全体を汚す**ので、静的な発光（約 80 帯の `DrawGeometry`）も描き直される → A が効く。
- 明滅弱だけ: 分割 / 部分塗り直し / 両方 = 16.1 → 15.1 / 15.3 / 15.8（誤差程度）。B は玉・帯と重なった構成で効く（明滅の帯を 4 本に分けても、部分塗り直しが無いと汚れた矩形が 1 つに統合されて盤面全体に戻る。レポート B の指摘どおり）。
- C（玉の `Point[]` と `TranslateTransform` の再利用、尾の形が同じなら `InvalidateVisual` しない）: 19.6 vs 19.5 で効果なし → **不採用**（コードを削除）。
- 隠した後はどれも 0.4〜0.6%。メモリ +4〜7 MB（焼き込みの画像 2 枚）。

**astra レポートの判定表（転記）**

| 案 | 判定 | 根拠 |
|---|---|---|
| A 静的な枠の発光の画像化 | **採用** | 既定構成 29.6 → 26.3、負荷構成 34.5 → 28.1。見た目は同じ（焼く元が同じ `NeonGlowPlan`）。メモリ +数 MB |
| B 明滅の分割 + 部分塗り直し | **採用（組み合わせで）** | A の上にさらに 26.3 → 22.8 / 28.1 → 24.2。単独では誤差程度 |
| C 玉の配列・Transform の再利用 | **不採用** | 19.6 vs 19.5。割り当ては主因ではなかった |
| D 最適化後の GPU / Software 再比較 | 未実施（将来） | SPEC §12「Windows の GPU 描画の再評価」に含める。Avalonia の版を上げたときに |
| E Mac の演出を Core Animation の独立レイヤーへ | 未実施（将来候補） | SPEC §12 の常時の演出の候補に追記。OS 固有の技術検証が要る |

---

## 決めたこと（要約）

- **A: `NeonGlowLayer` が焼き込みのキャッシュを持つ**（`Bake = true` のときだけ。盤面 = true、`ChromeWindow`（設定画面・ダイアログ）= false）。作るのは **`Refresh()` の中だけ**（`BackdropLayer` / `PulseLayer` と同じ流儀。`Render` では作らない）。契機 = `SizeChanged`・`Chamfer`・`Shadows`（配色）・`TopLevel.ScalingChanged`・`OnAttachedToVisualTree`。`Render` は鍵が合えば `DrawImage` 1 回、合わなければ従来どおりベクタで描いて `Refresh` を予約（最初のフレームから正しい見た目を保証し、取りこぼしの保険にする）。余白は定数 48 ではなく **`NeonGlowPlan.OutwardExtent`（外側の帯の最大距離）+ 1 px**（外側の層で約 28 px、内側の層は 1 px）。外側・内側は別々の画像（重なり順を保つ。1 枚に統合しない）。
- **B 分割: `PulseLayer` を `Canvas` にし、子の `PulseStrip` 4 本（上・下・左・右）が焼いた画像の帯だけを描く**。`PulseLayer` 自身は何も描かない。帯の厚み = 外側 `FlGlowMargin`（40）+ 内側 `ceil(NeonGlowPlan.Extents(shadow).Inner) + 1`（内側の光が消えきる距離から計算。試作の `Inner = 24` は固定値だった）。**厚みはデバイス px で整数に丸める**（継ぎ目に 1 px の線や隙間が出ないため）。上下の帯は全幅、左右の帯はその間 = 重なりも隙間も無い。`BoardWindow` の構成は変わらない（`_ambientRoot` の子は `PulseLayer` → `ScanBeamLayer` → `OrbLayer` のまま。試作の「`BoardWindow` が帯を `Insert`」は無くす）。
- **B 部分塗り直し: `CompositionOptions { UseRegionDirtyRectClipping = true }` を両 OS 共通で有効にする**（`Program.BuildAvaloniaApp`）。2026-10-04（GPU 描画で玉が悪化）/ 2026-10-07（単独では変化なし）の記録は残し、「明滅の分割と組み合わせたソフトウェア描画では効く」ことを追記。**Windows は未計測**（§13.3 C28）。Windows で悪化（CPU・描画の乱れ）したら `OperatingSystem.IsMacOS()` で macOS だけに限る（Platform 層は増やさない。描画方式と同じく `Program` のオプション指定）。
- **変えないもの**: 設定項目・JSON スキーマ（変更なし）・Platform（変更なし）・動く条件・250 ms のフェードイン・VSync・`AmbientAnimator`（`PulseStep` 2% もそのまま）・`OrbLayer`（C は不採用で試作を削除し、元の形に戻す）・静的な発光の見た目（焼く元が同じ `NeonGlowPlan` なので同じ形。画素テストで保証）。
- **メモリ**: 盤面の焼き込み 2 枚 = scale 1 で約 1.3 MB、scale 2 で約 5 MB（420×330 の盤面、外側 pad 28 / 内側 pad 1）。SPEC §11 の 150 MB の内数。`ChromeWindow` を焼かないのはこのため（設定画面 640×715 は Retina で 2 枚 17 MB になり、しかも常時描き直しが無いので効果が薄い）。

---

## ステップ 0: 試作（MEASURE-TEMP）の整理

- [x] `Themes/NeonGlowLayer.cs`: `BakeGlow`（環境変数 `CPFL_BAKE_GLOW`）・`BakePad = 48`・`DrawBaked` を削除し、ステップ 1 の形に置き換える。
- [x] `Effects/PulseLayer.cs`: `Split`（環境変数 `CPFL_PULSE_SPLIT`）・`Strips`（`List<Control>`）・`Image` / `Margin2` の試作用公開を削除し、ステップ 2 の形に。
- [x] `Effects/PulseStrip.cs`: 試作を捨て、ステップ 2 の `PulseStrip` に書き直す（ファイル名は同じでよい）。
- [x] `BoardWindow.axaml.cs`: コンストラクタの `if (PulseLayer.Split) … _ambientRoot.Children.Insert(1, strip)` を削除。
- [x] `Program.cs`: `UseRegionDirtyRectClipping = Environment.GetEnvironmentVariable("CPFL_DIRTY") == "1"` → **`= true`**（コメントに 2026-10-08 の値と「明滅の分割と組み合わせて効く。Windows は C28 で確認、悪化なら macOS 限定」を書く）。
- [x] `Themes/FrameChrome.cs`: `Bind()` の `Environment.GetEnvironmentVariable("CPFL_DIAG_NOGLOW")` を外し `_outerGlow.IsVisible = _innerGlow.IsVisible = _chamfer > 0;` に戻す。
- [x] `Effects/OrbLayer.cs`: **C は不採用** → `OrbReuse`（`CPFL_ORB_REUSE`）・`OrbSprite.TailBuffer` / `_shown` / `_move` / `MoveTo` と `SetTail` の分岐を削除し、`Place` は `new Point[TailPoints]` + `new TranslateTransform(...)` の元の形に戻す。
- [x] `grep -rn "MEASURE-TEMP\|CPFL_" src/` が 0 件（`CPFL_FRAME_MS` 等の過去の分岐も残っていないこと）。

## ステップ 1: A — `NeonGlowLayer` の焼き込みキャッシュ（`Themes/NeonGlowLayer.cs`、`Themes/FrameChrome.cs`、`BoardWindow.axaml`）

- [x] `NeonGlowPlan` に足す:
  - `public double OutwardExtent { get; }`: 外側の帯（`outer`）の最大の `Outer` 距離（px）。外側の帯が無ければ 0。`Create` で計算して保持。
  - `public static (double Outer, double Inner) Extents(BoxShadows shadows)`: `Layers(shadows, inset: false/true)` → `NeonProfile.Extent(layers, AmbientAnimator.MaxPulseGain)`（層が無ければ 0）。ジオメトリは作らない（`PulseLayer` が帯の厚みを決めるのに使う。`Create` と同じ gain を使うので「焼いた画像の光が消えきる距離」と一致する）。
- [x] `NeonGlowLayer`:
  - `public bool Bake { get; set; }`（既定 false。true にしたら `Refresh()`、false にしたら `Discard()` + `InvalidateVisual()`）。
  - 状態: `NeonGlowPlan? _plan`（今のまま）、`RenderTargetBitmap? _baked`、`(NeonGlowPlan Plan, double Scale) _bakedKey`、`double _pad`、`TopLevel? _topLevel`。テスト用 `internal Bitmap? BakedImage`、`internal int BakeCount`、`internal bool UsesFallback`、`internal double BakePad => _pad`。
  - `private NeonGlowPlan EnsurePlan()`: `frame = new Rect(Bounds.Size)`、`_plan` が無いか `Matches` しなければ `Create`（今の `Render` 冒頭の処理を切り出す）。
  - `public void Refresh()`: `!Bake` か `Bounds` の辺が 1 未満なら `Discard()` + `InvalidateVisual()` して return。`plan = EnsurePlan()`、`scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1`。`_baked` があり `_bakedKey == (plan, scale)` なら return。`_pad = Math.Ceiling(plan.OutwardExtent) + 1`。`full = Bounds.Size + 2 × _pad`、`px = ceil(full × scale)`（1 以上）。`new RenderTargetBitmap(px, new Vector(96 × scale, 96 × scale))` → `CreateDrawingContext()` + `PushTransform(Matrix.CreateTranslation(_pad, _pad))` で `plan.Draw(ctx)`。`try / catch` で失敗なら `Discard()`・`UsesFallback = true`・ログ 1 行（以後ベクタで描く。次の `Refresh` でまた試す）。成功なら古い画像を `Dispose` して差し替え、`_bakedKey` 更新、`BakeCount++`、`UsesFallback = false`、`AppLog.Info($"枠の発光の作り置き（{Part}）: {px.Width}×{px.Height} px, {ms} ms")`。最後に `InvalidateVisual()`。
  - `Render(DrawingContext context)`: `plan = EnsurePlan()`。`Bake && _baked is not null && _bakedKey == (plan, scale)` なら **`context.DrawImage(_baked, new Rect(_baked.Size), new Rect(-_pad, -_pad, _baked.Size.Width, _baked.Size.Height))`**（dest は画像の論理サイズをそのまま使い、`frame.Inflate(pad)` にしない = 等倍で貼る。ピクセル数を ceil した分の端数で拡大縮小が入らないように）。そうでなければ `plan.Draw(context)`（従来のベクタ描画 = 最初のフレーム・拡大率が変わった直後のフレーム・失敗時の見た目を保証）し、`Bake && !UsesFallback` なら `Dispatcher.UIThread.Post(Refresh, DispatcherPriority.Render)` で次のフレームまでに焼く（`Render` の中で `RenderTargetBitmap` を作らない）。
  - `RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None)`（等倍で貼る前提。`BackdropLayer` と同じ。レイアウトの丸めで層の原点はデバイス px に揃うので画素がそのまま写る）。
  - 契機: コンストラクタで `SizeChanged += (_, _) => Refresh()`。`Chamfer` setter で `InvalidateVisual()` に加えて `Refresh()`。`OnPropertyChanged` で `ShadowsProperty` が変わったら `Refresh()`（`AffectsRender` は残す。配色の切替 = `FlBoardGlow` の差し替えで来る）。`OnAttachedToVisualTree` で `_topLevel = TopLevel.GetTopLevel(this)`、`ScalingChanged += OnScalingChanged`（→ `Refresh`）、`Refresh()`。`OnDetachedFromVisualTree` で購読解除 + `Discard()`。`IsVisible` が false（`Chamfer = 0`）の間は `Render` が呼ばれないだけなので何もしない（`Chamfer` setter の `Refresh` が `Bounds` ≥ 1 なら焼くが、`FrameChrome.Bind` が `IsVisible = false` にするので描かれない。無駄を避けたければ `Chamfer <= 0` のとき `Discard`）。
  - `Discard()`: `_baked?.Dispose(); _baked = null; _bakedKey = default;`。
  - クラスの summary に「盤面では画像に焼いて貼る（SPEC §3.6「作り置き」。明滅が毎フレーム枠全体を汚すので、静的な約 80 帯の `DrawGeometry` も毎フレーム描き直されていた。2026-10-08 計測で既定構成 29.6 → 26.3%）」を 1 行。
- [x] `FrameChrome`: `public bool BakeGlow { get => …; set { _outerGlow.Bake = _innerGlow.Bake = value; } }`（既定 false）。テスト用に `OuterGlow` / `InnerGlow` は公開済み。
- [x] `BoardWindow.axaml`: `<local:FrameChrome x:Name="Chrome" … BakeGlow="True">`。`ChromeWindow` は指定しない（false のまま = 従来どおりベクタ）。
- [x] `BoardWindow.EnsureScaling()`: 変更不要（`ScalingChanged` の購読と `Render` 内の鍵の確認で追従する）。Debug ログで確認: 起動（準備運動）で「枠の発光の作り置き（Outer / Inner）」が各 1〜2 回出て、以後の Hide / Show・ページ切替（同じ行列数）では出ない。行列数の違うページへ切り替えたとき・配色を変えたときに 2 行出る。
- [x] テスト（`NeonGlowTests`。観点は `issue/UI_TESTS.md`「常時の演出の負荷削減（A + B）」）: `Draw(setup)` ヘルパーに `bake: bool` を足し、既存の画素テスト 3 件（`発光は辺から外へ単調に弱まり_…` / `角で四角く光らず_…` / `内側の光は枠線のすぐ内側に…`）を **`[AvaloniaTheory] [InlineData(false)] [InlineData(true)]`** で両方の経路に対して走らせる。追加: `焼いた発光はベクタ描画と同じ画素`（bake なし / あり の 2 枚を撮って、上辺の法線 10 点 + 斜辺の法線 3 点 + 内側 3 点で `AssertNear`）/ `焼き込みは大きさ_面取り_影_拡大率が変わったときだけ作り直す`（`BakeCount` と `BakedImage` の参照: `InvalidateVisual` + `RunJobs` で変わらない、`Width` を変えると変わる、`Shadows` を別の `BoxShadows` にすると変わる、`Chamfer` を 12 にすると変わる）/ `焼いた画像の余白は発光が消えきる距離以上`（`OuterGlow.BakePad >= NeonProfile.Extent(FlBoardGlow の外側 3 層, MaxPulseGain)`、`BakedImage.PixelSize == ceil((W + 2 pad) × scale)`。内側の層の `BakePad == 1`）/ `面取りなしと窓から外したときは焼き込みを持たない`（`Chamfer = 0` → `IsVisible == false`、`window.Close()` 後 `BakedImage == null`）/ `ChromeWindow の枠は焼かない`（`Chrome.BakeGlow == false`、`OuterGlow.BakedImage == null`）/ `盤面の枠は焼く`（`BoardWindow` を `Show` して `Chrome.OuterGlow.BakedImage != null`、`BakeCount >= 1`）。

## ステップ 2: B — `PulseLayer` の 4 本の帯（`Effects/PulseLayer.cs`、`Effects/PulseStrip.cs`）

- [x] `PulseStrip : Control`（`internal sealed`。`IsHitTestVisible = false`、`ClipToBounds = false`、`RenderOptions.SetBitmapInterpolationMode(this, None)`）: `internal void Set(Bitmap? image, Rect source)`（画像と切り出す矩形。setter で `InvalidateVisual`）、`internal double Level`（親が入れる。setter で変化時だけ `InvalidateVisual`）。`Render`: `image is null || Level <= 0` なら return、`using (context.PushOpacity(Level)) context.DrawImage(image, source, new Rect(Bounds.Size))`。`source` と `Bounds.Size` は同じ論理サイズ（等倍）。
- [x] `PulseLayer : Canvas`（`Control` から変更。`IsHitTestVisible = false`、`ClipToBounds = false`）: 子に `PulseStrip` 4 本（`Top` / `Bottom` / `Left` / `Right` の順。`internal IReadOnlyList<PulseStrip> Strips`）。**自分の `Render` は何も描かない**（override を消す）。
  - `Level` setter: 変化時に 4 本へ `strip.Level = value`（`InvalidateVisual` は帯だけ。層全体を汚さない）。
  - `Refresh()`: 焼く処理は今のまま（鍵 = 大きさ・`RenderScaling`・`FlBoardGlow`・`Chamfer`・余白）。**焼いた後（鍵が同じで return するときは何もしない）に `ArrangeStrips()`**。
  - `ArrangeStrips()`: `m = _margin`、`scale = _bakedScale`、`(_, innerExtent) = NeonGlowPlan.Extents(shadow)`（`Chamfer = 0` の `Border` 経路でも同じ式でよい。`shadow` が `BoxShadows` でなければ 0）、`depth = Math.Ceiling(innerExtent) + 1`、**`bPx = Math.Ceiling((m + depth) × scale)`、`b = bPx / scale`**（デバイス px で整数）。画像の論理サイズ `S = _bitmap.Size`（`W = S.Width`、`H = S.Height`。ピクセル数を ceil しているので `Bounds + 2m` より僅かに大きいことがある → 画像のサイズを正とする）。画像座標（左上 = 0,0）の 4 矩形: Top `(0, 0, W, b)`、Bottom `(0, H − b, W, b)`、Left `(0, b, b, H − 2b)`、Right `(W − b, b, b, H − 2b)`。層座標 = 画像座標 − `m`（`Canvas.SetLeft/SetTop(strip, x − m, y − m)`、`Width` / `Height` = 矩形の大きさ）。各帯に `Set(_bitmap, 矩形)`。`H − 2b` が 0 以下（極端に小さい盤面）なら Left / Right は `IsVisible = false`。
  - `OnDetachedFromVisualTree`: 画像を `Dispose` した後、4 本に `Set(null, default)`。
  - テスト用 `internal`: `Strips`、`double StripDepth`（`b`）、既存の `BakedImage`。
  - クラスの summary に「2026-10-08: 画像を枠の周り 4 本の帯（別の Visual）に分けて描く。中央の透明な部分まで汚さないため。`UseRegionDirtyRectClipping` と組み合わせて効く（分割だけでは汚れた矩形が 1 つに統合される）」。
- [x] `BoardWindow`: `_ambientRoot` の子は `{ _pulseLayer, _beamLayer, _orbLayer }` のまま。`_pulseLayer.SizeChanged` / `ActualThemeVariantChanged` / `UpdateAmbient` の `Refresh` 呼び出しも今のまま（`Refresh` が帯を並べ直す）。
- [x] テスト（`NeonGlowTests` / `AmbientEffectsTests`。観点は UI_TESTS）: 既存 `明滅の画像は静的な発光と同じ八角形の形` はそのまま通る（`BakedImage` を見る）。追加: `明滅の帯は重ならず隙間なく枠を囲み_中央は描かない`（`Refresh` 後、4 本の `Bounds`: 互いの交差が空、上下は幅 `W`、左右は高さ `H − 2b`、`StripDepth × scale` が整数、`StripDepth − margin ≥ NeonGlowPlan.Extents(FlBoardGlow).Inner`）/ `明滅の帯の描画は焼いた画像と同じで継ぎ目に線が出ない`（`Draw` で静的な発光（`OuterGlow` / `InnerGlow`）を `IsVisible = false` にし、`PulseLayer` を `Overlays` に置いて `Level = 1` → `Refresh` → 窓を撮る。上辺の中央の法線上の 5 点と、**左の帯と上の帯の継ぎ目 `y = b − 1 / b / b + 1`（x は左辺の 2 px 外）**、角 `(top, top)` で窓の画素 == `BakedImage` の画素（`AssertNear`）。中央 `(W/2, H/2)` は 0）/ `明滅のLevelが0なら帯は何も描かない`（`Level = 0` で窓の上辺の外が 0）。`AmbientEffectsTests` の既存 `PulseLayer.Level` のテストはそのまま。

## ステップ 3: 部分塗り直しの記録とビルド

- [x] `Program.cs` の変更（ステップ 0）。`dotnet build FileLauncher.sln && dotnet test FileLauncher.sln` が通る（Core 167 + Desktop 66 + 追加分）。描画ありフィクスチャ（`NeonGlowTests` / `GlitchTests` / `BackgroundTests`）で `RenderTargetBitmap` の中に `DrawImage`（焼いた発光）が入っても例外が出ない。
- [x] `spec/EFFECTS.md`「CPU の計測と既定の判断」の `UseRegionDirtyRectClipping` の記録は designer が更新済み（2026-10-04 悪化 / 2026-10-07 単独で変化なし / **2026-10-08 分割と組み合わせて採用**）。実装後に値が変われば直す。

## ステップ 4: 再計測と記録（メインの自動計測。**2026-10-08 と同じ手順**: Mac・拡大率 1 の画面・ソフトウェア描画・Release `.app`（`src/build/publish-mac.sh`）・VSync 2・背景画像 bg01.jpg あり・20 秒間の `ps cputime` 増分の 1 コア換算・各 2 回。玉・帯・明滅が実際に動いているのを目で確認してから）

**2026-10-08 実装後の再計測（正式版。Mac・拡大率 1 の画面・ソフトウェア描画・Release `.app`・VSync 2・背景画像 bg01.jpg・20 秒の CPU 時間の 1 コア換算 × 2 回。Claude が自動で）**:

| 構成 | 導入前 | **導入後（A + B）** | footprint 表示中 |
|---|---|---|---|
| 既定（玉 1 個・明滅弱・帯） | 29.6% | **22.8%**（22.5 / 23.1） | 129〜131 MB |
| 玉 2 個・明滅強・帯 | 34.5% | **24.7%**（24.3 / 25.1） | 132〜133 MB |
| 明滅弱だけ | 16.1% | **10.4%** | 123〜129 MB |
| 玉 1 個だけ | 19.6% | **18.9%** | 128〜131 MB |
| すべてなし | — | 0.6% | 115〜119 MB |
| 隠した後（どれも） | 0.4〜0.6% | 0.4〜0.6% | — |

実装で設計から変えた点: 作り置きの余白（`OutwardExtent + 1`）を**デバイス画素の整数に切り上げる**（端数の余白だと画像の格子が盤面の格子から半画素ずれ、辺のすぐ外の明るさが 86 → 75 に落ちた。画素テストで発見）。層の位置が画素の途中にある場合の端数も焼くときに足して貼るときに戻す（`DeviceFraction`）。

- [x] 構成: 既定（玉 1 個・明滅弱・帯）/ 玉 2 個・明滅強・帯 / 明滅弱だけ / 玉 1 個だけ / 帯だけ / 常時の演出すべてなし / 隠した後。背景画像なしの既定構成も 1 回（SPEC §11 (2) の「既定構成・背景画像なし」の実測 = `issue/AMBIENT_SETTINGS.md` ステップ 4 の残りと兼ねる）。
- [x] あわせて: footprint（表示中 / 隠した後 5 秒以降）、「表示まで n ms」4 回（焼き込みは準備運動の中で済むので変わらないはず。初回だけ焼く時間が乗る可能性 → ログ「枠の発光の作り置き」の ms を見る）、Debug ログで作り置きの回数（起動で Outer / Inner 各 1〜2 回。Hide / Show で 0 回）。
- [ ] **Retina（scale 2）** が使えれば同じ構成で 1 回（ソフトウェア描画は塗る画素が 4 倍。§13.3 C27 の残りと兼ねる）。
- [x] 結果を `spec/EFFECTS.md` 冒頭の 2026-10-08 の表に「実装後」の列（または行）として足し、SPEC §10.7 / §11 の要約（既定構成の実測）を更新。試作値（29.6 → 22.8 / 34.5 → 24.2）と大きくずれたら原因を書く。

## ステップ 5: Mac での見た目の確認（ユーザー。Release `.app`）

- [ ] **発光が今と同じ**: 盤面の 4 辺・斜辺・角の発光が実装前（2026-10-06 の確認時）と同じに見える（ぼけ・段差・明るさの差が無い。スクリーンショットを並べて比べる）。内側の光（枠線のすぐ内側の染み）も同じ。配色をレッド等に変えると即座に追従（焼き直し）。ページの行列数が違うページへ切り替えても形が合う。
- [ ] **明滅の帯の継ぎ目なし**: 明滅「強」+ `orbTwin` + 帯で、上下の帯と左右の帯の境（枠の上辺から約 60 px 下・下辺から約 60 px 上の高さの、枠の外側〜内側 60 px の範囲）に線・隙間・明るさの段差が無い。4 隅も同じ。明滅の最も明るい瞬間と暗い瞬間の両方で見る。
- [ ] **部分塗り直しで描画の乱れなし**: 玉の尾・帯の残像・ゴースト（前のフレームが残る）が無い、ホバーの発光・ツールチップ・右クリックメニュー・ページ切替（fade）・ドラッグのゴースト・トーストが乱れない、不透明度 50% でも乱れない、ステータス行の時刻（`seconds`）の更新で乱れない。
- [ ] **Retina**: 発光がぼけない（等倍で貼れている）、帯の継ぎ目が無い、玉の大きさが変わらない。外部モニタ（scale 1）⇄ Retina を跨いで盤面を出し直すと発光がそのまま（拡大率で焼き直し。ログに「枠の発光の作り置き」が出る）。
- [ ] **グリッチ**: 表示・非表示のグリッチの静止画に枠の発光が写っている（従来どおり。`RenderTargetBitmap.Render(Frame)` の中で焼いた画像を `DrawImage` する経路）。設定画面・ダイアログ（焼かない）の見た目とグリッチは変わらない。
- [ ] 隠した後の CPU 0.5% 以下、メモリの増分が +数 MB にとどまる（footprint）。

## ステップ 6: Windows（次の Windows 確認時。SPEC §13.3 **C28**、`issue/MAC_SUPPORT.md` Mac-0）

- [ ] C28 の項目: 発光の見た目（100% / 125% / 150% / 200%）、帯の継ぎ目、部分塗り直しの乱れ（ソフトウェア描画 + 透過窓）、CPU（全部オン・既定・すべてなし。2026-10-07 の 8.6% と比べる）、隠した後 0.5% 以下、「表示まで n ms」。**悪化（CPU が上がる・描画が乱れる）なら `UseRegionDirtyRectClipping` を `OperatingSystem.IsMacOS()` で macOS 限定にして SPEC §10.7 を直す**（A と分割は OS に依らず残す）。

## 完了条件

- ステップ 0〜4 が済み、`MEASURE-TEMP` / `CPFL_` の分岐が無く、テストが通り、再計測の値が EFFECTS.md / SPEC §10.7 / §11 に記録されている。ステップ 5 の見た目をユーザーが Mac で確認して OK。Windows 側は C28 に残す。済んだらこのファイルを削除する（`astra-optimize-report.md` は .gitignore 対象のローカルファイルなので扱いはユーザーに任せる。判定表はこのファイルと SPEC §12 に転記済み）。

## 実装時の注意

- **`RenderTargetBitmap` は `Refresh` の中だけで作る**。`Render` の中では `DrawImage` かベクタ描画だけ（`Render` 内で焼くと、グリッチの静止画 `RenderTargetBitmap.Render(Frame)` の記録中に別の描画が走る）。`Render` から `Refresh` を呼ぶときは必ず `Dispatcher.UIThread.Post`。
- **焼いた画像は等倍で貼る**: dest は `new Rect(-pad, -pad, bitmap.Size.Width, bitmap.Size.Height)`（画像の論理サイズ）。`Bounds` から計算した矩形に貼ると、ピクセル数を ceil した端数ぶん拡大縮小が入ってぼける。帯も同じ（`source` と `Bounds.Size` を同じ論理サイズに）。
- **帯の厚みはデバイス px で整数に**（`ceil((m + depth) × scale) / scale`）。Windows の 125% / 150% で継ぎ目に 1 px の線が出るのを防ぐ。
- **外側と内側は別の画像**（外側は面の下、内側は面の上。重なり順を保つ）。1 枚に統合しない。
- **`ChromeWindow` は焼かない**（`FrameChrome.BakeGlow` 既定 false）。盤面だけ `BoardWindow.axaml` で true。将来 `ChromeWindow` にも常時の演出を足す（SPEC §12）ならそのとき true にする。
- 焼き直しの鍵は `(plan, scale)`。`plan` は `(frame, chamfer, shadows)` で `Matches` するので、配色の切替（`FlBoardGlow` の `DynamicResource` 差し替え）→ `Shadows` 変化 → `Refresh` で追従する。`PulseLayer` の鍵（大きさ・scale・`FlBoardGlow`・`Chamfer`・余白）と同じ契機。
- `PulseLayer.Level` の更新は帯 4 本の `InvalidateVisual` だけ。**`PulseLayer` 自身や `_ambientRoot` を `InvalidateVisual` しない**（盤面全体が汚れて B の効果が消える）。`PulseStep`（2%）はそのまま。
- `UseRegionDirtyRectClipping` は `Program.BuildAvaloniaApp` の 1 か所。OS で分けるときも `Program` の中（Platform のインターフェースは増やさない）。
- 計測は Release `.app`、玉・帯・明滅が描かれているのを目で確認してから。隠した後のメモリは `MemoryTrim` の 5 秒後以降。
- 既存のテスト名・観点は `issue/UI_TESTS.md`「八角形に沿う枠の発光」「常時の演出」節の [x] を崩さない（画素テストの許容 ±4 / ±20% は焼き込みでも同じ）。
