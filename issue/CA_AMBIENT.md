# 常時の演出を macOS では Core Animation の独立レイヤーで描く（案 E）

設計日: 2026-10-09（designer）。**ユーザー判断（2026-10-09）: `issue/AMBIENT_RENDER_OPT.md` の将来候補 E「Mac の常時の演出を Core Animation（CA）の独立レイヤーへ」を採用**。経緯・計測は `spec/EFFECTS.md`「2026-10-08 追加検証」(1)〜(4) と `issue/AMBIENT_RENDER_OPT.md`。試作 `src/FileLauncher.Platform.MacOS/CaOrbSpike.cs`（MEASURE-TEMP）と `BoardWindow.axaml.cs` の `CPFL_CA_ORB=1` 分岐は、この issue の形に正式化して削除する。

正となる文書: **SPEC §10.3**（`IAmbientLayerService` の行と説明）、**§10.7**（2026-10-09 の項）、§11（常時の演出の CPU の macOS の扱い）、§12（E を採用に）、**§13.3 C30**、**`spec/EFFECTS.md`**（冒頭、「常時の演出の詳細」の「描き方（OS 別）」「時計」の行、各演出の表、「CPU の計測と既定の判断」の 2026-10-09 の項、「方式の比較」D）、`spec/SETTINGS.md` 演出タブ（VSync・注記の macOS の扱い）、`spec/TERMS.md`（`_Mac` の 2 文言）、`issue/UI_TESTS.md`「常時の演出の Core Animation 化」、`issue/MAC_SUPPORT.md` 記録欄 C30。

---

## なぜ（要約）

- Avalonia 11.3 の macOS ソフトウェア描画は前のフレームを保持せず、玉 1 個が 6 px 動くだけで**毎フレーム窓全体を描き直して IOSurface に全体コピー**する（EFFECTS.md 追加検証 (1)）。A + B（作り置き・帯の分割・部分塗り直し）で描画側は減らしたが、転送とクリアは残り、既定構成で 22.8%。アプリ側では変えられない。
- CA のレイヤーは **OS（WindowServer）が合成**し、位置・不透明度のアニメーションは render server が進める。アプリは毎フレーム何もしない。試作（2026-10-09）: 光の玉 1 個で **Avalonia 13.0〜13.9% → CA 0.7〜2.4%**（演出なし 0.6〜3.4% と同じ水準）。見た目は同じとユーザー確認済み。
- Windows はソフトウェア描画で全部オン 8.6%（SPEC §10.7）なので対象外。**この変更は macOS だけ**で、Windows は従来の Avalonia の層のまま。

## 決めたこと（要約）

| 項目 | 決定 |
|---|---|
| 範囲 | **光の玉（`orb` / `orbTwin`）・発光の明滅（`glowPulse`）・走査線の帯（`scanBeam`）の 3 つとも CA に移す**。1 つでも Avalonia に残すと窓全体の描き直しが毎フレーム残り、E の効果が「その 1 つの分」で頭打ちになる（明滅弱だけで 10.4%）。**段階分け**: ステップ 2 = 玉（試作あり）→ ステップ 3 = 明滅（画像の受け渡し）→ ステップ 4 = 帯（八角形のマスク）。各ステップの終わりで CPU を測り、残っている Avalonia の層の分が見込みどおり消えていくことを確かめる |
| 置き場所 | 新しい Platform IF **`IAmbientLayerService`**（`FileLauncher.Platform/Interfaces.cs`。`IPlatformServices.Ambient`、**既定実装 = 非対応**）と、取り付けた窓 1 つぶんの **`IAmbientLayerHost`**。macOS 実装 `Platform.MacOS/MacAmbientLayerService.cs` / `MacAmbientLayerHost.cs`、ネイティブは `Platform.MacOS/Native/CoreAnimation.cs`（新規）+ `CoreGraphics.cs` / `ObjC.cs` / `CoreFoundation.cs` の追加。Windows / `FakePlatform` は既定実装のまま（コード変更なし）。`IWindowService` は拡張しない（窓の管理と演出の描画は別の責務。寿命も違う） |
| App 側 | **`Effects/NativeAmbient.cs`（新規、App）** が `BoardWindow` の状態（表示・演出中・収納・設定・大きさ・拡大率・配色・不透明度）をホストの呼び出しに写す。`BoardWindow.UpdateAmbient` は「ホストがあれば `SetRunning(run)`、無ければ従来の Avalonia の層」の 2 分岐。**動く条件・契機（`UpdateAmbient` / `DuringTransition` / `AmbientSuspended` / `ReducedMotion` / `ApplyEffects`）は 1 か所のまま**で、描き手だけが変わる。ホストがあるとき `AmbientAnimator` は始めず、`_ambientRoot` は `IsVisible = false` のまま（Avalonia 側は一切描かない。VSync の間引きも動かない） |
| Core | OS 非依存の数式・定数を Core に寄せ、**Avalonia の層と CA の層が同じ値を参照する**（見た目の一致をコード共有で保証）: `Effects/AmbientLook.cs`（玉・尾・帯・明滅の見た目の定数と導出）、`Effects/AmbientLayout.cs`（枠線の中心線の八角形・周長・CA の `timeOffset`・明滅のキーフレーム・帯の移動量・各層の矩形）。単体テスト `FileLauncher.Tests/AmbientLayerCoreTests.cs` |
| 座標系 | App → Platform は **窓の論理座標（左上原点、pt）**で渡す。上下の反転は Platform の中だけ: コンテナ層に `geometryFlipped`（contentView が `isFlipped` でなければ YES）を立て、子の座標を上下反転なしで置く。Core に反転の式は持たない（`Octagon` の座標式を Core 以外に書かない規則は守る: CGPath は Core `Octagon.Points` / `AmbientLayout` の点列をなぞるだけ） |
| 一時停止 / 再開 | 止めるときは**即座に非表示**（`hidden = YES`）+ **時間を止める**（コンテナ層 `speed = 0`、Apple QA1673 の方式）。再開は `hidden = NO` + 時間を進め直す + **250 ms のフェードイン**（コンテナの `opacity` 0 → 設定の不透明度）。玉・明滅・帯は**止まった位置から続く**（Avalonia の `Stopwatch` を止める今の挙動と同じ） |
| 位相の保持 | すべてのアニメーションは `beginTime = コンテナ層のローカル時刻での開始時刻（最初の再開時に 1 回決める）`。大きさ・拡大率・配色の変化で経路・画像を作り直しても `beginTime` を同じにするので**玉が始点に飛ばない**（ページの行列数が違うページへの切替で位置が連続する）。周期を変えたときは位相が変わってよい（Avalonia 版も同じ） |
| 不透明度 | CA の層は Avalonia の Visual ツリーの外なので **`Window.Opacity` が掛からない**。`BoardWindow.ApplyOpacity` がホストの `SetOpacity` にも同じ値を渡す（コンテナ層の `opacity`）。グリッチのフェード（`Root.Opacity`）・表示 / 非表示の `Frame` の演出は、その間 CA の層を非表示にしているので関係しない（今の規則「演出中は止める」で足りる） |
| Z 順 | CA の層は contentView の層の上 = **Avalonia が描くすべてのものの上**（`GlitchLayer` の静止画・`DragLayer` のゴースト・ホバーの発光・ツールチップの中身を含む）。グリッチ中は止めているので静止画との二重は無い。**盤面内 D&D のゴーストの上を帯・玉が通る**のは Avalonia 版（ゴーストが上）と違うが**許容（2026-10-09 ユーザー判断。C30 で実物を見る。気になれば `DragLayer` の表示中だけ `SetRunning(false)` にする）**。右クリックメニュー・ツールチップ・トースト・編集ダイアログは別ウィンドウなので従来どおり上。CA の層の中の順は Avalonia と同じ **明滅 → 帯 → 玉** |
| VSync（`appearance.vsync`）と CPU の注記（**2026-10-09 ユーザー判断で確定**） | CA では OS が表示のリフレッシュに合わせて進めるので**使わない**（設定値は読まない）。設定項目・JSON キーは残す（Windows で使う。エクスポート / インポートの互換）。**設定画面では `Platform.Ambient.IsSupported` が true のとき VSync の行（ComboBox + 注記）と CPU の注記 `Settings_Effects_AmbientNote` の項目自体を出さない**（macOS 用の文言 `_Mac` は作らない）。**Windows の注記は「光の玉・発光の明滅・走査線の帯は、盤面が見えている間ずっと動き、CPU を多く使います。」だけ**（英 "Light orb, glow pulse, and scan beam run the whole time the board is visible and use a lot of CPU."。resx からの変更点は TERMS.md）。視差効果の注記 `Settings_Effects_ReducedMotionNote` は両 OS で従来どおり |
| 設定・JSON | 新しい設定項目・JSON の変更は **無し**。既定値も変えない |
| 画像の受け渡し | 明滅の画像は Avalonia の `PulseLayer` が焼いた `RenderTargetBitmap` を **PNG にエンコード（`Bitmap.Save(Stream)`）して `byte[]` で渡す**。Platform は ImageIO（`CGImageSourceCreateWithData` → `CGImageSourceCreateImageAtIndex`）で `CGImage` にして層の `contents` に入れる。Avalonia の `Bitmap.Save` はこのプロジェクトのテストで実績があり、画素形式（乗算済み / BGRA / stride）の取り決めが要らない。焼き直しは大きさ・拡大率・配色の変化のときだけなので、エンコード + デコード（数十 ms）は許容（ログ「明滅の画像を渡す: W×H px, n ms」で確かめる。30 ms を超えるなら `Bitmap.CopyPixels` → `CGDataProviderCreateWithData` + `CGImageCreate(... kCGBitmapByteOrder32Little | kCGImageAlphaPremultipliedFirst)` に差し替える。`CoreGraphics.BitmapInfoBgraPremultiplied` が既にある） |
| 計測の切替 | 計測・切り分け用に起動引数 **`--ambient avalonia`**（`--lang en` と同じ流儀。`Program` が読み、`App` が `BoardWindow.AmbientLayers = null` にする = Avalonia の層で描く）。設定項目にはしない。環境変数 `CPFL_*` は使わない |
| 失敗時 | `Attach` が null（非対応・ハンドル無し・例外）なら従来の Avalonia の層で描く（ログ 1 行）。取り付け後の失敗は想定しない（`objc_msgSend` は失敗を返さない。セレクタの誤りはクラッシュするので、`RespondsTo` で守るのは OS の版で有無が変わるものだけ） |

---

## 設計

### 1. Platform インターフェース（`FileLauncher.Platform/Interfaces.cs` / `PlatformTypes.cs`）

```csharp
/// <summary>
/// 常時の演出（光の玉・発光の明滅・走査線の帯）を OS のアニメーション層で描く（SPEC §10.3 / §10.7、spec/EFFECTS.md「描き方（OS 別）」）。
/// macOS = Core Animation。他 OS は IsSupported = false で、App は Avalonia の層で描く。すべて UI スレッドから呼ぶ。
/// </summary>
public interface IAmbientLayerService
{
    bool IsSupported { get; }

    /// <summary>盤面ウィンドウに取り付ける。非対応・失敗なら null（App は Avalonia の層に戻る）。</summary>
    IAmbientLayerHost? Attach(nint windowHandle);
}

/// <summary>取り付けた窓 1 つぶんの層。Dispose で層を外す。</summary>
public interface IAmbientLayerHost : IDisposable
{
    /// <summary>枠の位置・大きさ・面取り・余白・拡大率。変わるたびに呼ぶ（経路・マスク・画像の枠を作り直す。位相は保つ）。</summary>
    void SetGeometry(AmbientGeometry geometry);

    /// <summary>光の玉。null = なし。</summary>
    void SetOrbs(AmbientOrbSpec? orbs);

    /// <summary>発光の明滅。null = なし。画像は枠 + 余白の大きさ（Geometry.GlowMargin）。</summary>
    void SetPulse(AmbientPulseSpec? pulse);

    /// <summary>走査線の帯。null = なし。</summary>
    void SetBeam(AmbientBeamSpec? beam);

    /// <summary>盤面の不透明度（appearance.opacity）。Window.Opacity は層に掛からないので別に渡す。</summary>
    void SetOpacity(double opacity);

    /// <summary>true: 表示して 250 ms でフェードインし、止めた位置から再開。false: 即座に非表示にして時間を止める。</summary>
    void SetRunning(bool running);
}

/// <summary>窓の論理座標（左上原点、pt）。Frame = 枠の外形（FrameChrome.Frame の Bounds を窓座標へ移したもの）。</summary>
public sealed record AmbientGeometry(
    double WindowWidth, double WindowHeight,
    double FrameX, double FrameY, double FrameWidth, double FrameHeight,
    double Chamfer, double BorderThickness, double GlowMargin, double Scale);

/// <summary>Count = 1（主色）/ 2（主色 + 副色が対角）。PeriodMs = 1 周。</summary>
public sealed record AmbientOrbSpec(int Count, int PeriodMs, RgbColor Primary, RgbColor Secondary);

/// <summary>Peak = 最大の明るさ（弱 0.45 / 強 0.9）。Image = 焼いた発光（PNG、枠 + 余白、デバイス px）。</summary>
public sealed record AmbientPulseSpec(int PeriodMs, double Peak, EncodedImage Image);

/// <summary>帯の高さ・グラデーションは Core AmbientLook の定数（App と同じ値）。</summary>
public sealed record AmbientBeamSpec(int PeriodMs, RgbColor Color);

/// <summary>PNG にエンコードした画像。論理サイズ = PixelWidth / Scale（Geometry.Scale）。</summary>
public sealed record EncodedImage(byte[] Png, int PixelWidth, int PixelHeight);
```

- `IPlatformServices` に **`IAmbientLayerService Ambient => AmbientLayers.Unsupported;`**（既定インターフェース実装）を足す。`AmbientLayers.Unsupported`（`PlatformTypes.cs` か新ファイル `AmbientLayers.cs`）は `IsSupported = false`・`Attach` が null を返す単一インスタンス。**Windows（`WindowsPlatformServices`）と `FakePlatform` は変更不要**。`MacPlatformServices` だけ `Ambient => new MacAmbientLayerService()` を返す。
- `RgbColor` は Core `Theming` の既存型（Platform は Core を参照済み）。文言は返さない（§10.3 の規則どおり）。
- 契約: すべて **UI スレッド（AppKit のメインスレッド）から**。`SetGeometry` → `SetOrbs` / `SetPulse` / `SetBeam` → `SetOpacity` → `SetRunning(true)` の順が最初の 1 回の想定だが、**どの順で呼ばれても最後の値が効く**（ホストが最新の値を持ち、足りないもの（Geometry 未設定）は何も描かない）。`SetRunning(false)` の間に変えた値は次の `SetRunning(true)` でまとめて反映する（止まっている間に層を組み直さない）。

### 2. Core（`FileLauncher.Core/Effects/`）

**`AmbientLook.cs`（見た目の定数。App の `OrbSprite` / `ScanBeamLayer` / `AmbientAnimator` もここを参照するように直す）**

| メンバー | 値 | 今の置き場所（移す） |
|---|---|---|
| `OrbCoreRadius` 2.5 / `OrbHaloRadius` 9 / `OrbTailPoints` 10 / `OrbTailLength` 48 / `OrbTailStartRadius` 7 / `OrbTailEndRadius` 3 / `OrbTailStartOpacity` 0.6 / `OrbHaloAlpha` 230/255 | 同左 | `OrbSprite` の定数 |
| `OrbCoreColor(RgbColor accent)` → `RgbColor` | 主色を白と 60% 混ぜる（`v + (255 − v) × 0.6`） | `OrbSprite.SetColor` の `Mix` |
| `OrbTailSteps()` → `(double Radius, double Opacity, double LagPx)[]` | i = 1..10: `t = i / 10`、半径 `7 + (3 − 7) t`、不透明度 `0.6 (1 − t)`、遅れ `48 i / 10`。**配列の順は尾の先端（i = 10）が先** = 描く順（遠い尾から） | `OrbSprite.Render` / `OrbLayer.Place` |
| `PulseWeakPeak` 0.45 / `PulseStrongPeak` 0.9 / `MaxPulseGain` 1.9 | 同左 | `AmbientAnimator` の定数（`MaxPulseGain` は `NeonGlowPlan` が使う） |
| `PulsePeak(EffectKind kind)` | `Pulse` → 0.45、`PulseStrong` → 0.9、他 0 | `AmbientAnimator.Tick` の switch |
| `PulseKeyframes(double peak, int samples)` → `double[]` | `i = 0..samples−1`、`t = i / (samples − 1)`、値 = `AmbientMath.PulseLevel(t × T, T, peak)`（T は任意、位相だけ）。最初と最後が 0、中央が peak | 新規（CA のキーフレーム用。32 + 1 点で十分） |
| `BeamBandHeight` 60 / `BeamStops` = `[(0, 0), (0.85, 28), (1, 60)]`（位置, α） | 同左 | `ScanBeamLayer.BandHeight` / `UpdateColor` の 3 点 |
| `AmbientFadeInMs` 250 | 同左 | `BoardWindow.UpdateAmbient` の `DoubleTransition` |

**`AmbientLayout.cs`（配置の数式。純粋関数）**

| メンバー | 内容 |
|---|---|
| `OrbInset(double chamfer, double borderThickness)` | `chamfer > 0 ? t × 1.5 : t / 2`（`OrbLayer.PathFor` の規則をここへ。`OrbLayer` もこれを使う） |
| `OrbCenterline(double frameWidth, double frameHeight, double chamfer, double borderThickness)` → `(double X, double Y)[]` | `inset = OrbInset(...)`、`Octagon.Points(inset, inset, w − 2 inset, h − 2 inset, chamfer)`（枠の左上を原点とする 8 点。`OrbPath.Octagon(w − 2 inset, h − 2 inset, chamfer)` と同じ形で、`OrbLayer` の `Offset` と同じ平行移動） |
| `Perimeter((double X, double Y)[] points)` | 閉じた折れ線の長さ。`OrbPath.Octagon(...).Length` と一致することをテスト |
| `OrbTimeOffsetMs(int periodMs, double phaseOffset, double lagPx, double perimeter)` | CA の `timeOffset`（ms）: `((phaseOffset × T) − T × lagPx / perimeter) mod T` を `[0, T)` に正規化。頭 = `(T, 0, 0, p)` → 0、2 個目 = `phaseOffset 0.5` → T/2、尾 = `lagPx` ぶん後ろ。`perimeter <= 0` なら 0 |
| `PulseLayerRect(AmbientGeometry g, int pixelWidth, int pixelHeight)` → `(X, Y, W, H)` | `(FrameX − GlowMargin, FrameY − GlowMargin, pixelWidth / Scale, pixelHeight / Scale)`。**画像のピクセル数を正とする**（`ceil` の端数で Bounds + 2m より僅かに大きい。`PulseLayer.LayoutStrips` と同じ考え方） |
| `FaceRect(AmbientGeometry g)` → `(X, Y, W, H)` | `(FrameX + t, FrameY + t, FrameWidth − 2t, FrameHeight − 2t)`（`FrameChrome.FaceClip` と同じ。面取りは `Chamfer` そのまま） |
| `BeamCenterY(double phase, double faceHeight)` | `AmbientMath.SweepOffset(phase, faceHeight, BeamBandHeight) + BeamBandHeight / 2`（帯の中心。CA は `position` を動かすので中心で渡す）。`phase 0` → `−30`、`phase 1` → `faceHeight + 30` |

Platform.MacOS はこれらを呼ぶだけ（数式を書かない）。`OrbLayer` / `OrbSprite` / `ScanBeamLayer` / `AmbientAnimator` も同じ定数・関数を参照するように直す（挙動は変わらない。既存テストが通ることで確認）。

**単体テスト（`FileLauncher.Tests/AmbientLayerCoreTests.cs`）**: 
- `OrbInset`: 面取りあり 1.5 → 2.25、なし → 0.75。
- `OrbCenterline`: 最初の点が `(inset + chamfer, inset)`、8 点、`Perimeter` が `OrbPath.Octagon(w − 2 inset, h − 2 inset, c).Length` と ±1e-9。
- `OrbTimeOffsetMs`: `(8000, 0, 0, p)` = 0 / `(8000, 0.5, 0, p)` = 4000 / `(8000, 0, p/4, p)` = 6000 / `(8000, 0, p, p)` = 0 / `(8000, 0.5, p/4, p)` = 2000 / 常に `[0, T)`。
- `PulseKeyframes(0.45, 33)`: 長さ 33、`[0] == 0`、`[32] ≈ 0`、`[16] ≈ 0.45`、`[8] ≈ [24]`、0 → 16 で単調増加。
- `BeamCenterY(0, 330)` = −30、`BeamCenterY(1, 330)` = 360、`(0.5, 330)` = 165。
- `OrbTailSteps`: 10 個、先頭が `(3, 0, 48)`、末尾が `(6.6, 0.54, 4.8)`。`OrbCoreColor((0, 255, 255))` = `(153, 255, 255)`。
- `FaceRect` / `PulseLayerRect` の四則。

### 3. macOS 実装（`FileLauncher.Platform.MacOS/`）

**`MacAmbientLayerService : IAmbientLayerService`**: `IsSupported = true`。`Attach(handle)`: `NSWindowOf(handle)`（`MacWindowService` の private を `internal static` にして共用）→ `contentView` → `layer`（無ければ `setWantsLayer: YES` してから。Avalonia の `AvnView` は IOSurface を層の `contents` にしているので通常は既に層付き）→ `new MacAmbientLayerHost(view, rootLayer)`。例外は捕まえて `Trace.WriteLine("[ambient] attach failed: …")` して null。

**`MacAmbientLayerHost : IAmbientLayerHost`**（状態: 最新の `AmbientGeometry?` / 3 つの spec / `_opacity` / `_running` / `_startTime` / `_dirty` フラグ / 保持する層のハンドル）

層の構成（上が手前）:

```
contentView.layer（Avalonia のもの。触らない）
└ _root: CALayer（コンテナ。frame = (0, 0, WindowWidth, WindowHeight)、geometryFlipped = !view.isFlipped、
  │        **zPosition = 1000**（2026-10-09 実装で追加。下の「Z 順の保険」）、
  │        hidden / opacity / speed / timeOffset / beginTime をここで操作。retain して保持）
  ├ _pulse: CALayer（contents = 焼いた発光の CGImage、contentsScale = Scale、frame = PulseLayerRect、
  │          magnificationFilter / minificationFilter = kCAFilterNearest、opacity の基準値 0、キーフレームで明滅）
  ├ _beamClip: CALayer（frame = FaceRect、mask = CAShapeLayer（八角形 Octagon.Points(0,0,W,H,Chamfer) の CGPath、fillColor 黒））
  │   └ _beamBand: CAGradientLayer（axial、startPoint (0.5, 0) → endPoint (0.5, 1)、colors = BeamStops の α を主色に、
  │                 locations = [0, 0.85, 1]、bounds = (0, 0, faceW, 60)、position.x = faceW / 2、position.y をアニメーション）
  └ _orbs: CALayer（frame = _root と同じ。玉 1 個につき 12 層: 尾 10（CAGradientLayer radial）→ 暈 1（radial）→ 芯 1（CALayer、
             cornerRadius 2.5、backgroundColor = OrbCoreColor）。2 個目は副色。各層に同じ CAKeyframeAnimation（keyPath position、
             path = 中心線の CGPath、calculationMode paced、repeatCount ∞、duration = PeriodMs / 1000、beginTime = _startTime、
             timeOffset = OrbTimeOffsetMs(...) / 1000））
```

- **組み立てはすべて `CATransaction begin` / `setDisableActions: YES` / `commit` の中**（枠を動かすときの暗黙アニメーションを出さない）。
- **Z 順の保険（2026-10-09 実装で追加）**: `_root.zPosition = 1000`。Avalonia は窓を表示するときに contentView の層へ自分の子（IOSurface の層）を**後から**足すので、`addSublayer:` の順だけに頼ると先に足した `_root` がその下に潜って見えない。`zPosition` は兄弟の中の描画順を決めるので、後から足された子より手前に出る（設計 §「Z 順」の「Avalonia が描くすべてのものの上」はこれで成り立つ）。
- **合成の頻度の上限（2026-10-09 実装で追加。ステップ 7 の計測 (1) で WindowServer が 43% 前後に増えたため）**: `CoreAnimation.KeyframeAnimation` / `BasicAnimation` が返すすべてのアニメーション（玉の `position`・明滅の `opacity`・帯の `position.y`・再開のフェードイン）に **`setPreferredFrameRateRange:`（`CAFrameRateRange { Minimum = 15, Maximum = 30, Preferred = 30 }`、`Native/CoreAnimation.FrameRate` 定数 = 30）** を付ける（`LimitFrameRate`）。macOS 12+ の API なので `RespondsTo("setPreferredFrameRateRange:")` が false なら何もしない。`CAFrameRateRange` は float 3 つの構造体で、arm64 では浮動小数レジスタで渡る（`ObjC.SendVoidFrameRate`。構造体を**返す**セレクタではないので stret の問題は無い）。上限なし / 30 / 20 fps の比較と「30 固定」の判断はステップ 7 の記録 (2)。**設定項目にはしない**（macOS で VSync の行を出さない判断は変えない。20 fps 化・VSync の流用は SPEC §12 の候補）。
- **`CALayer.speed` は float**: `setSpeed:` に double を渡すと arm64 では 0 として読まれ、時間が進まない（`SendVoidFloat`）。`timeOffset` / `beginTime` は `CFTimeInterval` = double のまま。
- **位相**: `_startTime` は最初の `SetRunning(true)` で `convertTime:fromLayer:`（`CACurrentMediaTime()` を `_root` のローカル時刻に）で決め、以後変えない。すべての繰り返しアニメーションは `beginTime = _startTime`（`_root` の時間空間）なので、作り直しても CA が「開始から経った時間」を同じに数える = 玉が飛ばない。
- **一時停止 / 再開**（QA1673）: 止める = `paused = convertTime(now)`、`_root.speed = 0`、`_root.timeOffset = paused`、`_root.hidden = YES`、フェードインのアニメーションがあれば `removeAnimationForKey:`。再開 = `paused = _root.timeOffset`、`speed = 1`、`timeOffset = 0`、`beginTime = 0`、`since = convertTime(now) − paused`、`_root.beginTime = since`、`hidden = NO`、`_dirty` なら組み直し、`opacity` に `CABasicAnimation`（0 → `_opacity`、0.25 s、`kCAMediaTimingFunctionLinear`、キー `fadeIn`）を足して基準値を `_opacity` に。
- **再取り付けの確認**: `SetRunning(true)` と `SetGeometry` で `_root.superlayer`（`Send(_root, Sel("superlayer"))`）が contentView の層でなければ `addSublayer:` し直す（Avalonia が層を作り直したとき用の保険。ログ 1 行）。
- **明滅**: `SetPulse` で PNG → `CFDataCreate` → `CGImageSourceCreateWithData` → `CGImageSourceCreateImageAtIndex(src, 0, 0)` → `_pulse.contents = image` → `CGImageRelease` / `CFRelease(src)` / `CFRelease(data)`（層が retain する）。アニメーションは `CAKeyframeAnimation(keyPath "opacity")`、`values = PulseKeyframes(peak, 33)` の `NSNumber` 配列、`calculationMode linear`、`duration = PeriodMs / 1000`、`repeatCount ∞`、`beginTime = _startTime`。**明滅の画像は上下対称（同じ面取りの八角形）なので `geometryFlipped` で内容が反転しても見た目は変わらない**（帯のグラデーションだけ向きを確認する。C30）。
- **帯**: `CABasicAnimation(keyPath "position.y")`、`fromValue = BeamCenterY(0, faceH)`、`toValue = BeamCenterY(1, faceH)`、線形、`duration`、`repeatCount ∞`、`beginTime = _startTime`。`geometryFlipped` のコンテナの中なので y は下向き（上から下へ流れる）。グラデーションの `startPoint` / `endPoint` は層の単位座標（コンテナが flipped なら (0,0) が左上）。**下端が明るい**ことを目で確かめる（逆なら `startPoint` / `endPoint` を入れ替える）。
- **玉**: 試作 `CaOrbSpike.Attach` の形をそのまま正式化（値は Core `AmbientLook` / 経路は `AmbientLayout.OrbCenterline` から。CGPath は `Points` を `CGPathMoveToPoint` / `CGPathAddLineToPoint` / `CGPathCloseSubpath` でなぞる。**Y の反転はしない**（コンテナが flipped））。
- **解放**: `[CALayer layer]` 等は autorelease。**保持する層（`_root`・`_pulse`・`_beamClip`・`_beamBand`・`_orbs`）は `retain` し、外すときに `removeFromSuperlayer` → `release`**。玉の 12 層・尾は `_orbs` に入れたら手放す（`_orbs` の `sublayers` を `removeAllAnimations` + 全部外すには `setSublayers: nil`）。`CGPath` / `CGColor` / `CGImage` / `CFData` は渡した直後に `CGPathRelease` / `CGColorRelease` / `CGImageRelease` / `CFRelease`（アニメーション・層が自分で retain する）。`NSArray arrayWithObjects:count:` / `NSNumber numberWithDouble:` は autorelease（pool の中で使い切る）。
- **スレッド・pool**: 公開メソッドの先頭で `using var _ = AutoreleasePool.Push();`。`NSThread isMainThread`（クラスメソッド、BOOL）が false なら `InvalidOperationException`（App の呼び出し規則違反。Platform から `Dispatcher` は見えないので OS に訊く）。
- **構造体を返すセレクタは使わない**: 窓・ビューの `frame` / `bounds` を読まない（`AmbientGeometry.WindowWidth / Height` を App が渡すのはそのため）。`convertTime:fromLayer:` は `double` を返すので可。構造体を**引数**に取るセレクタ（`setFrame:` / `setBounds:` / `setPosition:` / `setStartPoint:`）は arm64 で問題ない（試作で確認済み）。
- **`Native/CoreAnimation.cs`（新規）**: `QuartzCore` の `NativeLibrary.Load` と `CACurrentMediaTime()` の DllImport、`CALayers` ヘルパー（`Make(className)`、`SetFrame(layer, x, y, w, h)`、`SetPosition`、`SetBounds`、`SetOpacity(float)`、`SetHidden(bool)`、`SetDouble(layer, sel, value)`、`AddSublayer`、`RemoveFromSuperlayer`、`Retain` / `Release`、`Transaction.Begin/Commit(disableActions)`、`KeyframeAnimation(keyPath)`、`BasicAnimation(keyPath)`、`NSArray(nint[])`、`NSNumber(double)`、`TimingFunction(name)`）。`objc_msgSend` の新しいオーバーロードは **`ObjC.cs`** に足す（`SendRect(nint, nint, CGRect)` は既存の `ref CGRect` 版と名前が被るので `SendVoidRect` / `SendVoidPoint` / `SendVoidDouble` / `SendVoidFloat` / `SendVoidBool` / `SendRetDouble(nint, nint, double, nint)` / `SendArray(nint, nint, nint[], nint)` / `SendRetNintDouble(nint, nint, double)`（`numberWithDouble:`））。**`CoreGraphics.cs`** に `CGPathCreateMutable` / `CGPathMoveToPoint` / `CGPathAddLineToPoint` / `CGPathCloseSubpath` / `CGPathRelease` / `CGColorCreateSRGB` / `CGColorRelease`、**`ImageIO`** に `CGImageSourceCreateWithData` / `CGImageSourceCreateImageAtIndex`、**`CoreFoundation.cs`** に `CFDataCreate`。

### 4. App（`FileLauncher.Desktop`）

**`Effects/NativeAmbient.cs`（新規、`internal sealed`）**

```csharp
internal sealed class NativeAmbient : IDisposable
{
    public NativeAmbient(BoardWindow window, IAmbientLayerService service, PulseLayer pulse) { … }
    public bool IsAttached => _host is not null;
    /// <summary>取り付ける（1 回だけ）。非対応・失敗なら false（呼び出し側は Avalonia の層で描く）。</summary>
    public bool TryAttach();
    /// <summary>ApplyEffects から。解決済みの 3 つの spec と配色（FlAccent / FlAccent2）を写す。</summary>
    public void Apply(EffectSpec orb, EffectSpec pulse, EffectSpec beam);
    public void SetRunning(bool running);
    public void SetOpacity(double opacity);
    /// <summary>テスト用: 最後に渡した Geometry / spec。</summary>
}
```

- `TryAttach`: `service.IsSupported` → `window.TryGetPlatformHandle()?.Handle` → `service.Attach`。成功したら購読: `Chrome.Frame.SizeChanged` と `window.SizeChanged` → `UpdateGeometry()`、`window.ScalingChanged` → `UpdateGeometry()`、`window.ActualThemeVariantChanged` → `Dispatcher.UIThread.Post(() => { Apply(最後の spec); }, Loaded)`（配色の切替は `AppTheme.SetColors` でテーマの入れ直しとして来る = `PulseLayer` と同じ契機）、`window.Closed` → `Dispose()`。ログ「常時の演出: Core Animation」。
- `UpdateGeometry()`: `Chrome.Frame.Bounds` の辺が 1 未満なら何もしない（次の `SizeChanged` で）。`origin = Chrome.Frame.TranslatePoint(default, window)`、`geometry = new AmbientGeometry(ClientSize.Width, ClientSize.Height, origin.X, origin.Y, Frame.Bounds.Width, Frame.Bounds.Height, Chrome.Chamfer, FlBoardBorderThickness.Left, FlGlowMargin, RenderScaling)` → `host.SetGeometry`。明滅が有効なら `PushPulse()`。
- `PushPulse()`: `_pulse.Refresh(Chrome.Frame.Bounds.Size)`（**`PulseLayer.Refresh(Size)` のオーバーロードを足す**。`Refresh()` は `Refresh(Bounds.Size)` に。レイアウトされていない層でも `TryFindResource` / `RenderScaling` は使える）→ `BakedImage` の参照が前回と同じなら何もしない → `Save` して PNG → `host.SetPulse(new(spec.DurationMs, AmbientLook.PulsePeak(spec.Kind), new EncodedImage(png, px.Width, px.Height)))`。ログ「明滅の画像を渡す: W×H px, n ms」。
- `Apply`: `orb.IsActive ? new AmbientOrbSpec(kind == OrbTwin ? 2 : 1, DurationMs, accent, accent2) : null` → `SetOrbs`。明滅は `PushPulse()`（なしなら `SetPulse(null)`）。帯は `beam.IsActive ? new AmbientBeamSpec(DurationMs, accent) : null`。色は `window.TryFindResource("FlAccent" / "FlAccent2")` の `ISolidColorBrush.Color` を `RgbColor` に。
- `SetRunning(true)` の前に Geometry 未送信なら `UpdateGeometry()`（Frame が未レイアウトなら `Dispatcher.UIThread.Post(UpdateGeometry, Loaded)`。`Show()` 直後の `IsVisible` 変化で呼ばれるので、今の `PulseLayer.Refresh` の Loaded 投稿と同じ理由）。

**`BoardWindow`**

- `internal IAmbientLayerService? AmbientLayers { get; set; }`（App が `_platform.Ambient` を入れる。`--ambient avalonia` なら null）。
- `ApplyEffects` の先頭で `_native ??= AmbientLayers is { IsSupported: true } ? new NativeAmbient(this, AmbientLayers, _pulseLayer) : null; if (_native is { IsAttached: false } && !_native.TryAttach()) _native = null;`。**ホストがあるとき**: `_native.Apply(orbSpec, pulseSpec, beamSpec)` し、Avalonia の層には `EffectSpec.None` を入れる（`_orbLayer.Spec` / `_beamLayer.Spec` / `_ambient.PulseSpec` = None、`_pulseLayer.IsVisible = false`）。**無いとき**: 今のまま。`any` の判定（`UpdateAmbient`）は解決済み spec を別に持って見る（`_ambientSpecs`）。
- `UpdateAmbient`: `run` の計算は今のまま。`if (_native is not null) { _native.SetRunning(run); return; }`（`_ambient` は始めない、`_ambientRoot` は `IsVisible = false` のまま）。それ以外は今のとおり。`UpdateClock()` の呼び出しは両方で。
- `ApplyOpacity(percent)`: `Opacity = …; _native?.SetOpacity(percent / 100.0);`。
- `AmbientRunning`（テスト用）は Avalonia の層が動いているか。`internal NativeAmbient? Native => _native;` を足す。
- `Closed` で `_native?.Dispose()`（`NativeAmbient` 自身も `Closed` を購読するので二重でも可）。
- 試作の `if (OperatingSystem.IsMacOS() && Environment.GetEnvironmentVariable("CPFL_CA_ORB") == "1") { … }` を削除。

**`App.axaml.cs`**: `_board.AmbientLayers = Program.AmbientRenderer == "avalonia" ? null : _platform.Ambient;` を `ApplyEffects` の前に。`Program` に `--ambient <avalonia|native>` の読み取り（`--lang` と同じ場所。既定 native）。

**`SettingsWindow`（演出タブ）**: `_ctx.Platform.Ambient.IsSupported` のとき (a) VSync の行（`ComboBox` + 注記 `Settings_Effects_Vsync_Note`）を作らない、(b) CPU の注記 `Settings_Effects_AmbientNote` の `TextBlock` を作らない（`SettingsWindow.cs` 724 行目付近。視差効果の注記はそのまま）。(c) 両 OS 共通で `Settings_Effects_AmbientNote` の文言を短くする（下のステップ 5）。`FakePlatform` は非対応なので既存の UI テストは従来の表示。

**`OrbLayer` / `OrbSprite` / `ScanBeamLayer` / `AmbientAnimator`**: 定数・数式を Core `AmbientLook` / `AmbientLayout` 参照に差し替える（挙動不変。Windows の見た目が変わらないことは既存テストで）。

### 5. 動く条件・ライフサイクルの対応表

| 出来事 | 今（Avalonia） | CA（ホストあり） |
|---|---|---|
| `Show()`（`IsVisible` true） | `UpdateAmbient` → `_ambientRoot` 表示 + 250 ms フェード + `Start` | `UpdateAmbient` → `SetRunning(true)`（Geometry 未送信なら Loaded で送ってから）。直後の `PlayShowAsync` で `_transitions++` → `SetRunning(false)` → 終わって `SetRunning(true)`（同じフレーム内の true → false は何も見えない） |
| `PlayHideAsync` / `PlayGlitchAsync` 開始 | 即座に非表示・`Stop` | `SetRunning(false)`（即座に `hidden`、時間を止める） |
| `Hide()` | `IsVisible` false → 止める | `SetRunning(false)`。窓が `orderOut` されるので層も見えない |
| 常駐の収納（`AmbientSuspended = true`） | 止める | `SetRunning(false)`（窓は 4 px 見えているが層は `hidden`） |
| 収納から戻る | グリッチ後に再開（続きから） | グリッチ後に `SetRunning(true)`（続きから + フェードイン） |
| `PrefersReducedMotion` / `appearance.animation` OFF / 3 つとも「なし」 | `run = false` | 同じ式で `SetRunning(false)` |
| 設定変更（種類・時間） | `ApplyEffects` → spec 差し替え | `Apply` → 変わった層だけ組み直し（位相は `beginTime` で継続。周期が変われば位相は変わってよい） |
| 配色の変更 | 毎フレーム色を引く / `PulseLayer.Refresh` | `ActualThemeVariantChanged` → `Apply`（色）+ `PushPulse`（焼き直した画像） |
| VSync の変更 | `AmbientAnimator.Vsync` | 何もしない（設定画面では行を出さない。2026-10-09 ユーザー判断） |
| 盤面の大きさ（行列数・ステータス行・ボタンサイズ） | `SizeChanged` → 経路 / 画像の作り直し | `Frame.SizeChanged` → `SetGeometry`（経路・マスク・枠を作り直し、位相は継続）+ `PushPulse` |
| 拡大率の変化（Retina ⇄ 外部モニタ） | `ScalingChanged` → 焼き直し | `ScalingChanged` → `SetGeometry`（`Scale` / `contentsScale`）+ `PushPulse` |
| 不透明度 | `Window.Opacity` | `Window.Opacity`（Avalonia 側）+ `SetOpacity`（CA 側） |
| 常駐の移動（`residentMove` / ドラッグ） | 窓と一緒に動く | 同じ（層は窓の中） |
| 盤面内 D&D のゴースト | ゴーストが上 | **帯・玉が上**（許容。C30 で見る） |
| アプリ終了 | — | `Closed` → `Dispose`（層を外す） |

### 6. テスト観点（`issue/UI_TESTS.md`「常時の演出の Core Animation 化」に転記済み）

- Core: 上の §2 の一覧。
- Desktop（ヘッドレス。`FakePlatform` に **`FakeAmbientLayers`**（`IsSupported` を切り替えられる・`Attach` が記録用 `FakeAmbientLayerHost` を返す・`Attach` を null にもできる）を足す）: ホストあり → `AmbientRunning == false` のまま `SetRunning(true/false)` が表示 / 非表示・収納・`ReducedMotion`・アニメーション OFF で届く / `ApplyEffects` の spec が届く（`orbTwin` → `Count 2`、`pulseStrong` → `Peak 0.9`、`none` → null）/ 明滅の `EncodedImage.PixelWidth == ceil((W + 80) × scale)` / `SetGeometry` の `FrameX == FlGlowMargin`、`Width` を変えると新しい値 / `ApplyOpacity(50)` → `SetOpacity(0.5)` / `Attach` が null なら Avalonia の層が動く（`AmbientRunning == true`）/ 設定画面で `IsSupported` のとき VSync の `ComboBox` と `Strings.Settings_Effects_AmbientNote` の `TextBlock` が無い（false のとき両方ある）。
- 実機（C30）: 下のステップ 7。

---

## 実装タスク

**2026-10-09 実装（メイン）**: ステップ 1〜6 を実装。テスト Core 258 / Desktop 124 が通る。実装で設計から変えた・足した点:
- コンテナ層に **`zPosition = 1000`**（Avalonia は表示時に contentView の層へ自分の子を後から足すので、先に足した `_root` がその下に潜って見えなかった）。
- `CALayer.speed` は **float**（double で渡すと arm64 では 0 として読まれ、時間が進まなかった）。`SendVoidFloat` で渡す。
- `NativeAmbient.TryAttach` はハンドルが取れなくても `Attach(0)` を呼ぶ（macOS 実装は 0 で null を返す。テストの偽物は取り付く）。
- Debug ビルドのログに Platform の `[ambient]` / `[finder]` の Trace も出す（`AppLog.ForwardingListener`）。
- **合成の頻度の上限**（`CAAnimation.preferredFrameRateRange`、既定 30 fps）を追加。下の計測で WindowServer が増えたため（ステップ 7）。

### ステップ 1: Platform IF と Core（OS 非依存。Windows でもビルド・テストが通る）

- [x] `FileLauncher.Platform/Interfaces.cs`: `IAmbientLayerService` / `IAmbientLayerHost`、`IPlatformServices.Ambient`（既定 `AmbientLayers.Unsupported`）。`PlatformTypes.cs`: `AmbientGeometry` / `AmbientOrbSpec` / `AmbientPulseSpec` / `AmbientBeamSpec` / `EncodedImage`、`AmbientLayers.Unsupported`。
- [x] Core `Effects/AmbientLook.cs` / `Effects/AmbientLayout.cs`（§2 の表どおり）。`AmbientAnimator.MaxPulseGain` の参照元（`NeonGlowPlan.Extents` / `NeonGlowTests`）を `AmbientLook.MaxPulseGain` に。
- [x] `OrbLayer.PathFor`（`AmbientLayout.OrbInset`）、`OrbSprite`（`AmbientLook` の定数・`OrbCoreColor`・`OrbTailSteps`）、`ScanBeamLayer`（`BeamBandHeight` / `BeamStops`）、`AmbientAnimator`（`PulsePeak`）を Core 参照に。既存テスト（Core 179 / Desktop 85 + α）が通る。
- [x] `FileLauncher.Tests/AmbientLayerCoreTests.cs`（§2 のテスト）。

### ステップ 2: macOS 実装 — 光の玉（試作の正式化）

- [x] `Native/ObjC.cs` のオーバーロード追加、`Native/CoreGraphics.cs`（CGPath / CGColor）、`Native/CoreAnimation.cs`（新規。§3）。
- [x] `MacAmbientLayerService` / `MacAmbientLayerHost`（§3。まず `SetGeometry` / `SetOrbs` / `SetOpacity` / `SetRunning` / `Dispose`。`SetPulse` / `SetBeam` は受け取って保持するだけ）。`MacPlatformServices.Ambient`。
- [x] App `Effects/NativeAmbient.cs`（§4。`PushPulse` は後のステップ）、`BoardWindow` の分岐、`App` / `Program` の `--ambient`。
- [x] 試作の削除: `Platform.MacOS/CaOrbSpike.cs` を削除、`BoardWindow` の `CPFL_CA_ORB` 分岐を削除。`grep -rn "MEASURE-TEMP\|CPFL_" src/` が 0 件。
- [ ] 動作: `dotnet run` で玉 1 個 / 2 個が枠線の中心線（Avalonia 版と同じ位置。試作は外形の上だった）を周回、Esc で即消え、再表示で続きから + 250 ms フェードイン、設定で周期・個数・配色を変えると即反映、行列数の違うページへ切替で経路が追従し玉が飛ばない、不透明度 50% で玉も薄い。**この時点の CPU**（Release `.app`、玉だけ）を測って記録。

### ステップ 3: macOS 実装 — 発光の明滅

- [x] `PulseLayer.Refresh(Size)` オーバーロード。`NativeAmbient.PushPulse`（PNG 化、ログ）。
- [x] `MacAmbientLayerHost.SetPulse`（ImageIO でデコード、`contents` / `contentsScale` / `Nearest`、opacity のキーフレーム）。`Native/CoreFoundation.cs` に `CFDataCreate`、`ImageIO` に 2 関数。
- [ ] 動作: 明滅「弱」/「強」で枠の発光が 4 秒周期で呼吸し、Avalonia 版（`--ambient avalonia`）とスクリーンショットで同じ明るさ・形（八角形、継ぎ目なし）。Retina でぼけない（`contentsScale`）。配色変更で色が追従（焼き直し → 画像差し替え）。ログの「明滅の画像を渡す」が Hide / Show では出ず、起動（準備運動）・配色・大きさの変化で出る。n ms が 30 を超えるなら `CopyPixels` 方式へ。

### ステップ 4: macOS 実装 — 走査線の帯

- [x] `MacAmbientLayerHost.SetBeam`（`_beamClip` + マスク + `CAGradientLayer` + `position.y` のアニメーション）。
- [ ] 動作: 帯が面の中だけ（八角形の角で切れる）、**下端が明るい**、上から下へ 4 秒で流れ続ける、スロット・タブ・ステータス行の上、Esc で即消える。盤面内 D&D 中にゴーストの上を帯が通るのを確認して許容かどうか記録。

### ステップ 5: 設定画面と文言（2026-10-09 ユーザー判断で確定）

- [x] `Settings_Effects_AmbientNote` の文言を短くする（両 resx。**変更点**: 日 = 「（15〜25% 程度）」と「種類を「なし」にすると止まり、「VSync」を大きくすると軽くなります。」を削除 → 「光の玉・発光の明滅・走査線の帯は、盤面が見えている間ずっと動き、CPU を多く使います。」/ 英 = " (about 15–25%)" と " Set the kind to None to stop them, or raise VSync to lighten the load." を削除 → "Light orb, glow pulse, and scan beam run the whole time the board is visible and use a lot of CPU."。TERMS.md）。新しいキーは足さない（`_Mac` は作らない）。
- [x] `SettingsWindow` 演出タブ: `_ctx.Platform.Ambient.IsSupported` が true なら VSync の行（ComboBox + `Settings_Effects_Vsync_Note`）と `Settings_Effects_AmbientNote` の `TextBlock` を作らない。視差効果の注記・「すべて既定に戻す」（`vsync` を 2 に戻す）は変えない。
- [x] UI テスト（`SettingsUiTests`）: `FakeAmbientLayers.IsSupported = true` で VSync の `ComboBox` と `Strings.Settings_Effects_AmbientNote` が無い / false で両方ある。既存の注記テストは新しい文言（`Strings.*` 参照なのでそのまま通る）。

### ステップ 6: テスト

- [x] `FakePlatform.Ambient`（`FakeAmbientLayers` / `FakeAmbientLayerHost`）、`AmbientEffectsTests` に §6 の Desktop 観点。`NoHardcodedJapaneseTests` が通る（Platform のログは `Trace.WriteLine` 英語、App のログは `AppLog` 日本語で対象外）。

### ステップ 7 の記録（2026-10-09、メインの自動計測。Release `.app`・背景画像 bg01.jpg・常駐・常に手前・20 秒 × 2。玉・帯・明滅が動いていることはスクリーンショットで確認）

**(1) 上限なし（表示のリフレッシュで合成）**: アプリの CPU は既定 1.2 / 1.4%（従来 22.8 / 24.1%）、全部オン 1.3 / 0.9%、玉だけ 1.2 / 1.8%、明滅だけ 2.1 / 2.8%、帯だけ 0.7 / 6.9%、すべてなし 1.9 / 1.2%。footprint 115〜125 MB（従来 133 MB）。**ただし WindowServer が 43% 前後**（すべてなし 18%、従来の既定 25〜29%）で、アプリ + WindowServer の合計は従来 約 50% → 約 44% と小さな差だった。従来は VSync 2 で 30 fps に間引いていたのに、CA は表示のリフレッシュ（60 Hz 以上）で合成していたため。

**(2) 合成の頻度の上限 `preferredFrameRateRange`（既定構成、交互に 2 巡）**:

| 構成 | アプリ | WindowServer | 合計の目安 | footprint |
|---|---|---|---|---|
| すべてなし | 0.5〜0.9% | 8.7〜13.3% | 約 11% | 117〜124 MB |
| CA 上限なし | 0.5〜0.9% | 29.8〜33.2% | 約 33% | 121〜125 MB |
| **CA 30 fps（採用）** | **0.6〜0.7%** | **21.5〜24.4%** | **約 24%** | 123〜126 MB |
| CA 20 fps | 0.5〜0.8% | 16.9〜18.2% | 約 19% | 125〜126 MB |
| 従来（`--ambient avalonia`、VSync 2） | 16.8〜17.7% | 20.2〜23.0% | 約 39% | 125〜131 MB |

→ **30 fps 固定を採用**（従来の既定と同じ滑らかさ。合計 約 39% → 約 24%、演出なしとの差は 約 28 → 約 13 ポイント）。20 fps ならさらに 5 ポイントほど軽い。WindowServer は他のアプリの画面更新で揺れるので、同じ巡の中の差で見ること。

### ステップ 7: 計測と記録（メインの自動計測。2026-10-08 と同じ手順: Mac・Release `.app`（`publish-mac.sh`）・背景画像 bg01.jpg あり・20 秒間の `ps cputime` 増分の 1 コア換算・各 2 回。玉・帯・明滅が動いているのをスクリーンショットで確認してから。WindowServer の % と footprint も）

- [x] 構成: 既定（玉 1・明滅弱・帯）/ 全部オン（`orbTwin` + `pulseStrong` + 帯）/ 玉だけ / 明滅だけ / 帯だけ / すべてなし（ホストは取り付いたまま）/ `--ambient avalonia` の既定構成（上の記録）。**残り**: 隠した後 / 常駐の収納中（アプリ 0.5% 以下、WindowServer が「すべてなし」と同じ水準 = `hidden` + `speed 0` で合成が止まっていること）。
  - 2026-10-09 一部計測: **ポップアップで隠れたまま（起動後に一度も出していない）**の既定構成 = アプリ 0.7 / 0.7%・WindowServer 11.8 / 10.2%・footprint 99 MB、すべてなし = 0.7 / 0.6%・13.3 / 10.0%・99 MB で差なし（ホストは取り付いて止まったまま）。**一度出してから隠した後・常駐の収納中**はトリガー操作が要るので未計測（ユーザー確認のとき C30 で）。
- [ ] 「表示まで n ms」4 回（取り付け・画像の受け渡しは準備運動で済むので変わらないはず）、footprint 隠した後 5 秒以降（表示中は記録済み。CA の画像は WindowServer 側にも持たれるので `footprint` に出ない分がある旨を記録）。
- [ ] Retina（scale 2）が使えれば既定構成を 1 回（C30 (7) と兼ねる）。
- [x] 結果を `spec/EFFECTS.md`「CPU の計測と既定の判断」に 2026-10-09 の表として足し、SPEC §10.7 / §11 / §12 / §13.3 C30 を更新（2026-10-09 designer）。アプリの CPU は見込みどおり（既定 1.2〜1.4% → 上限 30 fps で 0.6〜0.7%）。見込みとずれた点 = **WindowServer の増加**（上限なしで 43% 前後）→ `preferredFrameRateRange` 30 fps で対処（§3 の「合成の頻度の上限」）。

**2026-10-09 ユーザー確認「OKです」（ステップ 8 / C30）。** 残りはステップ 7 の未計測分（一度出してから隠した後・常駐の収納中・表示まで n ms・Retina）だけ。済んだらこのファイルを削除する。

### ステップ 8: Mac での見た目の確認（ユーザー。Release `.app`。SPEC §13.3 C30）

- [ ] 玉: 位置（枠線の中心線・斜辺に沿う）・大きさ・尾・色が Avalonia 版と同じ。`orbTwin` で対角。
- [ ] **滑らかさ（合成の上限 30 fps。C30 (1')）**: 玉・帯・明滅の動きが従来（`--ambient avalonia`、VSync 2 = 30 fps）と同程度で、コマ落ち・カクつきが目立たない（玉の最速 2 s / 周と帯で特に見る）。粗く見えるなら `CoreAnimation.FrameRate` を 60 にして合計の CPU（上限なし 約 33%）と見比べ、ユーザー判断。
- [ ] 明滅: 形・明るさが同じ、角で四角く光らない、Retina でぼけない。
- [ ] 帯: 面の中だけ、下端が明るい、上から下へ。
- [ ] 止まる / 続く: Esc・外クリック・常駐の収納・「アニメーション」OFF・OS の「視差効果を減らす」で止まり（グリッチの静止画に写らない）、戻ると**止まった位置から**フェードインで再開。
- [ ] 不透明度 50% で玉・帯・明滅が盤面と同じ薄さ（二重に薄くなっていない / 盤面より濃くない）。
- [ ] 行列数の違うページへ切替・ボタンサイズ変更・ステータス行 OFF で 3 つとも新しい枠に合い、玉が始点に飛ばない。
- [ ] 配色の変更（プリセット・カラーピッカー）で即追従。
- [ ] 右クリックメニュー・ツールチップ・トースト・編集ダイアログが帯・玉の上に出る。盤面内 D&D のゴーストの上を帯・玉が通る見え方（許容か）。
- [ ] 外部モニタ（scale 1）⇄ Retina を跨いで出し直すと大きさ・鮮明さがそのまま。
- [ ] 設定画面の演出タブ: VSync の行と CPU の注記が無い（視差効果の注記は OS 設定オンのとき出る）。
- [ ] （任意。旧 `issue/AMBIENT_RENDER_OPT.md` ステップ 5 の残り = C30 (7)）`--ambient avalonia` で起動して、枠の発光・明滅の帯の継ぎ目・部分塗り直しの乱れが 2026-10-08 以前と同じに見える。Retina（scale 2）の CPU を CA 既定と `--ambient avalonia` で 1 回ずつ（C27 の残りを兼ねる）。
- [ ] 隠した後・収納中の CPU 0.5% 以下、footprint が導入前と同程度。

### 完了条件

ステップ 1〜7 が済み（`MEASURE-TEMP` / `CPFL_` が無く、テストが Windows / macOS で通り、計測値が EFFECTS.md / SPEC §10.7 / §11 に記録され）、ステップ 8 をユーザーが Mac で確認して OK、C30 を `issue/MAC_SUPPORT.md` に記録。Windows 側は変更なし（C17 / C28 のまま）。済んだらこのファイルを削除する。`issue/AMBIENT_RENDER_OPT.md` は 2026-10-09 のユーザー判断で閉じた（残りの Mac の Retina 計測・見た目は C27 / C28 / C30 (7) に移した。ファイルの削除はメインが行う）。

---

## 実装時の注意

- **Core の `Octagon` の座標式を Platform に書かない**。CGPath は `AmbientLayout.OrbCenterline` / `Octagon.Points` の点列をなぞるだけ。帯の移動量・明滅の波形も Core の関数から。
- **Platform のネイティブ呼び出しは `Native/` 経由のみ**。`CaOrbSpike` の `DllImport` 直書きは `ObjC.cs` / `CoreGraphics.cs` / `CoreAnimation.cs` に移す。構造体を返すセレクタは使わない（窓の大きさは App から受け取る）。
- **保持する層は retain、手放すときは release**。autorelease されるものは pool の中で使い切る。`CGPath` / `CGColor` / `CGImage` / `CFData` は渡したら即解放（試作はすべて漏らしていた）。
- **`CATransaction` で暗黙アニメーションを切る**（`setDisableActions: YES`）。切らないと `frame` の変更が 0.25 s かけて動く。
- **`beginTime` の時間空間**: 子のアニメーションの `beginTime` は親（`_root`）のローカル時刻。`_startTime` は `convertTime:fromLayer:` で `_root` の時刻にしてから使う。`_root.speed = 0` の間はローカル時刻が止まるので、止まっている間に足したアニメーションも矛盾しない。
- **止まっている間は組み直さない**（`_dirty` を立てて次の `SetRunning(true)` で）。隠れている盤面の設定変更で WindowServer に仕事をさせない。
- **`contentsScale`** を画像の層に必ず入れる（入れないと Retina で 2 倍に拡大されてぼける）。玉・帯（ベクタ的な層）にも `Scale` を入れておく（害はない）。
- **App のログは `AppLog`（日本語）、Platform のログは `Trace.WriteLine("[ambient] …")`（英語）**（`FinderTabs` / `ExplorerTabs` と同じ流儀。Platform から `AppLog` は参照できない）。
- `UI 文字列は直書きしない`: 新しい注記は両 resx + TERMS.md。
- Windows のビルド・テストに Platform.MacOS の変更が影響しないこと（`[SupportedOSPlatform("macos")]`、既定インターフェース実装で Windows / Fake は無変更）。
- 計測は Release `.app` で、玉・帯・明滅が実際に動いているのを目で確認してから。隠した後のメモリは `MemoryTrim` の 5 秒後以降。

## ユーザー判断（2026-10-09、確定）

1. 設定画面の VSync の行: macOS（`Platform.Ambient.IsSupported`）では出さない。
2. CPU の注記: macOS では項目自体を出さない（`_Mac` 案は取りやめ）。Windows の注記は「CPU を多く使う」だけの表記（補足を外す。ステップ 5 / TERMS.md）。
3. 盤面内 D&D のゴーストの上を帯・玉が通る: 許容して C30 で実物を見る。
4. `issue/AMBIENT_RENDER_OPT.md`: 残りを C27 / C28 / C30 (7) に寄せて閉じる（削除）。

未決は無し。
