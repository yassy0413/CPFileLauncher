# 常時の演出の CPU を下げる: 更新を約 30 fps に + 背景画像の作り置き

設計日: 2026-10-07。**同日ステップ 1〜4 実装・再計測済み**（残り: 30 fps の見た目と背景画像の見た目のユーザー確認、ステップ 5 の判断）。設計日の記録: 2026-10-07（designer）。ユーザーの問い「表示処理の FPS はどのくらい？描画を回し過ぎの可能性は？」→ メインの自動計測（Mac Retina、Release `.app`、ソフトウェア描画固定後）→ ユーザー「両方進めて」。

正となる文書: **`spec/EFFECTS.md`「常時の演出の詳細」の「時計」の行と「CPU の計測と既定の判断」の 2026-10-07 の表**、**SPEC §3.7「表示方法の意味」「作り置き」**、§10.5（`BackgroundLayout` / `BackdropLayer`）、§10.7「同時に入れた最適化」、§11「常時の演出の CPU」、§12、§13.3 C11 (10) / C27、`spec/SETTINGS.md` 背景画像の行、`issue/UI_TESTS.md`「常時の演出の CPU」。

---

## 決めたこと（要約）

- **更新間隔**: `AmbientAnimator.MinFrameMs` を 16 → **30 ms**（約 30 fps。60 Hz で 2 フレームに 1 回、120 Hz で 4 フレームに 1 回）。`RequestAnimationFrame` の毎フレーム再登録はそのまま、更新しないフレームでは何も触らない。**33 ではなく 30**: 60 Hz の 2 フレーム = 33.3 ms が時計の揺れで 33 を下回ると 3 フレーム目まで待って 20 / 30 fps が混ざる（計測で 33 ms 指定が 26〜30 fps とばらついた）。両 OS 共通の定数、**設定項目にしない**。
- **背景画像の作り置き**: `Border` + `ImageBrush` の `Backdrop` を `Themes/BackdropLayer : Control` に置き換え、面の大きさ × `RenderScaling` に一度だけ縮小した `RenderTargetBitmap` を等倍で貼る。表示方法 5 種の意味は Core の純粋な数式 `BackgroundLayout.Compute` に移す（作り置き・失敗時の直描き・テストで共有）。画像の不透明度は要素の `Opacity` ではなく描画時の `PushOpacity`。作り直しは「大きさ・拡大率・表示方法・画像の参照」が変わったときだけ。
- **計測の根拠**（EFFECTS.md の表）: 全部オン 60 fps 51.5% → 30 fps 35% → 20 fps 33〜34%（描き直し回数に比例。20 fps は採らない）。背景画像が約 15% 上乗せ（玉だけ 17 fps: あり 30.8% / なし 15.8%）。効かなかったもの: タイマーで必要なときだけ要求、`UseRegionDirtyRectClipping`、`FlTextGlow` を外す。
- **変えないもの**: 動く条件・250 ms のフェードイン・停止中はフレーム要求なし・既定値（`frameOrb` / `glowPulse` = none、`scanBeam` = beam / 4000）・設定項目・JSON スキーマ（変更なし）・Platform（変更なし。OS 固有処理は増えない）。
- **再計測後に決めること**（要ユーザー判断）: 注記 `Settings_Appearance_Effects_AmbientNote` の「20〜30% 程度」の数値、`frameOrb` / `glowPulse` の既定を見直すか（2026-10-04 案 A）。

---

## ステップ 1: 30 fps 化（`AmbientAnimator`）

- [x] `Effects/AmbientAnimator.cs`: `MinFrameMs` を定数 **30** に。間引きの判定を `internal static bool ShouldTick(double lastFrameMs, double frameMs)`（`frameMs - lastFrameMs >= MinFrameMs`、`lastFrameMs` が `NegativeInfinity` なら true）のような純粋な形に切り出し、`OnFrame` は `RequestAnimationFrame` のコールバックが渡すフレーム時刻（`TimeSpan`）でこれを呼ぶ。位置・明るさの計算（`Tick(elapsedMs)`）は従来どおり `Stopwatch` の経過時間（`Stop` で止まり再開は続きから）。`Start()` 直後の最初の `Tick` は従来どおり即座に。
- [x] 計測用の一時コード（`// MEASURE-TEMP`: 環境変数 `CPFL_FRAME_MS` / `CPFL_FRAME_MODE`、5 秒ごとの fps ログ）は**ステップ 4 の再計測が済むまで残し、済んだら削除する**（製品コードに環境変数の分岐を残さない）。再計測では `CPFL_FRAME_MS=16` と既定（30）を比べる。
- [x] コメント（クラスの summary「更新は 16 ms 以上の間隔」）を 30 ms に。
- [x] テスト `AmbientEffectsTests`: `MinFrameMs == 30` / `ShouldTick` が 29 ms で false・30 ms で true・最初のフレームで true。既存の `Tick(ms)` 直呼びのテストは影響なし（`issue/UI_TESTS.md`）。

## ステップ 2: 背景画像の作り置き（Core の数式）

- [x] `Core/Theming/BackgroundLayout.cs`（static）: `BackgroundPlacement Compute(double faceWidth, double faceHeight, double imageWidth, double imageHeight, BackgroundFit fit)`。`BackgroundPlacement`（`readonly record struct`。Core には `Rect` / `Size` 型が無いので `Octagon` / `NeonProfile` と同じく double で持つ）= `(double X, double Y, double Width, double Height) Source`（元画像の論理 px 矩形）/ `(double X, double Y, double Width, double Height) Dest`（面の論理 px 矩形。`tile` では左上の 1 枚）/ `int TileColumns` / `int TileRows`（`tile` 以外は 1×1）/ `bool IsEmpty`（面か画像の辺が 0 以下）。App 側で Avalonia の `Rect` に詰め替える。規則は SPEC §3.7「表示方法の意味」:
  - `fill`: 倍率 `s = max(W/w, H/h)`、`Source` = 画像の中央の `(W/s)×(H/s)`、`Dest` = 面全体。
  - `fit`: 倍率 `s = min(W/w, H/h)`、`Source` = 画像全体、`Dest` = `(w·s)×(h·s)` を面の中央に。
  - `stretch`: 画像全体 → 面全体。
  - `center`: 原寸（1 画像 px = 1 論理 px）を面の中央に。面より大きい辺は `Source` を中央で切り `Dest` をその辺いっぱいに。
  - `tile`: `Dest` = `(0, 0, w, h)`、`TileColumns = ceil(W/w)`、`TileRows = ceil(H/h)`。
  - Core は Avalonia に依存しないので `Size` / `Rect` は Core にある型（`Octagon` / `OrbPath` が使っているもの）か `double` の組で表す。
- [x] テスト `FileLauncher.Tests/BackgroundLayoutTests.cs`（観点は `issue/UI_TESTS.md`）。

## ステップ 3: 背景画像の作り置き（App の層）

- [x] `Themes/BackdropLayer.cs : Control`（`IsHitTestVisible = false`、`ClipToBounds = true`。`RenderOptions.SetBitmapInterpolationMode(this, None)`）:
  - プロパティ `Source : Bitmap?` / `Fit : BackgroundFit` / `ImageOpacity : double`（0.1〜1）。`Source` / `Fit` の setter は `Refresh()`、`ImageOpacity` の setter は `InvalidateVisual()` だけ。
  - `Refresh()`: `Bounds.Size` が 1 px 未満か `Source` が null なら作り置きを `Dispose` して `InvalidateVisual`。それ以外は鍵（`Bounds.Size`、`TopLevel.RenderScaling`、`Fit`、`Source` の参照）を前回と比べ、同じなら return。違えば `Stopwatch` を回して `RenderTargetBitmap(PixelSize = ceil(Bounds × scale), Dpi = 96 × scale)` を作り、`CreateDrawingContext()` に `BackgroundLayout.Compute(Bounds.Size, Source.Size, Fit)` の矩形で `DrawImage(Source, Source 矩形, Dest 矩形)`（`tile` は `TileColumns × TileRows` 回、`Dest` を `w` / `h` ずつずらす。描くときは `HighQuality`）。古い作り置きを `Dispose` して差し替え、`BakeCount++`、`AppLog.Info($"背景の作り置き: {px.Width}×{px.Height} px, {ms} ms")`、`InvalidateVisual`。例外なら `UsesFallback = true` にしてログ 1 行、作り置きなしで続ける（次の `Refresh` でまた試す）。
  - `Render(DrawingContext)`: `Source` が null なら何も描かない。作り置きがあれば `using (context.PushOpacity(ImageOpacity))`（1 なら掛けない）で `DrawImage(baked, new Rect(baked.Size), new Rect(Bounds.Size))`（等倍）。無ければ（失敗時）同じ `Compute` の矩形で `Source` を直接 `DrawImage`（この経路だけ補間 `HighQuality` を一時的に `PushRenderOptions` で掛ける）。
  - 契機: `SizeChanged += (_, _) => Refresh()`（同期。面の大きさが変わる操作のすべて）、`OnAttachedToVisualTree` で `TopLevel.ScalingChanged += Refresh` の購読（`OnDetachedFromVisualTree` で解除 + 作り置きの `Dispose`）。
  - テスト用 `internal`: `BakedImage`（`Bitmap?`）/ `BakeCount` / `UsesFallback`。
- [x] `BoardWindow.axaml`: `<Border x:Name="Backdrop" …>` を `<local:BackdropLayer x:Name="Backdrop" />` に（同じ位置 = `Grid` の最初の子、`Overlay` の下）。`CornerRadius` は不要（切り抜きは `FrameChrome` の中身の `Clip`）。
- [x] `BoardWindow.ApplyBackground(bg, image)`: `ImageBrush` の組み立てを捨て、`Backdrop.Source = image`、`Backdrop.Fit = bg.Fit`、`Backdrop.ImageOpacity = bg.ImageOpacity / 100.0`。画像なしは `Backdrop.Source = null`。`Overlay` / `SetSurfaceVisible` は今のまま。`RenderOptions.SetBitmapInterpolationMode(Backdrop, HighQuality)` の行は削除（層の中で扱う）。
- [x] `BoardWindow.EnsureScaling()`: `RenderScaling` が変わって `Render` し直すときに `Backdrop.Refresh()` も呼ぶ（`SizeChanged` が起きない = 同じ論理サイズのまま拡大率だけ変わる場合の保険。`ScalingChanged` の購読と二重でも鍵が同じなら何もしない）。
- [x] `PopupController.Warmup` / `EndWarmup` は変更なし（macOS が 0×0 に縮めたときは `Refresh` が大きさ 1 px 未満で何もせず、`RestoreSize` 後の `SizeChanged` で作る。= 最初の作り置きは準備運動の中で済む）。Debug ログで「背景の作り置き」が準備運動の中で 1 回だけ出て、以後の Hide / Show では出ないことを見る。
- [x] テスト `BackgroundTests` の書き換えと追加（`issue/UI_TESTS.md`「常時の演出の CPU」）。既存の `ImageBrush` / `Backdrop.Opacity` を見るテストを `Fit` / `Source` / `ImageOpacity` / `BakedImage` に。
- [ ] 見た目の確認（Mac、Release `.app`。SPEC §13.3 C11 (10)）: 表示方法 5 種が作り置き前と同じに見える（2026-10-04 の確認時と見比べ。特に「並べる」「中央」の原寸と「収める」の余白）、ページの行列数が違うページへ切り替えても画像が歪まず瞬時に合う、Retina でぼけない・継ぎ目が無い、画像の不透明度スライダーに追従、グリッチに画像が写る（従来どおり）。等倍のモニタ（scale 1）は Mac 環境があれば。Windows は C11 (10)。

## ステップ 3b: 覆い `Overlay` の合成（任意。計測して決める）

- [x]（計測で差なし → 実施しない） ステップ 4 の計測で「全部オン・背景画像あり」を**覆い 40%（既定）と覆い 0** で比べる。差が 2% 以上なら: `Overlay` を要素 `Opacity` ではなく「`FlBoardBackground` の色にアルファ（覆い %）を掛けた不透明度 1 の `ImmutableSolidColorBrush`」にする（色は `this.GetResourceObservable("FlBoardBackground")` で配色の切替に追従。見た目は同じ。SPEC §3.7「覆い `Overlay` の合成」）。差が無ければ何もしない（SPEC の該当項に「差なし」と記録）。

## ステップ 4: 再計測と記録（メインの自動計測。Mac・拡大率 1 の画面（盤面のログ scale=1。Retina ではない。2026-10-07 に訂正）、Release `.app`、`top` の % CPU、盤面表示中・何もしない、表示 3 秒後から 8 秒）

ステップ 1〜3 の後、2026-10-07 と**同じ構成・同じ手順**で取り直し、`spec/EFFECTS.md`「CPU の計測と既定の判断」の 2026-10-07 の表に「30 fps + 作り置き後」の行（または列）を足す。SPEC §10.7 / §11 の要約も更新。

**2026-10-07 再計測（実装後、Mac・拡大率 1 の画面（盤面のログ scale=1。Retina ではない。2026-10-07 に訂正）、ソフトウェア描画、Release `.app`、Claude が自動で）**:

| 構成（背景画像は 1280 px 級の jpg、覆い 10%） | 60 fps（作り置きあり） | **30 fps（既定）+ 作り置き** | 作り置き前（2026-10-07 同日） |
|---|---|---|---|
| 全部オン（`orbTwin` + `pulseStrong` + 帯 + 背景画像） | 38.3%（最大 41.5%） | **30.1%（最大 31.7%）** | 60 fps 51.5% / 33 ms 35% |
| 全部オン・背景画像なし | — | **29.3%** | — |
| 光の玉だけ・背景画像あり | — | **24.0%** | 60 fps 45.6% |
| 明滅だけ・背景画像あり | — | **22.7%**（揺れ 29%） | 60 fps 30.6% |
| 帯だけ・背景画像あり（＝既定構成 + 背景画像） | — | **16.1%** | 60 fps 38.8% |
| 常時の演出すべてなし・背景画像なし | — | **0.5%** | 0.4% |
| 全部オン・覆い 0% / 40% | — | 30.2% / 30.6%（差なし → 覆いの合成の見直し（ステップ 3b）は不要） | — |
| 隠した後（どの構成でも） | 0.5% | 0.5〜0.6% | 0.4% |
| 表示まで n ms（4 回） | 17〜53 ms | 12〜79 ms（初回が最大） | 13〜45 ms |
| footprint 表示中 / 隠した後（全部オン・背景あり） | 118 / 123 MB | 118 / 110 MB | 115 / 116 MB |

背景画像の作り置きは起動後 1 回だけ（ログ「背景の作り置き」が各起動で 1 回、表示・非表示では出ない）。作り置き後は背景画像の有無で CPU がほぼ変わらない（29.3 vs 30.1%）。

- [x] 上の表を埋め、EFFECTS.md / SPEC §10.7 / §11 に転記。目で玉・帯・明滅が実際に動いていることを確認してから測る（2026-10-04 の計測の誤りの教訓）。
- [ ] 見た目: 30 fps で光の玉（8 s / 周）と帯（4 s）がカクついて見えないか、2 s / 周の玉のコマ送りが許容か（ユーザー確認。Mac 環境があれば）。
- [x] 計測用の一時コード（`MEASURE-TEMP`）を削除。`dotnet test FileLauncher.sln` が通る。
- [ ] Windows 側: ソフトウェア描画で全部オン 8.6%（§10.7）が 30 fps 化でさらに下がるはず。次の Windows 確認（`issue/MAC_SUPPORT.md` Mac-0 / SPEC §13.3 C17 (2) / C23 (5)）のときに同じ構成で記録する（目標外。記録のみ）。

## ステップ 5: 注記と既定の見直し（再計測の結果で。**ユーザー判断済み 2026-10-07 → タスクは `issue/AMBIENT_SETTINGS.md` へ**）

ユーザー判断（2026-10-07、30 fps の見た目と背景画像の確認 OK の後）: (1) 更新間隔を設定「VSync」（1〜3、1 = 60 fps、既定 2）に（上の「設定項目にしない」は撤回）、(2) 注記の数値を「15〜25% 程度」に、(3) `frameOrb` / `glowPulse` も既定オン（案 A 撤回）。実装タスク・テスト・再計測は **`issue/AMBIENT_SETTINGS.md`**。下の 2 項目は記録として残す。

- [x]（判断済み → AMBIENT_SETTINGS） 注記 `Settings_Appearance_Effects_AmbientNote`（日「…CPU を多く使います（20〜30% 程度）…」/ 英「…about 20–30%…」）の数値を、Mac の実測（単独オン・30 fps・背景画像なし〜あり）の範囲に合わせる。例: 単独オンが 10〜20% なら「10〜20% 程度」。変えるなら `Strings.resx` / `Strings.ja.resx` / `spec/TERMS.md` / SETTINGS.md「演出の調整…」の行 / SPEC §11 / EFFECTS.md の引用箇所を同時に直す。Windows はもともと 1 桁 % なので Mac に合わせたままでよい（2026-10-07 の方針どおり）。
- [x]（判断済み: **既定オンにする** → AMBIENT_SETTINGS） `frameOrb` / `glowPulse` の既定（2026-10-04 案 A = none）を見直すか。designer の推奨は据え置きだったが、ユーザー判断で既定オン。 EFFECTS.md の既定値・`EffectCatalog.Default`・`Normalize` の「既定と同じは保存しない」規則・既存テスト（「光の玉と明滅は既定で動かず…」）を直す。

## 完了条件

- ステップ 1〜4 が済み、再計測の値が EFFECTS.md / SPEC に記録され、`MEASURE-TEMP` が消え、テストが通っている（済み）。残りはステップ 3 / 4 の見た目のユーザー確認（**2026-10-07 に OK**）と Windows 側の記録のみ。ステップ 5 の判断は `issue/AMBIENT_SETTINGS.md` に移したので、**このファイルは AMBIENT_SETTINGS の着手時に削除してよい**（Windows 側の記録は SPEC §13.3 C11 (10) / C17 / C23 に残る）。済んだらこのファイルを削除する（Windows 側の記録は SPEC §13.3 C11 (10) / C17 / C23 に残る）。

## 実装時の注意

- **`BackdropLayer` の作り置きは `Refresh` の中だけで作る**。`Render` の中で `RenderTargetBitmap` を作らない（描画の記録中に別の描画を走らせない。`PulseLayer` と同じ流儀）。
- **要素の `Opacity` を 1 未満にしない**（`PulseLayer` の教訓: 上を玉が通るたびに層全体を合成し直す）。画像の不透明度は `PushOpacity`。
- 作り置きの鍵に `Source` は**参照**で入れる（`BoardBackgroundLoader` は同じファイルなら同じ `Bitmap` インスタンスを返すので、設定の保存のたびに `ApplyBackground` が呼ばれても作り直さない）。
- `tile` / `center` の「原寸」は `Bitmap.Size`（DPI 込みの論理 px）。従来の `ImageBrush` も `image.Dpi` で割っていたので同じ。
- `Compute` は Core に置き Avalonia の型を使わない（OS 非依存の数式。`OrbPath` / `AmbientMath` と同じ扱い）。
- 30 fps の間引きは**フレーム時刻**で判定し、`Stopwatch` は位置計算だけに使う（`Stop` / `Start` で止まる時計を間引きの基準にすると再開直後の判定がずれる）。
- `MinFrameMs` を 33 にしない（理由は EFFECTS.md「時計」）。
- 計測は Release `.app`（`src/build/publish-mac.sh`）で、玉・帯が実際に描かれているのを目で確認してから。隠した後の値は `MemoryTrim` の 5 秒後以降。
