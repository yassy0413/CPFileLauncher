# CPFileLauncher 演出（アニメーション）一覧

盤面・トースト等の**演出（アニメーション）の正はこのファイル**。SPEC §3.1 には方針だけを置く。
`spec/SETTINGS.md` と同じく、ユーザーが手で編集する表。**「ユーザー記入欄」に「こうしたい」を書けば、実装側がそれを既定値（コード内 `EffectCatalog`）と settings.json の `appearance.effects` に反映する。**

最終更新: 2026-10-07（**走査線の帯 `scanBeam` を常時の演出として追加**（SPEC §3.9 H14「走査線の仕様」の採用。ユーザーが試し表示で選んだ「高さ 60 px の主色のグラデーションの帯が 4 秒で上から下へ流れ続ける」。種類 none / `beam`、時間 = 1 回流れ切る長さ 2000〜12000 ms・500 刻み。**既定 `beam / 4000`（同日ユーザー判断。2026-10-04 の案 A「常時の演出は既定オフ」の唯一の例外。CPU が光の玉並みでも既定オン）**。注記 `AmbientNote` を「…走査線の帯以外は既定ではオフです。」に。SPEC §11 の「表示中 3%」は常時の演出をすべて「なし」にした構成に適用し、既定構成の表示中は実測を記録するだけ。`issue/SCANLINES.md`）。層 `ScanBeamLayer` は `AmbientRoot` の `PulseLayer` と `OrbLayer` の間、`AmbientAnimator` で駆動、`Clip` は面の八角形、スロットの上。共通の注記 `AmbientNote` の文言に「走査線の帯」を加える。静止した走査線は演出ではなく HUD の見た目（SETTINGS.md `appearance.hud.scanlines`）。**2026-10-07 実装済み（Mac でユーザー確認待ち）**）。2026-10-06（**枠の発光を八角形（面取り）に沿わせ、余白を 34 → 40 に**（ユーザー報告「明滅「強」+ 光の玉で外側フレームの発光位置がおかしい」。SPEC §3.6「発光の作り方」/ §3.9 H8、`issue/CHAMFER_GLOW.md`）: `glowPulse` の `PulseLayer` は `FlBoardGlow` の矩形の `Border` ではなく、盤面の静的な発光と同じ `NeonGlowPlan`（八角形のリング描画。`Effect` を使わないので `RenderTargetBitmap` にそのまま焼ける）を焼く = 静的な発光と同じ形。`frameOrb` の `OrbPath` も八角形（玉が斜辺に沿う。それまでは角丸矩形で斜辺を横切っていた）。玉の暈がはみ出せる余白は 40。**設計のみ、未実装**。`issue/AMBIENT_EFFECTS.md` ステップ 4 の見た目の確認はこの修正後に行う）。2026-10-04（**設定画面・すべてのダイアログ（`ChromeWindow`）も `boardShow` / `boardHide` の glitch で出入りする**ように（ユーザー要望、実装済み・Mac で確認 OK。「対象外」から外し「glitch の詳細」13. を追加。新しい演出 ID は作らない。SPEC §3.8「演出」）。同日それ以前: **サイバーパンク専用化（SPEC §1.3）に伴い「テーマ別の既定値」を廃止**: 旧サイバーパンクの値（`boardShow` / `boardHide` = glitch、`itemHover` / `tabHighlight` / `dropTarget` = glow）が唯一の既定値になり、`EffectCatalog` の `CyberpunkDefault` と `ThemeMode` 引数を無くす。`glowPulse` の行は常に出す。実装済み（2026-10-04、Mac で確認済み）。同日それ以前: **常時の演出 2 つを実装し、CPU の計測を受けて `frameOrb` / `glowPulse` の既定を「なし」に**（ユーザー判断 案 A、確定。**どのテーマでも既定オフ**、ユーザーが「演出の調整…」でオンにする扱い。2 行の下の共通の注記 `Settings_Appearance_Effects_AmbientNote` を「光の玉と発光の明滅は、盤面が見えている間ずっと動き、CPU を多く使います（20〜30% 程度）。既定ではオフです。」に変更。明滅専用の注記は作らない）。計測（Mac `.app` Release、盤面表示中）: 演出なし 0.5〜1.5% / **光の玉 22〜28%** / **明滅 11〜18%** / **両方 25〜42%**（Metal 描画でも改善なし。macOS の Avalonia は一部が動くだけでも毎フレーム窓全体を描き直しているとみられる）。オンにしたときは SPEC §11 の CPU 目標の対象外。軽くする方法は SPEC §12。実装は設計から変更: `OrbLayer` は `Canvas` + 部品 `OrbSprite` を `RenderTransform` で動かす、`PulseLayer` は発光を `RenderTargetBitmap` に焼いて `Level` で明るさを変える。OS の「視差効果を減らす」に従う（`PrefersReducedMotion`）も採用。詳細は「常時の演出の詳細」の「CPU の計測と既定の判断」。同日それ以前: **常時の演出 2 つを追加**: `frameOrb`（枠を周回する光の玉）と `glowPulse`（枠の発光の柔らかい明滅）。ユーザー要望「外側フレームや、内側でも可能なところに、光の玉が移動しているアニメーションを入れたい。又、全般的なグローに柔らかい明滅を入れたい」。2026-10-03 の「常時動く演出は入れない」をこの 2 つに限って改める。サイバーパンクだけ既定 ON（設計時。同日の CPU 計測を受けたユーザー判断で 2 つとも既定なしに）、盤面が隠れている間・常駐の収納中は止める。詳細は「常時の演出の詳細」、実装は `issue/AMBIENT_EFFECTS.md`。2026-10-03: M4 ステップ 5: `pageSwitch` の実装状況ときっかけを実装に合わせて更新。同日それ以前: **常駐モードでもグリッチ**（切替・起動・終了・自動で隠す・前面化。SPEC §3.2、「glitch の詳細」の常駐の節）。M4 設計で `pageSwitch` のきっかけに Alt+数字・ドラッグ中のタブホバーを追加。同日それ以前: glitch に重ねるフェードを**グリッチの半分の時間**に（表示 = 前半でフェードイン、非表示 = 後半でフェードアウト）。同日それ以前: ユーザー要望で **glitch にフェードを重ね、非表示（`boardHide`）でも glitch を選べるように**。サイバーパンクの `boardHide` 既定を fade / 60 / easeIn → **glitch / 150 / —** に。同日それ以前: T2 のユーザー確認を受けて **`glitch` の既定を 150 ms・ブレ強めに確定**（影 ±5 px、帯 ±6〜16 px、最後の段 ±6 px）。同日それ以前: テーマ「サイバーパンク」の設計で種類 `glitch` / `glow` と「テーマ別の既定値」を追加。初版は M5 着手前の候補一覧で、値はすべて「控えめ」な仮の初期値。ユーザーが実際に動かして見てから書き換える前提）

## 方針（SPEC §3.1 と同じ内容。実装は App 層 `Effects/EffectRunner.cs`（EffectSpec → Avalonia Animation / Transitions）と Core `Effects/EffectCatalog.cs`（ID・既定値・選べる種類）。M5 で実装済み、2026-10-04 完了）

- **全体 ON/OFF**: `appearance.animation`（既定 ON）。OFF にすると下表の全演出が「なし」（時間 0）になる。
- **個別パラメータ**: 各演出は「種類 / 時間 ms / イージング」の 3 つで表す。既定値はコード内の `EffectCatalog`（`src/FileLauncher.Core/Effects/`）に持ち、**この表と一致させる**。ユーザーが変えた値だけ `settings.json` の `appearance.effects.<演出 ID>` に保存する（SETTINGS.md 表示タブ）。設定画面の表示タブ「演出の調整…」からも変えられる（M5 ステップ 2）。
- **表示遅延 100ms（SPEC §11）との関係**: 表示遅延は「トリガー → 盤面の**最初のフレームが描かれるまで**」で計り、表示演出の時間は含めない。表示演出は最初のフレームから始まり、**演出中も盤面はクリック・キー操作を受け付ける**（透明度・拡大の変化は当たり判定を妨げない）。表示演出の時間は 200ms 以下にする（スライダーの上限はそれより大きいが、注記で勧める）。
- **非表示演出**: 盤面を閉じる操作の瞬間に入力の受付をやめ、フォーカスの返却（SPEC §3.3）も**即座に**行う。ウィンドウの `Hide()` だけを演出の終わりまで遅らせる。非表示演出中にトリガーで再表示されたら演出を打ち切って表示に戻す。
- **種類**: `none`（なし）/ `fade`（透明度）/ `zoom`（透明度 + 拡大縮小。表示は 0.96→1.0、非表示は 1.0→0.96）/ `slide`（透明度 + 平行移動。向きは演出ごとに固定。移動量 12 px）/ `highlight`（色の変化。ホバー・押下などの状態変化用）/ **`glitch`**（盤面の出現・消失時に一瞬だけ横ずれ + 色にじみ。同じ時間の一定速度のフェード（表示 = イン、非表示 = アウト）を重ねる。盤面の表示・非表示専用。詳細は下の「glitch の詳細」）/ **`glow`**（`highlight` に発光の強まりを足したもの。ホバー・タブ・ドロップ先用。「glow の詳細」）/ **`orb`**（枠を周回する光の玉 1 個、主色）/ **`orbTwin`**（光の玉 2 個。主色と副色が対角で周回）/ **`pulse`**（発光の柔らかい明滅、弱）/ **`pulseStrong`**（同、強）/ **`beam`**（走査線の帯。高さ 60 px の主色のグラデーションの帯が面を上から下へ流れ続ける。2026-10-07）。`orb*` / `pulse*` / `beam` は常時の演出専用（「常時の演出の詳細」）。演出ごとに選べる種類は「選べる種類」列のとおり。
- **既定値は 1 組**（2026-10-04 のサイバーパンク専用化まではテーマで変わった。「既定値の経緯」節）。下表の「初期値」列がその値。ユーザーが変えた値は ID 単位で保存される（SETTINGS.md 表示タブの備考）。
- **イージング**: `linear` / `easeOut`（既定。減速して止まる）/ `easeIn`（加速して消える）/ `easeInOut` / `backOut`（少し行き過ぎて戻る。強調用）。Avalonia のクラス対応: `LinearEasing` / `CubicEaseOut` / `CubicEaseIn` / `CubicEaseInOut` / `BackEaseOut`。
- **時間**: 0〜1000 ms（0 は「なし」と同じ）。範囲外は読み込み時に丸める。**常時の演出**（`frameOrb` / `glowPulse` / `scanBeam`）だけは「時間」が **1 周 / 1 周期 / 1 回流れ切る長さ**で、範囲も演出ごとに違う（`EffectDefinition.MinDurationMs` / `MaxDurationMs` / `DurationStepMs`。下表の備考）。種類が「なし」以外で範囲より短ければ下限に、長ければ上限に丸める。
- **常時の演出**（2026-10-04 追加。「常時の演出の詳細」）: 盤面が見えている間ずっと動く演出。**盤面が隠れている間（ポップアップの非表示中）・常駐の自動収納中・表示 / 非表示の演出（グリッチ）中は止める**。表示演出が終わってから始め、非表示演出が始まる瞬間に止める（表示遅延 100 ms には影響しない）。イージングは使わない（設定画面では無効表示、保存値は `linear` 固定）。アニメーション全体 OFF（`appearance.animation`）で他の演出と同じく止まる。OS の「視差効果を減らす」（macOS）/「アニメーション効果」OFF（Windows）のときも止める（`IWindowService.PrefersReducedMotion`、SPEC §10.3）。**`frameOrb` / `glowPulse` の既定は「なし」**（2026-10-04 ユーザー判断 案 A。Mac で光の玉 22〜28%・明滅 11〜18% と CPU を多く使うため。ユーザーが「演出の調整…」でオンにする。オンにしたときは SPEC §11 の CPU 目標の対象外）。**2026-10-07 追加の走査線の帯 `scanBeam` だけは既定 ON（`beam / 4000`。案 A の唯一の例外、ユーザー判断）**。これに伴い SPEC §11 の「表示中 3%」の目標は「常時の演出をすべて「なし」にした構成」に適用し、既定構成（帯あり）の表示中 CPU は目標なしで実測を記録する。CPU・バッテリーの目標と測り方は SPEC §11。
- **ウィンドウ透過**: 盤面ウィンドウは背景透過（`TransparencyLevelHint = Transparent`）なので、フェード・ズームは盤面の内容（`Frame`）の `Opacity` / `RenderTransform` で行い、不透明度設定（`Window.Opacity`）とは別の層で掛け合わせる。透過が取れない環境ではフェードが「黒い矩形のフェード」になりうる → SPEC §13.3 C8。
- 参考にしたもの: CLaunch（ウィンドウ表示時のフェード／スライド、アイテムのホバー強調、トースト）、macOS Spotlight / Alfred（短いフェード + わずかなズーム）、Windows のフライアウト（下からのスライド + フェード）。

## 演出一覧

列の意味: **ID** は `appearance.effects` のキー兼 `EffectCatalog` の識別子。**初期値** は v1 の控えめな案（種類 / 時間 / イージング）。**実装状況** は実装側が更新する。**ユーザー記入欄** はユーザーが自由に書く（例: 「もっと速く」「スライドにしたい」「要らない」）。

| ID | 演出名 | 対象 | きっかけ | 選べる種類 | 初期値（種類 / ms / イージング） | 設定キー | 実装状況 | ユーザー記入欄 |
|---|---|---|---|---|---|---|---|---|
| `boardShow` | 盤面の表示 | 盤面ウィンドウ全体（`Frame`）。**加えて設定画面・すべてのダイアログ（`ChromeWindow`。glitch のときだけ。SPEC §3.8「演出」、2026-10-04 ユーザー要望）** | ポップアップのトリガー / トレイクリック / 常駐モードの起動時・常駐への切替（SPEC §3.2）/ 設定画面・ダイアログを開く | none / fade / zoom / slide（下から）/ **glitch**（+ 同時間のフェードイン） | **glitch / 150 / —**（2026-10-04 までのライト / ダークは fade / 120 / easeOut） | `appearance.effects.boardShow` | 実装済み（fade / zoom / slide は M5 ステップ 1、glitch は THEME_CYBERPUNK T2、2026-10-03。150 ms・ブレ強め、フェードインの重ね合わせも反映済み） | 2026-10-03 ユーザー判断: glitch は初期値 100 ms では短い → **1.5 倍の 150 ms に、ブレを強めに**（影のずれ 3→5 px、横帯のずれ ±4〜10→±6〜16 px、最後の段 ±4→±6 px）。同日「グリッチにフェードインも重ねたい」→ 反映済み。さらに同日「フェードは**グリッチの半分の時間**に」（表示 = 前半でフェードイン、後半は不透明のままブレる）→ 反映済み |
| `boardHide` | 盤面の非表示 | 同上（**設定画面・ダイアログも**。glitch のときだけ） | Esc / 外クリック / 起動後 / トグル / マウスが離れた / 常駐 → ポップアップへの切替・メニューからの終了（終わるまで `Hide` / `Shutdown` を待つ。OS からの終了では待たない。SPEC §3.2）/ 設定画面・ダイアログを閉じる（× / Esc / OK / キャンセル。アプリ終了・OS 終了では演出しない） | none / fade / zoom / slide（下へ）/ **glitch**（+ 同時間のフェードアウト） | **glitch / 150 / —**（2026-10-04 までのライト / ダークは fade / 80 / easeIn） | `appearance.effects.boardHide` | 実装済み（fade / zoom / slide は M5 ステップ 1、glitch は 2026-10-03 ユーザー要望） | 2026-10-03 ユーザー要望「消えるときもグリッチにしたい」→ 選べる種類に glitch を追加し、サイバーパンクの既定を fade / 60 / easeIn から glitch / 150 に。反映済み。フェードアウトは**後半（時間/2〜時間）だけ**（前半はくっきりブレる。前半で消すと後半のブレが見えないため）。ユーザーに説明済みで、変えたければ言ってもらう |
| `pageSwitch` | ページ切替 | スロットグリッド（`SlotGrid`） | タブクリック / Ctrl+Tab / ホイール / Alt+数字 / アイテムをドラッグ中のタブホバー（500 ms）/ マウストリガー「ホイールクリック + 回転」（SPEC §6.5 / §4.2） | none / fade / slide（切替方向へ。左右） | **fade / 80 / easeOut** | `appearance.effects.pageSwitch` | 実装済み（タブクリックは M5 ステップ 2、Ctrl+Tab / ホイール / Alt+数字 / タブホバーは M4 ステップ 2、ホイールクリック + 回転は M4 ステップ 4。2026-10-03）。きっかけはすべて `BoardWindow.SwitchPage(index)` に集まる（`SwitchPageBy` / `SwitchPageTo` はその入口）ので演出は同じ。端では折り返さず、同じページへの切替は何もしない（演出も出ない） | |
| `tabHighlight` | ページタブのホバー・選択の強調 | ページタブ（`ToggleButton.tab`） | タブにマウスを乗せる / 選択が変わる | none / highlight / **glow** | **glow / 80 / linear**（2026-10-04 までのライト / ダークは highlight） | `appearance.effects.tabHighlight` | 実装済み（highlight は M5 ステップ 2、glow は THEME_CYBERPUNK T1、2026-10-03） | |
| `itemHover` | アイテムのホバー強調 | スロットボタンの背景・枠（`Button.slot`） | スロットにマウスを乗せる / 外す | none / highlight / **glow** | **glow / 80 / linear**（2026-10-04 までのライト / ダークは highlight） | `appearance.effects.itemHover` | 実装済み（highlight は M5 ステップ 1、glow は THEME_CYBERPUNK T1、2026-10-03） | |
| `itemPress` | アイテムの押下 | スロットの中身（アイコン + ラベル） | 左ボタン押下 / 離上 | none / zoom（0.92 倍に縮む） | **zoom / 60 / easeOut** | `appearance.effects.itemPress` | 実装済み（M5 ステップ 2、2026-10-03） | |
| `itemLaunch` | 起動のフィードバック | 起動したアイテムのアイコン | 起動成功時 | none / zoom（1.0→1.12→1.0 のパルス） / fade（一瞬明滅） | **zoom / 150 / easeOut** | `appearance.effects.itemLaunch` | 実装済み（M5 ステップ 2、2026-10-03） | ポップアップで「起動したら閉じる」ON のときは非表示演出が先に始まるのでほぼ見えない。常駐モードや閉じない設定のとき向け |
| `labelHover` | ホバー時ラベルの表示 | ラベル表示 = `hover` のときのラベル（SETTINGS.md 表示タブ） | スロットにマウスを乗せる / 外す | none / fade | **fade / 80 / linear** | `appearance.effects.labelHover` | 実装済み（M5 ステップ 2、2026-10-03） | |
| `iconLoad` | アイコン取得完了 | スロットのアイコン画像（仮表示の頭文字 → 実アイコン） | 非同期のアイコン取得が終わった瞬間 | none / fade | **fade / 100 / linear** | `appearance.effects.iconLoad` | 実装済み（M5 ステップ 2、2026-10-03） | 現在は瞬時に切り替わる |
| `dropTarget` | D&D 中のドロップ先ハイライト | ドロップ先のスロット（空スロット = 登録、アイテム = Drop-to-Open、フォルダ = コピー／移動） | Explorer / Finder からのドラッグがスロット上に入る / 出る | none / highlight / **glow** | **glow / 100 / linear**（2026-10-04 までのライト / ダークは highlight） | `appearance.effects.dropTarget` | 実装済み（highlight は M5 ステップ 2、glow は THEME_CYBERPUNK T1、2026-10-03） | |
| `toastShow` | トーストの表示 | トーストウィンドウ（SPEC §5.2） | トーストが出る | none / fade / slide（下から） | **slide / 150 / easeOut** | `appearance.effects.toastShow` | 実装済み（M5 ステップ 2、2026-10-03） | |
| `toastHide` | トーストの消失 | 同上 | 表示時間の経過 / クリック | none / fade | **fade / 150 / easeIn** | `appearance.effects.toastHide` | 実装済み（M5 ステップ 2、2026-10-03） | |
| `residentAutoHide` | 常駐モードの自動収納・スライドイン | 盤面ウィンドウの位置 | マウスが離れて収納 / 画面端に近づいて復帰（SPEC §3.2） | none / slide（画面端方向） | **slide / 150 / easeInOut** | `appearance.effects.residentAutoHide` | 実装済み（M5 ステップ 3、2026-10-03） | 収納までの待ち時間（500 ms 固定）は演出ではなく動作のパラメータ。要望があれば SETTINGS.md に項目化。**`boardShow` / `boardHide` が glitch のとき（サイバーパンク既定）はスライドに重ねてグリッチも掛かる**: 隠れるときはフェードなし（収納後も 4 px の帯を見せるため）、出てくるときはフェードイン付き（2026-10-03 ユーザー要望。「glitch の詳細」常駐の節） |
| `residentMove` | 常駐の盤面がトリガーでカーソル位置へ移動 | 盤面ウィンドウの位置 | `resident.moveToCursorOnTrigger` ON でのトリガー | none / slide（現在位置から移動先へ） | **none / 0 / —** | `appearance.effects.residentMove` | 実装済み（M5 ステップ 3、2026-10-03） | 即座に移動するのが既定。アニメさせたい場合は slide にする。移動しない前面化（`moveToCursorOnTrigger` OFF）では代わりに一瞬グリッチ（フェードなし）が掛かる（glitch のときだけ。移動中は掛けない） |
| `frameOrb` | 光の玉（枠を周回） | 盤面の枠線の上（`FrameChrome.Overlays` の `OrbLayer`） | **常時**（盤面が見えている間。表示演出の後に始まり、非表示演出・収納・非表示で止まる） | none / **orb**（1 個、主色）/ **orbTwin**（2 個、主色 + 副色が対角） | **none / 0 / —**（選んだときの時間の初期値は 8000） | `appearance.effects.frameOrb` | 実装済み（2026-10-04。**既定 OFF**。見た目の値の確定（任意）は `issue/AMBIENT_EFFECTS.md` ステップ 4、Windows は SPEC §13.3 C17） | 時間 = **1 周の長さ**（2000〜20000 ms、500 刻み。UI は「8.0 s」のように秒で表示）。枠線の中心線を時計回りに一定速度で周回、**面取りの八角形に沿う**（2026-10-06 設計。それまでは角丸矩形で玉が斜辺を横切った）、尾（トレイル）付き。**2026-10-04 ユーザー判断（案 A）: Mac で CPU 22〜28% 使うので、サイバーパンクでも既定は「なし」**（設計時は `orb / 8000`）。オンにしたときは SPEC §11 の CPU 目標の対象外。詳細は「常時の演出の詳細」。2026-10-04 ユーザー要望 |
| `glowPulse` | 発光の明滅（呼吸） | 盤面の枠の発光（八角形に沿う `NeonGlowPlan` を画像に焼いた `PulseLayer`。2026-10-06 まで `FlBoardGlow` の矩形の `Border` を焼いていた） | 同上 | none / **pulse**（弱: 発光が最大 +45%）/ **pulseStrong**（強: 最大 +90%） | **none / 0 / —**（選んだときの時間の既定は 4000） | `appearance.effects.glowPulse` | 実装済み（2026-10-04。**既定 OFF**。選ぶと動く） | 時間 = **1 周期（明 → 暗 → 明）の長さ**（1000〜10000 ms、250 刻み。秒で表示）。波形はサイン（今の明るさを下限に、上へ膨らんで戻る）。**2026-10-04 ユーザー判断（案 A）: 盤面全体を描き直すため CPU を多く使う（Mac で 11〜18%）ので、サイバーパンクでも既定は「なし」**（設計時は `pulse / 4000`）。オンにしたときは SPEC §11 の CPU 目標の対象外。文字の発光・タブ・セル・設定画面の枠は v1 では明滅させない（理由は「常時の演出の詳細」）。2026-10-04 ユーザー要望 |
| `scanBeam` | 走査線の帯 | 盤面の面の上（`AmbientRoot` の `ScanBeamLayer`。スロット・タブ・ステータス行の上、光の玉の下。面の八角形で切り抜く） | 同上（常時） | none / **beam**（高さ 60 px、主色の縦グラデーションの帯 1 本） | **beam / 4000 / —**（**既定 ON**。2026-10-07 ユーザー判断） | `appearance.effects.scanBeam` | **実装済み（2026-10-07、Mac でユーザー確認待ち。`issue/SCANLINES.md`）** | 時間 = **1 回流れ切る長さ**（帯の下端が面の上端に現れてから帯の上端が面の下端を抜けるまで。2000〜12000 ms、500 刻み。秒で表示）。上から下へ一定速度、途切れず繰り返す。SPEC §3.9 H14「走査線の仕様」。ユーザーが試し表示（2026-10-06〜07、Mac）で選んだ組み合わせに含まれるので**既定 ON = 2026-10-04 案 A（常時の演出は既定オフ）の唯一の例外**（CPU が光の玉並みの 20% 台でも既定オン、ユーザー判断）。止めるには「演出の調整…」で「なし」（settings.json に `scanBeam: none` が保存される）。静止した走査線（横線）は演出ではなく HUD の設定 `appearance.hud.scanlines`（SETTINGS.md） |

## 既定値の経緯（旧「テーマ別の既定値」。2026-10-03 設計、2026-10-04 一本化）

2026-10-04 のサイバーパンク専用化（SPEC §1.3）までは、`appearance.theme = cyberpunk` のときだけ `boardShow` / `boardHide`（glitch）、`itemHover` / `tabHighlight` / `dropTarget`（glow）の既定が変わり、コードでは `EffectDefinition.CyberpunkDefault` と `EffectCatalog.DefaultFor(id, ThemeMode)` で引いていた。**専用化後はこれらが唯一の既定値**になる: `CyberpunkDefault` の値を `Default` に移し、`CyberpunkDefault` / `DefaultFor(…, ThemeMode)` の `ThemeMode` 引数 / `Resolve` / `Normalize` の `theme` 引数を削除する（実装済み 2026-10-04）。

- 旧ライト / ダークで「既定と同じ」として保存されなかった値（例 `boardShow` = fade / 120）は、専用化後の読み込みでサイバーパンクの既定（glitch）になる。テーマ廃止に伴いサイバーパンクの既定を受け入れる扱いで、マイグレーションでは補わない（SPEC §3.6「配色」の移行）。
- `frameOrb` / `glowPulse` の既定 `none` はテーマに関係なく 2026-10-04 ユーザー判断 案 A（CPU）によるもので、そのまま。`glowPulse` の行は設定画面に常に出す（出し分けの理由だった「ライト / ダークでは光らない」が無くなった）。
- **`scanBeam`（2026-10-07 追加）の既定は `beam / 4000` = 案 A の唯一の例外**（ユーザー判断。試し表示で選んだ見た目に帯が含まれるため、CPU が光の玉並みでも既定オン）。「既定と同じは保存しない」規則はそのままなので、帯を止めたユーザーの settings.json には `scanBeam: none` が残り、既存ユーザー（キーなし）は次回起動から帯が流れる（マイグレーションはしない）。
- **既定値はいずれも仮**。ユーザーが実物を見て下の「ユーザー記入欄」に書いた内容で更新する。

## glitch の詳細（盤面の表示・非表示専用。2026-10-04 から設定画面・ダイアログの出入りにも同じ部品を使う）

一瞬（既定 150 ms）だけ、盤面の**静止画**を使って「横ずれ（スライス）+ 色にじみ（RGB 分離）」を見せ、表示なら終わったら本物の盤面を静止表示し、非表示なら終わったらウィンドウを隠す。常時動く演出ではない。
**グリッチの半分の時間・一定速度（linear）のフェードを重ねる**（2026-10-03 ユーザー要望「グリッチにフェードの感じも欲しい」→ 同日「フェードは半分の時間に」）: 表示は前半で `Root.Opacity` 0 → 1（後半は不透明のままブレる）、非表示は後半で 1 → 0（前半はくっきりブレる）。`Root` の `Transitions` に `DoubleTransition`（`Opacity`、Duration = glitch の時間 / 2、非表示は Delay = 時間 / 2、`LinearEasing`）を置いて値を切り替える方式で、静止画層と `Frame` の両方にまとめて掛かる。

値の経緯: 設計時の初期値は 100 ms・影 ±3 px・帯 ±4〜10 px・最後の段 ±4 px。T2 のユーザー確認（2026-10-03）で「1.5 倍の長さ・ブレ強め」の判断となり、下表の値に確定（`GlitchPlayer` 定数と `EffectCatalog` の `CyberpunkDefault` に反映済み）。

| パラメータ | 既定値（2026-10-03 確定） | 備考 |
|---|---|---|
| 時間 | **150 ms**（`durationMs`。UI では 200 ms 以下を勧める注記） | 0 なら「なし」。表示遅延（SPEC §11）には含めない |
| ステップ | 3 回（0 / 50 / 100 ms で引き直し）→ 150 ms で静止 | 時間を変えると均等に割り直す（時間 ÷ 3） |
| RGB 分離 | 本体の下に **主色の影を -5 px、副色の影を +5 px**（横方向）。影の不透明度 0.55 | 色は `FlAccent` / `FlAccent2`（配色の主色・副色に追従。SPEC §3.6「配色」） |
| 横ずれ | 本体を**横 3 本の帯**に切り、各帯を **±6〜16 px** 横へずらす。帯の高さは盤面の 6〜18%、位置とずれ量はステップごとに乱数 | 最後のステップ（100 ms）は帯 1 本・**±6 px** に収束させる |
| 重ねるフェード | **グリッチの半分の時間**（150 ms なら 75 ms）、`linear`。表示 = 前半（0〜時間/2）で `Root.Opacity` 0 → 1、後半は不透明のままブレる。非表示 = 後半（時間/2〜時間）で 1 → 0、前半はくっきりブレる（前半で消すと後半のブレが見えないため） | `DoubleTransition`（Duration = 時間/2。非表示は Delay = 時間/2）。フェードは `Root`、不透明度設定は `Window.Opacity` と層が違うので掛け合わせになる。2026-10-03 ユーザー判断（当初は同じ時間だったが半分に）。変えたければユーザーから言う |
| イージング | 使わない（横ずれは段階的に切り替わるステップ関数、フェードは常に linear）。設定画面ではイージング欄を無効表示 | 保存値は `linear` 固定 |
| 乱数 | `Random.Shared`。テストでは種を固定できるようにする | |

**実装方式（Avalonia）**

1. `BoardWindow` の `Frame` を `Root`（`Grid`、`Margin = FlGlowMargin`）で包み、`Frame` の上に `GlitchLayer`（`Canvas`、`IsHitTestVisible = false`、普段は `IsVisible = false`）を重ねる。
2. `PopupController.Show` は今までどおり位置決め → `PrepareShow()` → `Show()` → 前面化 → `PlayShowAsync()`。種類が `glitch` のとき `PrepareShow` は `Root.Opacity = 0`（`Frame` は不透明のまま）にしておく。
3. （表示 = In）`PlayShowAsync` は**同期的に**（最初のフレームが描かれる前に）: `Frame.Bounds` から `RenderTargetBitmap`（`PixelSize = Bounds × RenderScaling`、DPI = 96 × RenderScaling）を作り `Render(Frame)` で盤面の静止画を取る（`Root.Opacity` は `Frame` 自身の描画に影響しない。`Window.Show()` は初回レイアウトを同期的に行うので `Show()` 直後に `Bounds` が決まっている想定。決まっていなければ最初の `LayoutUpdated` で行い、1 フレームの遅れを許容） → `GlitchLayer` に次を置く: 主色の影（`Border`、`Background = FlAccent`、`OpacityMask = ImageBrush(静止画)`、`RenderTransform = translateX(-5)`、`Opacity 0.55`）、副色の影（同、+5 px）、本体（`Image`、静止画）、帯 ×3（`Image` に `Clip = RectangleGeometry(帯の矩形)` と `translateX(ずれ量)`） → `Frame.Opacity = 0`、`GlitchLayer.IsVisible = true`、`Root.Opacity = 1`（`DoubleTransition`（Duration = 時間/2）により 0 → 1 のフェードインが始まり、時間/2 で不透明になる）。ここまでが最初のフレーム。
4. `Task.Delay(時間 ÷ 3)` ごとに帯の位置とずれ量を引き直し（2 回）、時間が来たら `GlitchLayer.IsVisible = false`、子を破棄（`RenderTargetBitmap.Dispose`）、`Frame.Opacity = 1`（フェードインは時間/2 で済んでいるので、後半は不透明のままブレて静止する）。
4b. （非表示 = Out）`PlayHideAsync` で種類が `glitch` のとき: 盤面はまだ見えているので同じ手順で静止画を撮り `GlitchLayer` を組んで `Frame.Opacity = 0`・`GlitchLayer.IsVisible = true` にし、同時に `Root.Opacity = 0` を設定する（`DoubleTransition` の Delay = 時間/2、Duration = 時間/2 なので、前半はくっきりブレ、後半に 1 → 0 のフェードアウトが掛かる）。以後は In と同じ間隔で帯を引き直し、時間が来たら `GlitchLayer` を消して `PlayHideAsync` を完了させる → `PopupController.FinishHideAsync` が `Hide()` を呼ぶ。入力の受付停止とフォーカス返却は方針どおり演出の前に済んでいる。次の表示のために `ResetFrame()` / `PrepareShow()` が `Root.Opacity` と `Frame.Opacity` を表示演出の開始状態に戻す。
5. 影の色付けは `OpacityMask` 方式（静止画のアルファ形状をアクセント色で塗る）。本物のチャンネル分離（R / GB の抜き出し）ではないが、盤面の面がほぼ不透明なので縁と帯のずれにシアン / マゼンタのにじみとして見える。物足りなければ `Bitmap.CopyPixels` でチャンネルを落とした `WriteableBitmap` を 2 枚作る方式へ差し替える（1 枚 1 MB 以下、数 ms）。
6. 打ち切り（演出中に `Hide` / 再 `Show`）: `CancellationToken` で中断し `ResetFrame()`（`GlitchLayer` を消し `Frame.Opacity = 1`）。
7. フォールバック: `RenderTargetBitmap` が作れない（描画バックエンドが無い・失敗）か、`ActualTransparencyLevel` が透過でない（SPEC §13.3 C8）ときは**表示 = `fade / 100 / easeOut`、非表示 = `fade / 100 / easeIn`** で代替し、Debug ログに 1 行（代替のフェードは 150 ms に合わせず短いままでよい）。
8. **表示遅延との関係**: 静止画の生成は数 ms（例: 420×330 論理 px × 1.25 → GPU で 1〜3 ms、ソフトウェア描画で 5〜10 ms）を見込む。これは最初のフレームの前に入るので表示遅延（SPEC §11「表示まで n ms」）に含まれる。Debug ログに「スナップショット n ms」を出し、10 ms を超えるなら静止画の解像度を 1/2 に落とす（ぼけるが一瞬なので問題ない）。
9. 演出中も `Frame` は `Opacity 0` のまま当たり判定を持つ（Avalonia のヒットテストは透明度を見ない）ので、グリッチ中のクリック・キー操作は今までどおり効く。不透明度設定（`Window.Opacity`）は静止画層にも等しく掛かる。
10. 打ち切りの対称性: 非表示グリッチ中にトリガーで再表示されたら `CancelHideAnimation` → `ResetFrame()` で `GlitchLayer` を消し、`Root.Opacity` / `Frame.Opacity` を 1 に戻してから表示演出に入る（`Transitions` が途中値から 1 へ滑らかに戻るのは許容）。
11. ページ切替（`pageSwitch`）への `glitch` 適用は v1 ではしない（要望があれば `SlotGrid` を対象に同じ仕組みで足せる）。
12. **常駐モードでの使い方**（2026-10-03 ユーザー要望、実装済み・確認済み。SPEC §3.2）: `boardShow` / `boardHide` が glitch のときだけ（他の種類なら従来の演出のまま）。
    - 常駐への切替・常駐での起動 = `PlayShowAsync`（グリッチ + フェードイン）。ポップアップへ戻す・メニューから終了 = `PlayHideAsync`（グリッチ + フェードアウト）を**待ってから** `Hide()` / `Shutdown()`。待てるように `IBoardController.DeactivateAsync()` は `Task` を返し、`App.SwitchMode` は前のモードの演出が終わってから次のモードを `Activate` する（同じ盤面ウィンドウを使うため）。OS からの終了（`Exit` イベント）では待たない（`Cleanup` は `DeactivateAsync` を投げっぱなしにして位置の保存だけ確実に行う）。
    - 自動で隠す = `WindowMover` のスライドと同時に `BoardWindow.PlayGlitchAsync(appearing, fade)`: 隠れるときは `appearing: false, fade: false`（収納後も 4 px の帯が見えている必要があるので `Root.Opacity` を落とさない）、出てくるときは `appearing: true, fade: true`。
    - トリガーで前面化 = `PlayGlitchAsync(appearing: true, fade: false)`（見えている盤面を一度消さない）。`moveToCursorOnTrigger` で移動中（`_moving`）は掛けず `residentMove` に任せる。
    - 実装: `GlitchPlayer.Start(..., appearing, fade)` の `fade` 引数で `Root.Opacity` の `DoubleTransition` を付けるかを切り替える。それ以外の手順（静止画・影・帯・3 ステップ）は表示 / 非表示と同じ。盤面が見えている状態から始まるので静止画は常に撮れる（フォールバックは表示 / 非表示と同じ fade）。
13. **設定画面・ダイアログ（`ChromeWindow`）での使い方**（2026-10-04 ユーザー要望「盤面以外の窓も同じグリッチで出入りさせたい」、実装済み・Mac で確認済み。SPEC §3.8「演出」）: `boardShow` / `boardHide` が glitch のときだけ（他の種類なら演出なし。窓専用の ID は作らない）。
    - 部品は盤面と同じ `GlitchPlayer`。`ChromeWindow` が `FrameChrome.Overlays` に自分の `GlitchLayer` を持ち、`Start(chrome, chrome.Frame, layer, spec, FlAccent, FlAccent2, …)` を呼ぶ（`Root` に当たるのは `FrameChrome` 自身）。影の色・帯・ステップ・フェードの半分時間は盤面と同じ値。
    - 表示: コンストラクタで `Chrome.Opacity = 0` にしておき（最初のフレームで素の窓が見えないように）、`Opened` の後 `DispatcherPriority.Loaded` で `Start(appearing: true)`。静止画が撮れなければ `Opacity = 1` にしてそのまま出す（**窓では fade の代替は入れない**。盤面の 7. とは違う）。
    - 非表示: `Closing` で `e.Cancel = true` → `IsHitTestVisible = false` → `Start(appearing: false)` を待つ → `Close(result)` で閉じ直す。`result` は `Close(object)` に渡されたダイアログ結果（Avalonia の `Window._dialogResult` をリフレクションで読む。SPEC §10.5）。2 回目の `Closing` は `_hideGlitchDone` で素通し。`CloseReason` が `ApplicationShutdown` / `OSShutdown` なら演出しない（アプリ終了を待たせない。盤面の「OS からの終了では待たない」と同じ）。
    - 設定の反映: `ChromeWindow.ShowEffect` / `HideEffect`（static）に `App.ApplyWindowEffects` が起動時と `SettingsChange.Effects` のたびに解決値（`EffectCatalog.Resolve`。`appearance.animation` OFF・`PrefersReducedMotion` を含む）を入れる。開いている窓には次の閉じるときから効く。既定は `EffectSpec.None`（UI テストでは動かない。グリッチを試すテストは自分で入れて `finally` で戻す）。

## glow の詳細（ホバー・タブ・ドロップ先）

- `highlight`（背景・枠の `BrushTransition`）に加えて `BoxShadow` を `BoxShadowsTransition` で変化させる。対象は `Button.slot` / `ToggleButton.tab` のテンプレート内 `ContentPresenter`（`BoxShadow` プロパティを持つ）。時間・イージングは `highlight` と同じ値を使う。
- スタイルは「種類が `glow` のとき対象に `.glow` クラスを付ける」方式: `Button.slot.glow:pointerover /template/ ContentPresenter#PART_ContentPresenter { BoxShadow = {DynamicResource FlSlotHoverGlow} }`、`ToggleButton.tab.glow:checked …{ FlTabSelectedGlow }`、`Button.slot.glow.drop-target …{ FlDropGlow }`。`highlight` のときはクラスを付けないので発光しない。発光の色・ぼかし量はトークン（SPEC §3.6。主色・副色に追従）。
- 発光が `Frame` の外へはみ出さないように `Frame` 内側の `Padding`（6 px）で足りる範囲（ぼかし 12〜14 px は `Frame` の `ClipToBounds = false` で `Root` の余白まで出てよい）。
- `labelHover` / `iconLoad` / `itemPress` / `itemLaunch` には `glow` を足さない（「中」の範囲外）。

## 常時の演出の詳細（`frameOrb` / `glowPulse` / `scanBeam`。2026-10-04 設計、2026-10-07 に `scanBeam` を追加）

ユーザー要望「外側フレームや、内側でも可能なところに、光の玉が移動しているアニメーションを入れたい。又、全般的なグローに柔らかい明滅を入れたい」。2026-10-03 の「常時動く演出は入れない」を**この 2 つに限って**改める（ちらつき・点滅カーソルは引き続き入れない）。**走査線の帯 `scanBeam`** は 2026-10-04 のユーザー要望「後ほど検討」（SPEC §3.9 H14）を受けて 2026-10-06〜07 に試し表示で比べ、採用した 3 つ目の常時の演出（本節の規則をそのまま適用）。実装タスクは `issue/AMBIENT_EFFECTS.md`（玉・明滅）/ `issue/SCANLINES.md`（帯）。

**共通の規則**

| 項目 | 規則 |
|---|---|
| 動く条件 | 種類が `none` 以外 **かつ** `appearance.animation` ON **かつ** 盤面が見えている（`IsVisible`）**かつ** 常駐の自動収納中でない **かつ** 表示 / 非表示の演出（グリッチ・フェード）中でない **かつ** OS の「視差効果を減らす」がオフ（`IWindowService.PrefersReducedMotion`、SPEC §10.3。2026-10-04 採用・実装済み）。1 つでも外れたら止める（`BoardWindow.UpdateAmbient`: `AmbientSuspended` / 演出中カウンタ / `ReducedMotion` デリゲートで判定） |
| 始まり | `PlayShowAsync` の完了後（表示演出なしなら `Show()` の直後）。2 層を包む `AmbientRoot`（`Grid`）が **250 ms のフェードイン**（`DoubleTransition`、`Opacity` 0 → 1。`PulseLayer` 自身の `Opacity` は明滅の値なので包む側で行う）で現れる（突然出るのを避ける。表示遅延には含めない）。常駐の自動収納から戻ったとき・前面化のグリッチの後も同じ |
| 止まり | `PlayHideAsync` / `PlayGlitchAsync` の開始時・`Hide()`・収納の開始時に**即座に**非表示（フェードアウトなし。非表示演出の邪魔をしない）。止まっている間はフレーム要求を出さない（CPU 0） |
| 時計 | `Effects/AmbientAnimator.cs`（App）1 つが 3 つ（玉・明滅・帯）を駆動する。`TopLevel.RequestAnimationFrame` を毎フレーム再登録し、`Stopwatch` の経過時間から位置・明るさを計算する。**フレーム間隔は 16 ms 以上**（120 Hz の ProMotion でも 60 fps 以下に抑える）。`DispatcherTimer` は使わない（描画と同期しないのでカクつく） |
| 計算 | OS 非依存の数式は Core `Effects/OrbPath.cs`（八角形 / 角丸矩形の周長と「周長上の距離 → 座標」。形は Core `Theming/Octagon`、2026-10-06）と `Effects/AmbientMath.cs`（周期と経過時間 → 0〜1 の位相、サイン波の明るさ）に置き、単体テストする。App は描くだけ |
| 色 | テーマのトークンを**毎フレーム引く**（`FlAccent` / `FlAccent2` の `Color`。辞書の参照 1 回で軽い）。色が変わったときだけブラシを作り直す（基調色の切替に追従。`ApplyEffects` 経由の配線を増やさない） |
| グリッチとの関係 | `GlitchPlayer` は `Frame` の静止画を撮り、`OrbLayer` / `PulseLayer` / `ScanBeamLayer` は `Frame` の外（`Overlays`）にあるので**静止画に含まれない**。グリッチ中は止めているので二重にも見えない。**帯を `Frame` の中（`Body`）に置かないのはこのため**（置くと `DuringTransition` が `_transitions` を増やす前に `GlitchPlayer.Start` が静止画を撮る順序になっていて、帯が静止画に写る） |
| 背景画像・不透明度 | 光の玉・帯は画像・覆いの上に描かれる。`Window.Opacity`（不透明度設定）はどの層にも等しく掛かる |
| 設定画面での表示 | 「演出の調整…」の末尾に 3 行（`frameOrb` / `glowPulse` / `scanBeam`。行は `EffectCatalog.All` の順に自動で出る）。時間スライダーは演出ごとの範囲・刻み（`EffectDefinition`）、値は**秒で表示**（「8.0 s」）。イージングは無効表示（`glitch` と同じ）。`glowPulse` の行は常に出す（2026-10-04 のテーマ廃止まではサイバーパンクのときだけ）。3 行の下に共通の注記 `Settings_Appearance_Effects_AmbientNote`「光の玉・発光の明滅・走査線の帯は、盤面が見えている間ずっと動き、CPU を多く使います（20〜30% 程度）。走査線の帯以外は既定ではオフです。」（2026-10-04 ユーザー判断 案 A で文言を変更、2026-10-07 に帯を追加し、帯が既定オンになったので末尾を「走査線の帯以外は」に。種類が「なし」でも常に出す = 選ぶ前に分かるように。演出ごとの注記は置かない）。OS の「視差効果を減らす」がオンのときはさらに `Settings_Appearance_Effects_ReducedMotionNote` を出す（コントロールは触れるまま）。文言の英訳は `spec/TERMS.md` |
| 対象にしないもの（v1） | **文字の発光**（`FlTextGlow` は `DropShadowEffect` = 要素ごとのオフスクリーン描画。毎フレーム変えると文字の数だけラスタライズし直すので重い）、**タブ・選択セル・ホバーの発光**（切替・ホバーのたびに生成 / 破棄される要素なので層を重ねにくく、動きもうるさい）、**トースト**（一瞬出るだけ）、**設定画面・ダイアログの枠**（静的な画面。「中」の範囲外。要望があれば `ChromeWindow` に同じ 2 層を足すだけで対応できる）、**ヘッダーの区切り線・タブ下線の上を走る玉**（下の「内側の候補」。枠の玉を見てから決める） |

**`frameOrb`（光の玉）**

| パラメータ | 既定値（仮。実物を見て調整） | 備考 |
|---|---|---|
| 経路 | `Frame` の**枠線の中心線**。面取り（SPEC §3.9 H8、既定 10）のときは**八角形**（`Frame.Bounds` を枠線太さ t の半分だけ内側に寄せ、面取りは `Octagon.OffsetChamfer(c, −t/2)` = `c − (t/2)(2 − √2)`。**2026-10-06 設計で角丸矩形から変更**。それまでは玉が斜辺を横切っていた）、`Chamfer = 0`（テスト用）なら従来の角丸矩形（角 `FlBoardCornerRadius` − t/2）。**時計回り**、始点は上辺の始まり（左上の斜辺の終わり）、周長に対して**一定速度**（角でも減速しない） | `OrbPath.Octagon(w, h, c)`: 周長 = 2(w + h) − 8c + 4c√2。4 辺 + 4 斜辺の 8 区間を距離で引く。`OrbPath.RoundedRect(w, h, r)`: 周長 = 2(w + h) − 8r + 2πr（4 辺 + 4 円弧）。盤面の大きさ・面取りが変わったら経路を作り直す |
| 時間 | **8000 ms で 1 周**（2000〜20000、500 刻み。既定の種類が `none` なのでスライダーの初期位置として使う） | 420×330 の盤面なら周長約 1500 px → 約 190 px/s。ゆっくりが上品 |
| 数 | `orb` = 1 個（主色）、`orbTwin` = 2 個（主色 + 副色。位相が半周ずれて対角を走る） | 既定は `orb`。副色の玉は角ブラケットの 2 色使いと揃う |
| 玉 | 芯: 半径 **2.5 px**、色 = 主色を白と 60% 混ぜた色。暈（かさ）: 半径 **9 px** の `RadialGradientBrush`（中心 = 主色 90% → 縁 = 透明） | `DrawingContext.DrawEllipse` 2 回。暈は枠の外側の余白（40 px。2026-10-06 まで 34）にも、内側の面の上にもはみ出してよい（`Overlays.ClipToBounds = false`） |
| 尾（トレイル） | 玉の後ろ **48 px** ぶんの経路上に **10 点**。半径 7 → 3 px、不透明度 0.6 → 0 に線形に減らした暈だけ（芯なし） | 1 フレームの描画は玉 1 個あたり 12 回の `DrawEllipse`。2 個でも 24 回 |
| 層 | `OrbLayer : Canvas`（`IsHitTestVisible = false`、`ClipToBounds = false`）を `PulseLayer`（と 2026-10-07 から `ScanBeamLayer`）と一緒に `AmbientRoot`（`Grid`）に入れ、`FrameChrome.Overlays` の **`GlitchLayer` より下**に置く（順: `AmbientRoot`（`PulseLayer` → `ScanBeamLayer` → `OrbLayer`）→ `GlitchLayer` → `DragLayer`）。**玉 1 個 = 小さな部品 `OrbSprite : Control`**（芯 + 暈 + 尾を自分の `Render` で描く。大きさは尾の長さ + 暈 + 余白の 2 倍の正方形）で、`OrbLayer.Advance(ms)` が経路上の位置を求めて部品の `RenderTransform`（`TranslateTransform`）と尾の相対座標（`SetTail`）を更新する。層全体を `InvalidateVisual` する方式（設計時）は盤面全体の再描画になり CPU 29% だったので、**描き直す範囲を玉の周りだけ**にするこの形に変えた。ただし再計測では部品方式でも 22〜28%（macOS の Avalonia は一部が動くだけでも毎フレーム窓全体を描き直しているとみられる。「CPU の計測と既定の判断」） | `Overlays` は `Frame` と同じセルなので座標が `Frame.Bounds` と一致する。2 個目（`orbTwin`）は同じ部品のもう 1 つで、`none` / 1 個のときは `IsVisible = false` |

**`glowPulse`（発光の明滅）**

| パラメータ | 既定値（仮） | 備考 |
|---|---|---|
| 作り方 | 枠の静的な発光（`FrameChrome` の `NeonGlowLayer`。SPEC §3.6「発光の作り方」の「枠の発光」）はそのまま残し（アニメーション OFF の見た目 = 今のまま）、その上に **同じ `NeonGlowPlan`（八角形のリング描画。外側 + 内側）を 1 枚の `RenderTargetBitmap` に焼いた画像**（`PulseLayer : Control`。`FlGlowMargin` ぶん外へはみ出して描く。`RenderTargetBitmap.CreateDrawingContext()` に余白ぶん平行移動して `Paint` を呼ぶ）を重ね、`Level`（0〜1）を描画時の `PushOpacity` で掛けて明るさを変える。暈が重なるぶん明るくなる。**大きさ・`RenderScaling`・`FlBoardGlow` の値（配色）・面取り・余白が変わったら `Refresh()` で焼き直す**（表示開始時に `DispatcherPriority.Loaded` で呼ぶ = 大きさが決まってから）。**2026-10-06 設計で変更**: それまでは `FlBoardGlow` の `BoxShadow` を持つ矩形の `Border` を焼いていたため、面取りに沿わず角で四角く光った。`Chamfer = 0`（テスト用）のときだけ従来の `Border` を焼く | 設計時は `Border` の `Opacity` を揺らす案だったが、`BoxShadow` を持つ要素は描き直しのたびにぼかしを計算し直すので画像化した。リング描画は `Effect` を使わないので `RenderTargetBitmap`（`ImmediateRenderer` は `Visual.Effect` を適用しない）にそのまま焼ける = 静的な発光と同じ形が **コード共有で**保証される。要素の `Opacity` ではなく `PushOpacity` にしたのは、要素の `Opacity` が 1 未満だと上を玉が通るたびに層全体を合成し直すため。`Level` は **2% 以上変わったときだけ更新**（`AmbientAnimator.PulseStep = 0.02`。0 ⇄ 非 0 の切替は必ず反映）。それでも層が盤面全体に掛かるので CPU は重い（「CPU の計測と既定の判断」） |
| 波形 | サイン: `Level(t) = peak × (1 − cos(2π t / T)) / 2`。位相 0（今の明るさ）から始まる | `AmbientMath.PulseLevel(elapsedMs, periodMs, peak)` |
| 強さ | `pulse` = peak **0.45**、`pulseStrong` = peak **0.9** | 「柔らかい」が要望なので既定は弱。下限は今の明るさ（暗くはならない。暗くもしたい場合は `Frame` 側の発光を弱い版に差し替える案になるが、OFF 時の見た目が変わるので採らない） |
| 時間 | **4000 ms で 1 周期**（1000〜10000、250 刻み。既定の種類が `none` なのでスライダーの初期位置として使う） | 呼吸の速さ。`frameOrb` とは独立（同期させない。周期が違うので重なりが単調にならない） |
| 同期 | 対象が増えたら（将来）同じ `AmbientAnimator` の時計を使い**全体で同じ位相**にする | ずらすと落ち着かない |

**`scanBeam`（走査線の帯）**（2026-10-07 設計。値はユーザーが試し表示 `ScanlinePreview` で選んだもの = 確定値。SPEC §3.9 H14「走査線の仕様」）

| パラメータ | 値 | 備考 |
|---|---|---|
| 帯 | 高さ **60 px**（`ScanBeamLayer.BandHeight`、定数）、幅 = 面の幅。塗りは主色の縦 `LinearGradientBrush`: 上端 α 0 → **85% の位置で α 28/255（約 11%）** → 下端 **α 60/255（約 24%）**。下端が明るく、上へ尾を引く | 試し表示の `Band(accent)` と同じ値。ブラシは `FlAccent` が変わったときだけ作り直す（毎フレームの `Color` 比較は `OrbLayer.UpdateColors` と同じ） |
| 動き | 上から下へ**一定速度**。`y = −60 + phase × (面の高さ + 60)`、`phase = AmbientMath.Phase(elapsedMs, durationMs)`（0〜1）。帯の下端が面の上端に現れてから帯の上端が面の下端を抜けるまでが 1 周期で、途切れず繰り返す（1 周期の終わりで次の帯がすぐ上から来る） | 数式は Core `AmbientMath.SweepOffset(phase, faceHeight, bandHeight)` に置いて単体テスト。動かすのは子の `Rectangle` の `RenderTransform`（`TranslateTransform.Y`）だけ（層全体の `InvalidateVisual` はしない） |
| 時間 | **4000 ms で 1 回**（2000〜12000、500 刻み。既定値そのもの = `EffectCatalog` の `Default` は `beam / 4000 / linear`） | 330 px の盤面なら約 100 px/s |
| 既定 | **`beam / 4000`（既定 ON。2026-10-07 ユーザー判断）** | 案 A の唯一の例外。`Normalize` は既定と同じ `beam / 4000` を捨て、`none` は残す（他の演出と同じ規則）。既定構成で動くので SPEC §11 の「表示中 3%」の計測構成は「常時の演出をすべて「なし」にした構成」に改めた |
| 層 | `ScanBeamLayer : Canvas`（`IsHitTestVisible = false`、**`ClipToBounds = true` + `Clip` = 面の八角形**）。`AmbientRoot` の中で `PulseLayer` の上・`OrbLayer` の下。**スロット・タブ・ステータス行の上**に乗る（試し表示でユーザーが見た形。最大 24% なので文字は読める） | 八角形は `FrameChrome.FaceClip`（中身の `Clip` = `Octagon(0,0,w−2t,h−2t,c)` を `Frame`（= `Overlays`）座標へ枠の太さ t だけ平行移動したもの。`Chamfer = 0` なら `FlBoardCornerRadius` の角丸矩形。`SizeChanged` で作り直し、`FaceClipChanged` を出す）。`Overlays.ClipToBounds = false` のままだが帯は自分の `Clip` で面に収まる |
| 幅・高さの追従 | `ScanBeamLayer.SizeChanged` で `Rectangle.Width` を面の幅に、`Advance` が面の高さを使う（`Bounds`）。盤面の行列数が変わっても作り直しは要らない | |
| 色 | 主色だけ（副色は使わない） | 配色の切替に追従 |
| 駆動 | `AmbientAnimator.Tick` が `ScanBeamLayer.Advance(elapsedMs)` を呼ぶ。Avalonia の `Animation`（試し表示の方式）は使わない | 理由: 動く条件・250 ms のフェードイン・停止中のフレーム要求なし・16 ms の下限・ヘッドレステスト（`Tick`）が玉・明滅と共通になる。`Animation` の `IterationCount.Infinite` は開始 / 停止と大きさ変更時の作り直しを別に管理することになる |

**内側の候補**（v1 では入れない。枠の玉をユーザーが見てから判断する → `issue/AMBIENT_EFFECTS.md` ステップ 6）

- ヘッダーの区切り線（1 px、`FlHeaderSeparator`）の上を 1 個が左右に**往復**する: 可能。`OrbPath` に「開いた線分 + 往復」を足すだけで `OrbLayer` を共用できる。速度は枠と同じ px/s に揃える。
- 選択タブの下線（2 px）: 短すぎて落ち着かない。タブの作り直しのたびに経路が変わる。見送り。
- ホバー / 選択セルの枠: ホバーのたびに始まり・終わる動きはうるさい。見送り。

**負荷の目標と測り方**（SPEC §11 にも記載）: Release `.app`、**常時の演出をすべて「なし」にした構成**（2026-10-07 までは「既定構成」と同じだったが、`scanBeam` が既定 ON になったので計測構成を明示する。既定構成（帯あり）の表示中 CPU は目標なしで実測を記録）、盤面を出して何もしない 30 秒間の平均で、Mac は Activity Monitor の「% CPU」（または `top -pid <pid> -stats cpu`）**3% 以下**（1 コア換算）、隠した後は今と同じ（0.5% 以下）。Windows はタスクマネージャの CPU 3% 以下（SPEC §13.3 C17）。**`frameOrb` / `glowPulse` をオンにしたときは目標の対象外**（ユーザーが注記を見て選ぶ。2026-10-04 ユーザー判断 案 A）。止まっている間（既定・隠した後・収納中）はフレーム要求を出さないので既定構成では常に目標内。メモリは +1 MB 以下（`PulseLayer` の画像は `glowPulse` が `none` なら焼かない。焼いても 1 枚、盤面 + 余白の大きさ × `RenderScaling`²）。設計時に「超えたときの手順」としていた 30 fps 化・Composition アニメーション化は、原因が窓全体の再描画（描画スレッド側）なので UI スレッドの負荷を減らす Composition 化は効かない見込み。コマ数を落とす方式と別ウィンドウに描く方式は SPEC §12 の将来の検討。

**CPU の計測と既定の判断**（2026-10-04、Mac `.app` Release、`top` の % CPU、盤面表示中・何もしない）

| 構成 | CPU | 備考 |
|---|---|---|
| 演出なし（比較用） | 0.5〜1.5% | |
| 光の玉だけ（`orb / 8000`） | **22〜28%** | **訂正後の値**（最初の計測「1.0〜1.9%」は玉が描かれていなかった可能性が高く、誤り）。`sample` では描画スレッド（RenderTimerLoop）が常に忙しく、Skia → AppleMetalOpenGLRenderer の描画が並ぶ。macOS の Avalonia（OpenGL）は**一部が動くだけでも毎フレーム窓全体を描き直している**とみられ、発光（ぼかし）の多いサイバーパンクの盤面では 1 フレームが重い。`OrbSprite` 方式（描き直す範囲を玉の周りだけにする）でも変わらず。`AvaloniaNativeRenderingMode.Metal` でも 20〜30%（元に戻した） |
| 明滅だけ（`pulse / 4000`） | **11〜18%** | 明滅の層が盤面全体に掛かるので、`Level` の更新のたびに盤面全体が描き直しになり、そのたびに**文字の発光（`FlTextGlow`、文字ごとのぼかし）を計算し直す**ため。文字の発光を消すと約 2%。更新が 2% 刻み（玉より少ない）なので玉より軽い |
| 両方 | **25〜42%** | 窓全体の再描画が毎フレーム起きる（玉と同じ原因）とみられる。Metal でも改善なし |
| 隠した後（どの構成でも） | 0.3〜1.0% | フレーム要求を出していない |

- 試して効かなかったもの: `CompositionOptions.UseRegionDirtyRectClipping`（光の玉が 28% に悪化）、`AvaloniaNativeRenderingMode.Metal`（改善なし）。どちらも元に戻した。
- **ユーザー判断（2026-10-04、案 A。確定）**: **`frameOrb` / `glowPulse` ともどのテーマでも既定を `none`** にする。どちらもユーザーが「演出の調整…」でオンにする扱いで、2 行の下の共通の注記 `Settings_Appearance_Effects_AmbientNote` を「光の玉と発光の明滅は、盤面が見えている間ずっと動き、CPU を多く使います（20〜30% 程度）。既定ではオフです。」に変えた（明滅専用の注記は置かない）。オンにしたときは SPEC §11 の CPU 目標の対象外。
- 常時の演出を軽くする方法（光の玉を別の透明ウィンドウに描く、コマ数を落とす、文字の発光の描き方の見直しなど）は将来の検討（SPEC §12）。軽くなれば既定を再検討する。
- 「仮」の値（玉の大きさ・尾・速さ、明滅の強さ・周期）は変えていない。ユーザーが実物を見て決める（任意。`issue/AMBIENT_EFFECTS.md` ステップ 4）。

**方式の比較**（採ったのは B。実装では「層全体の描き直し」を「部品ごとの `RenderTransform`」に変えた）

| 案 | 内容 | 利点 | 欠点 |
|---|---|---|---|
| A | 枠線の `BorderBrush` を `ConicGradientBrush` にし `Angle` を回す | 描画 1 回で尾も付く | 角度一定 = 周長上の速度が辺の長さで変わり、長辺で玉が伸びる。暈が枠線の外に出ない。ブラシのプロパティのアニメーションが扱いにくい |
| **B** | `OrbLayer` が玉と尾を描き、`RequestAnimationFrame` で毎フレーム位置を更新する。**実装: 玉 1 個を小さな部品 `OrbSprite` にし、`RenderTransform` で動かす**（層全体の `InvalidateVisual` は盤面全体の再描画になり 29%） | 経路・尾・一時停止・色の追従を自由に制御できる。ヘッドレスでも `Advance(ms)` で位置を検証できる。レイアウトは走らない（`RenderTransform` だけ） | UI スレッドが毎フレーム起きる。**Mac では窓全体の再描画が毎フレーム起きて 22〜28%**（原因は描画側なので、この案の問題というより Avalonia / macOS の描き方の問題。「CPU の計測と既定の判断」） |
| C | `Ellipse` を Composition の `Vector3KeyFrameAnimation` で角の座標へ順に動かす | UI スレッドを使わず最も軽い | 角丸の円弧・一定速度・尾（遅延付きの複製 10 個）が複雑。API の情報が少ない。**窓全体の再描画が原因なら効かない見込み**（描画スレッドの負荷は変わらない） |

## 対象外（演出として扱わないもの）

- **常時動く演出のうち**ちらつき、点滅するカーソル: 入れない（2026-10-03 ユーザー確定。演出の強さ「中」）。**光の玉と発光の明滅は 2026-10-04 のユーザー要望で例外として追加**（「常時の演出の詳細」。CPU を多く使うので既定はオフ、選べば動く）。**走査線は 2026-10-04 のユーザー要望で「入れない」から「後ほど検討」になり、2026-10-07 に採用**（SPEC §3.9 H14「走査線の仕様」）: **静止した走査線は演出ではなく見た目**（HUD の設定 `appearance.hud.scanlines` / `scanlineOpacity`。グリッドと同じ静的な層 `ScanlineLayer`。CPU は増えない。`appearance.animation` OFF でも消えない）、**走査線の帯は常時の演出 `scanBeam`**（上の表。macOS では毎フレーム窓全体の再描画になる見込みだが、2026-10-07 のユーザー判断で**既定 ON**（案 A の唯一の例外））。
- 盤面の枠・選択タブの**静的な発光**（SPEC §3.6 の `FlBoardGlow` 等）: テーマの見た目であって演出ではない（`appearance.animation` OFF でも光る。変化しないので CPU を使わない）。`glowPulse` はこの上に重ねる別の層で、OFF ならこの静的な発光だけが残る。
- 不透明度設定（`appearance.opacity`）の変更: スライダーに追従して即時反映する。アニメさせない。
- 背景画像（`appearance.background`、SPEC §3.7）: 静的な見た目。画像の読み込み完了・切替・覆いの変更は瞬時に反映し、演出は付けない。`glitch` の静止画は `Frame` を撮るので画像ごとブレる（追加の処理なし）。
- ~~設定ウィンドウ・権限ガイド・確認ダイアログの表示: 演出は付けない~~ → **2026-10-04 ユーザー要望で対象に入れた**: `boardShow` / `boardHide` が glitch のとき、`ChromeWindow` の全ウィンドウが盤面と同じグリッチで出入りする（「glitch の詳細」の窓の節、SPEC §3.8「演出」）。glitch 以外の種類では従来どおり演出なし（OS のウィンドウ表示に任せる）。窓専用の演出 ID・設定項目は作らない。
- 存在しないアイテムのグレーアウト・警告マーク（SPEC §5.1）: 静的表示。
- ツールチップ: Avalonia 標準。

## 書き方の例（ユーザー記入欄）

- 「`boardShow` は zoom にして 100ms にしたい」
- 「`pageSwitch` は要らない（none）」
- 「`toastShow` は右からスライドが良い」→ 向きの追加は実装側で「選べる種類」を増やす
- 「全体的に 1.5 倍速く」→ 全演出の ms を一括で縮める（一括倍率の設定項目を足すかは、そのとき決める）
