# CPFileLauncher UI 用語の対訳表と文言の規則（日本語 / English）

UI 文字列のリソース化（SPEC §7「言語と UI 文字列」/ §11。M5 で実装済み、2026-10-04 Mac でユーザー確認済み）で使う **英訳の正**。
`src/FileLauncher.Desktop/Resources/Strings.resx`（英語、既定）と `Strings.ja.resx`（日本語）の訳語はこの表に合わせる。
訳語を変えたいときは**先にこの表を直してから** resx を直す（設定項目の名前そのものは `spec/SETTINGS.md` が正、演出の日本語名は `spec/EFFECTS.md` の「演出名」列が正）。

最終更新: 2026-10-08（**macOS でもフォルダを既存の Finder のタブで開く**（SPEC §5.2「macOS: Finder のタブ」/ §4.3「自動化」、`issue/FINDER_TABS.md`。設計のみ・未実装）: 「フォルダを開く先」の Mac の選択肢名 `Enum_FolderOpenTarget_ExistingTab_Mac` = "New tab in the open Finder window"（§4 に enum の `_Mac` 優先の規則を追加）、注記を OS 別キーに（`Settings_General_FolderOpenTarget_Note` → `_Note_Win`、新規 `_Note_Mac`）、メニュー「新しいウィンドウで開く」を両 OS に（文言は同じ）、権限ガイドの 4 文言 `Permission_AutomationStatus` / `Permission_NotAsked` / `Permission_AutomationNote` / `Permission_OpenAutomationSettings`、`Info.plist` の `NSAppleEventsUsageDescription`（resx ではない）。§3 に "⌘-click" / "⌘Enter" の表記。2026-10-07: **静止した走査線の 3 行を表示タブから演出タブへ移す**（ユーザー指摘。SPEC §3.9「走査線の仕様」/ §7）: キー `Settings_Appearance_Hud_Scanlines` / `_Note` / `Settings_Appearance_Hud_ScanlineOpacity` / `Settings_Appearance_Hud_ScanlinePitch` → **`Settings_Effects_Scanlines` / `_Note` / `Settings_Effects_ScanlineOpacity` / `Settings_Effects_ScanlinePitch`**（§4 の規則。行名の訳語は変えない）、小見出し「静止した走査線」= "Static scanlines"（`Settings_Effects_ScanlinesHeading`、新規）、注記 `Settings_Effects_Scanlines_Note` に後半の 1 文「動かないので CPU を使わず、「アニメーション」がオフでも消えません。」を追加（designer 案）。設計のみ・未実装、`issue/AMBIENT_SETTINGS.md` ステップ 3b。同日それ以前: **「ハードウェアアクセラレーション」の語を削除扱いに**（設定項目の廃止、SPEC §10.7 / SETTINGS.md 詳細タブ。キー `Settings_Advanced_Hardware` / `Settings_Advanced_Hardware_Note` を両 resx から削除。`Common_RestartNow` / `Common_RestartNote` は「言語」が使うので残す）。同日それ以前: **「ターミナルで開く」（SPEC §6.3、設計のみ・未実装、`issue/OPEN_TERMINAL.md`）の語を追加**: メニュー「ターミナルで開く」= "Open in Terminal"（`Menu_OpenTerminal`、両 OS 共通の 1 キー）とトースト `Toast_OpenTerminalFailed`。同日それ以前: **走査線（SPEC §3.9 H14、採用）の語を追加**: 設定の行名「走査線」= "Scanlines" / 「走査線の濃さ」= "Scanline strength" / 「走査線の間隔」= "Scanline spacing"（同日のユーザー判断で追加）と注記（間隔が選べるようになったので「3 px ごとに」を外した）、書式 `Common_PixelValue` = "{0} px"、演出「走査線の帯」= "Scan beam"（`Effect_scanBeam`）と種類「帯」= "Beam"（`Enum_EffectKind_Beam`）、共通の注記 `AmbientNote` を帯の既定オンに合わせて「…走査線の帯以外は既定ではオフです。」に。設計のみ・未実装（`issue/SCANLINES.md`）。2026-10-05: **「フォルダを開く先」（SPEC §5.2、Windows のみ）の語を追加**: 設定の行名・選択肢 3 つ・注記、メニュー「新しいウィンドウで開く」。§3 に Windows 限定の文言では「Ctrl+クリック」を直接書いてよい旨。実装済み・Windows でユーザー確認済み（SPEC §13.3 C21）。2026-10-04: **HUD 段階 A の語を実装・Mac でユーザー確認済みの値に合わせた**: ステータス行 = "Status line"、時刻の選択肢 = "Hours:minutes" 等、注記 2 件の文言、「日付（年/月/日）」= "Date (yyyy/MM/dd)" を追加。`PAGE` / `ITEMS` はリソースではなく Core の固定文字列（§5）。同日それ以前: **製品名 CPFileLauncher・サイバーパンク専用化・配色・HUD**（SPEC §1.3 / §3.6「配色」/ §3.9。2026-10-05 までに実装済み・Mac でユーザー確認済み）: 製品名を含む文言を CPFileLauncher に（§5 新設）、「テーマ」「基調色」を削除し「配色 / 主色 / 副色 / カスタム / 入れ替え」を追加、HUD の装飾語（PAGE / ITEMS / タグのコード）は翻訳しない規則、HUD の設定行の語を追加。同日それ以前: 常時の演出の注記 `AmbientNote` の文言を「CPU を多く使います（20〜30% 程度）。既定ではオフです。」に変更（ユーザー判断 案 A。明滅専用の注記は作らず共通の 1 文に統合）、`ReducedMotionNote` を採用済みに。同日それ以前: 新設。M5 ステップ 5 設計。英訳は **2026-10-04 に Mac の実物（`--lang en`）でユーザー確認済み**: 「推奨」列の語をそのまま採用し、resx もこの表どおり。「候補」列は将来差し替えるときの選択肢として残す。変えるときは先にこの表を直してから resx を直す）

## 1. 英語の文体（Windows / macOS の標準に合わせる）

- **Sentence case**（最初の語だけ大文字。固有名詞・製品名は除く）: 項目名・ボタン・メニュー・注記・トーストのすべて。Windows 11 の流儀で、macOS でも違和感がない（"Show in Finder" の Finder は固有名詞）。
- 末尾に続きがある操作（ダイアログを開く・ファイルを選ぶ）は日本語・英語とも「…」（U+2026）を付ける: 「設定…」/ "Settings…"。
- 注記・トーストは完全な文にしてピリオドを付ける。項目名・ボタン・メニューにはピリオドを付けない。
- 引用符は日本語「　」、英語は “ ” （U+201C / U+201D）。引用符はリソースの文字列の中に書き、コードで組み立てない。
- 省略形は使わない（"Ctrl" / "Alt" などキーの名前は例外）。
- OS で呼び名が違うものだけ OS 別のキーを持つ（§3）。それ以外は 1 つの文言で両 OS に出す。
- 単数・複数（英語）: 件数が入る文は `_One` / `_Other` の 2 キーを持ち、コードが件数で選ぶ（`Loc.Count`）。日本語は両キーに同じ文を書く。
- 数の書式・日付の書式は OS の設定（`CultureInfo.CurrentCulture`）に従う。UI 言語（`CurrentUICulture`）とは独立。

## 2. 用語

「推奨」が採用した訳語（2026-10-04 ユーザー確認済み）。「候補」は将来差し替えるときの選択肢。

### 2.1 基本概念（SPEC §2）

| 日本語 | English（推奨） | 候補 | 備考 |
|---|---|---|---|
| 盤面 | board | launcher / panel | SPEC §2 と識別子（`Board`）に合わせる。「盤面を表示」= "Show board" |
| ページ | page | | |
| スロット | slot | cell | 空きスロット = "empty slot" |
| アイテム | item | | |
| 表示モード | display mode | | |
| ポップアップ（モード） | popup | | 「ポップアップモード」= "Popup mode" |
| 常駐（モード） | pinned | always visible / resident | "Resident" は英語として不自然。「常駐」タブ = "Pinned"、「常駐モード」= "Pinned mode" |
| トリガー | trigger | | 「トリガー」タブ = "Triggers" |
| ホットキー | hotkey | keyboard shortcut | 短い方を採る。アイテムの「ショートカットキー」とは区別する |
| マウス操作 | mouse trigger | mouse gesture | 設定画面の行名 |
| 修飾キー | modifier key | | |
| 演出 | effect | animation | 「アニメーション」（全体 ON/OFF）とは別の語にする。「演出の調整…」= "Adjust effects…" |
| 光の玉（演出 `frameOrb`） | Light orb | Orb / Glow orb | `Effect_frameOrb`。常時の演出（`spec/EFFECTS.md`、2026-10-04）。種類: 1 個 = "One orb" / 2 個（主色と副色）= "Two orbs" |
| 発光の明滅（演出 `glowPulse`） | Glow pulse | Breathing glow | `Effect_glowPulse`。種類: 弱 = "Soft" / 強 = "Strong" |
| 走査線の帯（演出 `scanBeam`） | Scan beam | Sweep / Scanning beam | `Effect_scanBeam`。常時の演出（`spec/EFFECTS.md`、2026-10-07 実装済み・Mac でユーザー確認済み（2026-10-07））。種類: 帯 = "Beam"（`Enum_EffectKind_Beam`）。"scanline" と区別するため "beam" を採る |
| 常時の演出の注記 | Light orb, glow pulse, and scan beam run the whole time the board is visible and use a lot of CPU (about 15–25%). Set the kind to None to stop them, or raise VSync to lighten the load. | | 日本語「光の玉・発光の明滅・走査線の帯は、盤面が見えている間ずっと動き、CPU を多く使います（15〜25% 程度）。種類を「なし」にすると止まり、「VSync」を大きくすると軽くなります。」（`Settings_Effects_AmbientNote`。旧 `Settings_Appearance_Effects_AmbientNote`、2026-10-07 の演出タブ新設で改名。経緯: 2026-10-04 案 A「…（20〜30% 程度）。既定ではオフです。」→ 2026-10-07 午前「…走査線の帯以外は既定ではオフです。」→ **2026-10-07 午後、数値を実測に合わせて「15〜25%」に（ユーザー判断）、3 つとも既定オンになったので後半を止め方 / 軽くし方に差し替え（文言は designer 案、要ユーザー確認）**。未実装、`issue/AMBIENT_SETTINGS.md`）。範囲のダッシュは en ダッシュ U+2013。"None" は種類の表示名 `Enum_EffectKind_None` と同じ語。「演出の調整…」の 3 行 + VSync の下に常に出す） |
| VSync | VSync | Refresh interval / Frame rate | `Settings_Effects_Vsync`。**両言語とも "VSync" のまま（翻訳しない。§5 の HUD 装飾語と同じ扱い。ユーザーの言葉、2026-10-07）**。項目の書式 `Settings_Effects_Vsync_Item` = 日「{0}（{1} fps）」/ 英「{0} ({1} fps)」（`{0}` = 1〜3、`{1}` = 60 / 30 / 20。"fps" は小文字・翻訳しない）。注記 `Settings_Effects_Vsync_Note` = 日「光の玉・発光の明滅・走査線の帯を描き直す間隔です。数字が大きいほど CPU を使いませんが、動きが粗くなります。」/ 英 "How often the light orb, glow pulse, and scan beam redraw. Higher numbers use less CPU but move less smoothly."。SETTINGS.md 表示タブ、未実装 |
| 視差効果を減らす（OS 設定で停止中の注記） | Stopped because the system’s reduce-motion setting is on. | | 日本語「OS の「視差効果を減らす」設定がオンのため停止しています。」（`Settings_Appearance_Effects_ReducedMotionNote`。2026-10-04 採用・実装済み。OS 設定がオンのときだけ「演出の調整…」の末尾に出す） || アニメーション | animation | | 全演出の ON/OFF |
| サイバーパンク | Cyberpunk | | 固有名として大文字。「テーマ」の語は 2026-10-04 のサイバーパンク専用化で UI から消えた（`Enum_ThemeMode_*` / `Settings_Appearance_Theme*` のキーは削除） |
| 配色 | Color scheme | Colors / Palette | 旧「基調色 = accent color」を置き換え（SPEC §3.6「配色」）。選択肢: シアン = Cyan / レッド = Red / ブルー = Blue / グリーン = Green / パープル = Purple / カスタム = Custom |
| 主色 / 副色 | Primary color / Secondary color | Accent / Second accent | 行名。hex の表示は翻訳なし |
| 入れ替え（主色と副色） | Swap | | 「⇄」ボタンのツールチップ「主色と副色を入れ替える」= "Swap primary and secondary colors" |
| ステータス行 | Status line | Status bar / HUD bar | `appearance.hud.statusBar`（`Settings_Appearance_Hud_StatusBar`）。実装・2026-10-04 Mac でユーザー確認済みの語。注記「盤面の下にページ・アイテム数・時刻を出します。」= "Shows the page, item count, and time below the board."（`_Note`） |
| 時刻（ステータス行） | Clock | | 選択肢（`Enum_ClockMode_*`）: 表示しない = Off / 時:分 = Hours:minutes / 時:分:秒 = Hours:minutes:seconds（コロン区切りの表記で確定）。注記「「時:分:秒」は毎秒書き換えるので、盤面が見えている間 CPU を少し使います。」= "Hours:minutes:seconds updates every second and uses a little CPU while the board is visible."（`_Note`。Mac 計測 0.4〜0.7% だが注記は残す） |
| 日付（年/月/日）（ステータス行） | Date (yyyy/MM/dd) | | `appearance.hud.date`（`Settings_Appearance_Hud_Date`）。2026-10-04 ユーザー要望で追加。書式そのものは翻訳しない（`yyyy/MM/dd` 固定）。注記なし |
| 背景グリッド | Background grid | | `appearance.hud.grid`（`Settings_Appearance_Hud_Grid`）。実装・2026-10-04 Mac でユーザー確認済みの語。注記「スロットの下に主色の細い線を敷きます。」= "Thin lines in the primary color behind the slots."（`_Note`。設計時の「盤面の背景に薄いグリッドを敷きます。」= "Draws a faint grid behind the board." から、主色の線であることが分かる文言に変更） |
| グリッドの濃さ（背景グリッド） | Grid strength | Grid opacity | `appearance.hud.gridOpacity`（`Settings_Appearance_Hud_GridOpacity`）。2026-10-04 ユーザー要望で追加。値は `%` 表示（翻訳なし）。注記なし。"opacity" は「不透明度」（ウィンドウ全体）と紛れるので、線の見え方を指す "strength" を採る |
| 静止した走査線（演出タブの小見出し） | Static scanlines | Scanlines (static) | `Settings_Effects_ScanlinesHeading`。演出タブの末尾、「すべて既定に戻す」の下に置く小見出し（「常時の演出」と同じ作り）。下の 3 行をまとめ、直上の行「走査線の帯」= "Scan beam" と区別する。SPEC 本文の語「静止した走査線」をそのまま使う。2026-10-07 夕方の設計・未実装 |
| 走査線 | Scanlines | Scan lines | `appearance.hud.scanlines`（**`Settings_Effects_Scanlines`**。2026-10-07 夕方に演出タブへ移動するのに伴い `Settings_Appearance_Hud_Scanlines` から改名、§4。未実装）。SPEC §3.9 H14、2026-10-07 実装済み・Mac でユーザー確認済み（表示タブの行として）。1 語 "Scanlines"（CRT の用語として定着）。注記（`_Note`）「スロットの下に主色の横線を敷きます。動かないので CPU を使わず、「アニメーション」がオフでも消えません。」= "Thin horizontal lines in the primary color behind the slots. They do not move, so they use no CPU and stay on even when Animation is off."（後半は演出タブへの移動で追加。「アニメーション」/ "Animation" は同じタブの行名 `Settings_Effects_Animation` と同じ語。designer 案、要ユーザー確認。設計時の「3 px ごとに」は間隔が選べるようになったので外した） |
| 走査線の濃さ | Scanline strength | Scanline opacity | `appearance.hud.scanlineOpacity`（**`Settings_Effects_ScanlineOpacity`**、旧 `Settings_Appearance_Hud_ScanlineOpacity`）。値は `%` 表示（翻訳なし）。注記なし。「グリッドの濃さ」= "Grid strength" にそろえる |
| 走査線の間隔 | Scanline spacing | Scanline pitch | `appearance.hud.scanlinePitch`（**`Settings_Effects_ScanlinePitch`**、旧 `Settings_Appearance_Hud_ScanlinePitch`）。2026-10-07 ユーザー判断で追加。選択肢は「2 px」「3 px」「4 px」で、書式リソース **`Common_PixelValue`** = "{0} px"（両言語同じ値。数値と単位は翻訳しない。半角数字 + 半角スペース + px）。注記なし。"pitch" は印刷 / 工学の語で一般には通じにくいので "spacing" |
| 不透明度 | opacity | | |
| 背景画像 | background image | | |
| サムネイルに画像ファイルをドロップしても設定できます。 | You can also drop an image file onto the thumbnail. | Drag an image onto the preview | `Settings_Appearance_Background_Note`。「背景画像」行の下の注記（SPEC §3.7「ドロップで設定」。ドロップ先はサムネイルだけ）。「サムネイル」は「画像はサムネイルで表示」と同じ語。2026-10-07 設計・実装済み（Windows 確認済み） |
| ここにドロップ | Drop here | Drop image here | `Settings_Appearance_Background_DropHint`。ドラッグ中、画像が無いときにプレビューの「なし」（`Common_None`）の代わりに出す。文ではないのでピリオド無し。2026-10-07 設計・実装済み（Windows 確認済み） |
| 画像の覆い | color overlay | overlay | 画像の上に重ねる面の色（SPEC §3.7）。"Image overlay" だと画像を重ねる意味に読めるので避ける |
| 画像の不透明度 | image opacity | | |
| 表示方法（背景画像） | fit | | 選択肢: 埋める = Fill / 収める = Fit / 引き伸ばす = Stretch / 並べる = Tile / 中央 = Center |
| ラベル表示 | labels | | 選択肢: 表示しない = Hidden / アイコンの下 = Below icon / マウスを乗せたとき = On hover |
| ボタンサイズ | button size | icon size | 小 / 中 / 大 / 特大 = Small / Medium / Large / Extra large |
| トースト | — | | UI に出ない語 |
| 通知（ウィンドウ） | notice | | ウィンドウのタイトル「データの読み込み」= "Loading data" |
| 権限ガイド | permission guide | | ウィンドウタイトル「権限の設定」= "Permissions" |
| アクセシビリティ | Accessibility | | macOS の機能名。大文字 |
| オートメーション（Finder の制御）: {0} | Automation (control Finder): {0} | | `Permission_AutomationStatus`。権限ガイドの状態行（SPEC §4.3「自動化」。2026-10-08 設計・未実装）。`{0}` は `Permission_Granted` / `Permission_NotGranted` / 下の `Permission_NotAsked`。"Automation" はシステム設定の項目名で大文字 |
| 未確認（フォルダを開くときに確認されます） | Not asked yet (macOS asks when you open a folder) | | `Permission_NotAsked`。macOS が拒否済みでも「未確認」を返すことがあるので「未確認または未許可」の意味で使う（SPEC §4.3） |
| フォルダを開いている Finder ウィンドウのタブで開くには、オートメーションで Finder の制御を許可してください（任意。許可しないと新しいウィンドウで開きます）。 | To open folders in a tab of the open Finder window, allow CPFileLauncher to control Finder under Automation (optional; otherwise folders open in a new window). | | `Permission_AutomationNote`。権限ガイドの状態行の下の注記 |
| オートメーションの設定を開く | Open Automation settings | | `Permission_OpenAutomationSettings`。権限ガイドのボタン（`IPermissionService.OpenAutomationSettings`） |
| （`Info.plist` `NSAppleEventsUsageDescription`。resx ではない） | Opens folders in a tab of the open Finder window. | | **`Info.plist` に英語を直接書く**。日本語は `macos/ja.lproj/InfoPlist.strings`: 「開いている Finder ウィンドウのタブにフォルダを開くために使います。」（`en.lproj/InfoPlist.strings` にも同じ英語を置く。SPEC §10.4）。macOS の許可ダイアログの本文に出る。製品名は OS が付けるので書かない。2026-10-08 設計・未実装 |
| データフォルダ | data folder | | |
| ポータブルモード | portable mode | | |
| バックアップ世代数 | backups to keep | backup generations | |
| エクスポート / インポート | export / import | | ボタン: "Export…" / "Import…" |
| ログ | log | | 「ログを出力する」= "Write log files"、「ログフォルダを開く」= "Open log folder" |
| ~~ハードウェアアクセラレーション~~ | ~~hardware acceleration~~ | | **削除**（2026-10-07。設定項目の廃止、SPEC §10.7。キー `Settings_Advanced_Hardware` / `_Note`（"Takes effect after restart. Turn it off if the display is garbled."）を両 resx から消す。`StringsTests` のキー一致はキーごと消せば通る） |
| 言語 | language | | 選択肢は各言語の自称: システム = System / 日本語 / English（日本語 UI でも "English"、英語 UI でも「日本語」） |
| 再起動後に反映 | takes effect after restart | | 注記 "Takes effect after restarting CPFileLauncher." ボタン「今すぐ再起動」= "Restart now" |

### 2.2 トレイ / メニューバー・コンテキストメニュー（SPEC §6.3 / §8）

| 日本語 | English（推奨） | 候補 | 備考 |
|---|---|---|---|
| 盤面を表示 | Show board | | |
| 表示モード ▸ ポップアップ / 常駐 | Display mode ▸ Popup / Pinned | | |
| ページ ▸ | Pages ▸ | | |
| 設定… | Settings… | Preferences… | macOS 13 以降も "Settings…" |
| 権限の設定… | Permissions… | | macOS のみ |
| 終了 | Exit（Win） / Quit CPFileLauncher（Mac） | | OS 別キー。Mac のアプリメニューの流儀 |
| CPFileLauncher について | About CPFileLauncher | | About の見出しは製品名 "CPFileLauncher"、副題 "CyberPunk FileLauncher"（両言語とも英字のまま） |
| 起動 | Launch | Open | |
| 管理者として起動 | Run as administrator | | Windows のみ |
| 格納フォルダを開く / Finder で表示 | Open file location（Win） / Show in Finder（Mac） | | OS 別キー。各 OS の標準表記 |
| 新しいウィンドウで開く | Open in new window | Open in a new window | `Menu_OpenNewWindow`。**フォルダアイテムのみ。両 OS**（Windows 2026-10-05 実装済み・ユーザー確認済み。**macOS は 2026-10-08 設計・未実装**、SPEC §5.2「macOS: Finder のタブ」）。設定「フォルダを開く先」に関係なく新しい Explorer / Finder ウィンドウ（SPEC §6.3）。ショートカット表示 Ctrl+Enter / ⌘Enter（`KeyGesture` から OS の流儀で出る）。Explorer の「新しいウィンドウで開く」/ "Open in new window"、Finder の「新規ウィンドウで開く」とほぼ同じ語（Finder の語には合わせず両 OS 1 キーのまま） |
| ターミナルで開く | Open in Terminal | Open Terminal here / New Terminal at Folder（Mac の Finder の語） | `Menu_OpenTerminal`。**両 OS 共通の 1 キー**（OS 別キーにしない: Windows 11 の Explorer の「ターミナルで開く」/ "Open in Terminal" と同じ語で、Mac でも "Terminal" がアプリ名として通る）。"Terminal" は Windows Terminal / Terminal.app の固有名として大文字。アイテムの右クリックメニュー、「格納フォルダを開く / Finder で表示」の直後（SPEC §6.3「ターミナルで開く」）。URL では出さず、コマンドは作業フォルダがあるときだけ。2026-10-07 実装済み・Windows でユーザー確認済み（Mac は未確認、`issue/MAC_SUPPORT.md` Mac-2） |
| 編集… | Edit… | | |
| 複製 | Duplicate | | |
| 削除 | Delete | Remove | |
| 新規アイテム… | New item… | | |
| 貼り付け | Paste | | |
| 元に戻す | Undo | | |
| 色 ▸ | Color ▸ | | なし = None、赤 = Red、橙 = Orange、黄 = Yellow、緑 = Green、水色 = Cyan、青 = Blue、紫 = Purple、ピンク = Pink |
| 新規ページ | New page | | |
| ページの設定… | Page settings… | | |
| ページを複製 / ページを削除 | Duplicate page / Delete page | | |
| 左へ移動 / 右へ移動 | Move left / Move right | | |

### 2.3 設定画面（タブと主な行。項目の定義は `spec/SETTINGS.md`）

| 日本語 | English（推奨） | 候補 | 備考 |
|---|---|---|---|
| 一般 / 表示 / **演出** / ポップアップ / トリガー / 常駐 / データ / 詳細 | General / Appearance / **Effects** / Popup / Triggers / Pinned / Data / Advanced | 「表示」= Display、「演出」= Animations | タブは短い語にする。7 タブは 560 px に収まっていたが、**2026-10-07 に「演出」タブ（`Settings_Tab_Effects`。表示タブの「アニメーション」「演出の調整…」を独立させた。SPEC §7「演出タブの構成」）が増えて 8 タブになり、本体の幅を 620 に広げる**（未実装） |
| 常時の演出（演出タブの小見出し） | Ambient effects | Always-on effects | `Settings_Effects_AmbientHeading`。光の玉 / 発光の明滅 / 走査線の帯の 3 行の上の小見出し。未実装 |
| 詳細設定（演出タブの折りたたみ） | Advanced effects | More effects… / Advanced… | `Settings_Effects_Advanced`。旧「演出の調整…」（`Settings_Appearance_Effects` "Adjust effects…"）を置き換える。常時の演出 3 つ以外の 11 演出を従来の行の形で収める。タブ名「詳細」= "Advanced" と紛れないよう "effects" を付ける。未実装 |
| すべて既定に戻す（演出タブ） | Reset all to default | | `Settings_Effects_ResetAll`（旧 `Settings_Appearance_Effects_ResetAll`。文言は同じ）。`effects` を空に + `vsync` を 2 に。静止した走査線（ボタンの下の 3 行）は対象外（文言は変えない。対象外であることは配置で示す、SPEC §7） |
| OS ログイン時に自動起動 | Start at login | Launch at login（Mac） | 両 OS で "Start at login" |
| フォルダを開く先 | Open folders in | Folders open in | `Settings_General_FolderOpenTarget`。**両 OS**（SPEC §5.2、SETTINGS.md 一般タブ。2026-10-08 まで Windows のみ）。選択肢（`Enum_FolderOpenTarget_*`）: 開いている Explorer の新しいタブ = **New tab in the open Explorer window**（`ExistingTab`）/ **開いている Finder ウィンドウの新しいタブ = New tab in the open Finder window**（**`Enum_FolderOpenTarget_ExistingTab_Mac`**。macOS で優先される enum 名、§4。2026-10-08 設計・未実装）/ 新しいウィンドウ = **New window**（`NewWindow`。両 OS 同じ）/ Windows に任せる = **Let Windows decide**（`System`。Windows だけに出す）。注記は OS 別キー（§3）: **`Settings_General_FolderOpenTarget_Note_Win`**（旧 `_Note` を改名）「Ctrl+クリック（Ctrl+Enter）は、この設定に関係なく新しいウィンドウで開きます。タブの追加は Explorer の非公開の仕組みを使うため、Windows の更新で動かなくなったら「Windows に任せる」を選んでください。」= "Ctrl+click (or Ctrl+Enter) always opens a new window. Adding a tab relies on an undocumented Explorer feature; if a Windows update breaks it, choose “Let Windows decide”."、**`Settings_General_FolderOpenTarget_Note_Mac`**「⌘クリック（⌘Enter）は、この設定に関係なく新しいウィンドウで開きます。タブを足すには Finder の制御（システム設定 → プライバシーとセキュリティ → オートメーション）の許可が必要で、初めて使うときに macOS が確認します。許可しない場合は新しいウィンドウで開きます。」= "⌘-click (or ⌘Enter) always opens a new window. Adding a tab needs permission to control Finder (System Settings → Privacy & Security → Automation); macOS asks the first time. Without it, folders open in a new window."。"Explorer" / "Windows" / "Finder" / "Automation" は固有名詞で大文字。Windows 側は 2026-10-05 実装済み・ユーザー確認済み（英語表示も確認済み） |
| 移動時に確認（フォルダへの Shift+ドロップ） | Confirm before moving files | | |
| ホイールでページを切り替える | Scroll wheel switches pages | | |
| Alt+1〜9 でページを切り替える | {0} switches pages | | `{0}` = "Alt+1–9" / "⌥1–9"（`Loc.Shortcut`。§3） |
| 不透明度 | Opacity | | |
| 既定の行数 / 列数 | Default rows / Default columns | | |
| 画像はサムネイルで表示 | Show images as thumbnails | | |
| 表示位置（キーボードで開いたとき / マウス操作で開いたとき） | Position (opened by keyboard) / Position (opened by mouse) | | 選択肢: カーソル位置 = At pointer / 前回の位置 = Last position / 画面中央 = Screen center / 固定座標 = Fixed |
| 固定座標 X / Y | Fixed position X / Y | | |
| 今の盤面の位置を使う | Use current board position | | |
| 起動したら閉じる | Close after launching | | |
| 盤面の外をクリックしたら閉じる | Close when clicking outside | | |
| もう一度トリガーしたら閉じる | Trigger again to close | Close on repeated trigger | |
| マウスが離れたら閉じる | Close when the pointer leaves | | |
| 離れたとみなす距離 | Leave distance | | |
| ホットキーを使う | Use hotkey | | |
| デスクトップ上でのみ有効 | Only on the desktop | | |
| 長押しとみなす時間 | Long-press time | | |
| 左右同時押しの判定時間 | Left + right click window | | |
| マウス操作の種類 | クリック = Click / 長押し = Long press / 左右同時クリック = Left + right click / ホイールクリック + 回転 = Wheel click + scroll | | |
| マウスのボタン | 左 = Left / 中 = Middle / サイド 1 = Side 1 / サイド 2 = Side 2 | Back / Forward | |
| 重ね順 | Stacking order | | 選択肢: 最前面 = Always on top / 通常 = Normal / 最背面 = Behind other windows |
| 自動で隠す | Auto-hide | | Windows のタスクバーの語 |
| 位置をロック | Lock position | | |
| トリガーでカーソル位置へ移動 | Move to the pointer on trigger | | |
| 設定フォルダを開く | Open data folder | | |
| 設定をリセット… | Reset settings… | | |
| オン / オフ（ToggleSwitch） | On / Off | | |
| 既定に戻す / すべて既定に戻す | Reset to default / Reset all to default | | |
| 閉じる / キャンセル / OK | Close / Cancel / OK | | |
| 追加 / 削除（行） | Add / Remove | | |
| 参照… | Browse… | Choose…（Mac） | 両 OS で "Browse…" |

### 2.4 アイテムの登録・編集ダイアログ（SPEC §6.4）

| 日本語 | English（推奨） | 候補 | 備考 |
|---|---|---|---|
| アイテムの登録 / アイテムの編集 | New item / Edit item | | ウィンドウタイトル |
| 種別 | Type | | ファイル = File / フォルダ = Folder / アプリケーション = Application / URL / コマンド = Command |
| パス / URL / コマンド | Path, URL, or command | | |
| 表示名 | Name | | 「空なら自動」= "Automatic if empty" |
| 引数 | Arguments | | |
| 作業フォルダ | Working folder | Working directory | 「空なら対象のフォルダ」= "Target's folder if empty" |
| アイコン | Icon | | 変更… = Change… / 既定に戻す = Reset |
| 起動方法 | Window | Start as | 通常 = Normal / 最小化 = Minimized / 最大化 = Maximized。Windows のみ |
| 管理者として実行 | Run as administrator | | Windows のみ |
| ショートカットキー | Shortcut key | | 「英数字 1 文字にしてください。」= "Use a single letter or digit." |
| 名前 / 行数 / 列数 / ボタンサイズ（ページの設定） | Name / Rows / Columns / Button size | | 「継承（共通設定 48 px）」= "Default ({0} px)" |

### 2.5 トースト・確認（件数つき）

| 日本語 | English（推奨） | 備考 |
|---|---|---|
| 「{0}」へ {1} 件コピーしました / 移動しました | Copied {1} item to “{0}” / Copied {1} items to “{0}” （Moved …） | `_One` / `_Other` |
| 別名 {0} 件 | {0} renamed | 括弧内の断片。区切りは `Common_ListSeparator`（ja「、」/ en ", "） |
| 同一のため {0} 件スキップ | {0} skipped (identical) | |
| {0} 件失敗しました（ログ参照） | {0} failed (see log) | |
| {0} 件は空きスロットが無いため登録しませんでした。 | {0} item was not added because there is no empty slot. / {0} items were not added … | |
| {0} 件を空きスロットへ移動しました。 | Moved {0} item to an empty slot. / Moved {0} items to empty slots. | |
| {0} 件を「{1}」へ移動します。よろしいですか？ | Move {0} item to “{1}”? / Move {0} items to “{1}”? | OK ボタン「移動する」= "Move" |
| 「{0}」でターミナルを開けませんでした。\n{1} | Could not open a terminal at “{0}”.\n{1} | `Toast_OpenTerminalFailed`。`{0}` = 開こうとしたフォルダ（アイテム名ではない。フォルダはアイテムから導くので、どこを開こうとしたかを見せる）、`{1}` = 理由（`Loc.LaunchError`。「見つかりません: …」等）。`Toast_RevealFailed`「「{0}」の場所を開けませんでした。\n{1}」と同じ形。2026-10-07 実装済み（Windows でユーザー確認済み） |
| 背景画像にできる画像ファイルがありません（png / jpg / jpeg / bmp / gif / webp）。 | No usable image file was dropped (png, jpg, jpeg, bmp, gif, webp). | `Toast_BackgroundDropUnsupported`。設定画面の「背景画像」のサムネイルにフォルダ・非対応形式・テキストだけを落としたとき（SPEC §3.7「ドロップで設定」）。書式引数なし。拡張子の並びは `BackgroundImageStore.Extensions` の順（翻訳しない）。2026-10-07 設計・実装済み（Windows 確認済み） |

## 3. OS で表記が変わるもの（キーの名前と使い方）

- **キーの名前・記号は翻訳しない**（`Ctrl` / `Alt` / `Shift` / `Win` と `⌃` / `⌥` / `⇧` / `⌘` は日本語 UI でも英語 UI でも同じ）。App 層の `Loc.Shortcut(modifiers, key)` が OS の流儀で組む: Win「Ctrl+Shift+Tab」、Mac「⌃⇧Tab」。文には `{0}` で埋める。
- **OS で呼び名が違う語だけ** `_Win` / `_Mac` の 2 キーを持ち、`Loc.Os(win, mac)` で選ぶ: 格納フォルダを開く / Finder で表示、終了 / CPFileLauncher を終了、トレイアイコン / メニューバーアイコン（"tray icon" / "menu bar icon"）。
- その OS に無い機能の文言はキーごと作らない（項目自体を出さないため。SPEC §1.2）。例: 管理者として起動（Win のみ）、権限の設定…（Mac のみ）はそれぞれ 1 キーだけ。
- **片方の OS にしか出ない文言では、その OS のキー表記を文に直接書いてよい**（`Loc.Shortcut` で組まなくてよい）。例: 「フォルダを開く先」の注記の「Ctrl+クリック（Ctrl+Enter）」/ "Ctrl+click (or Ctrl+Enter)"（`_Note_Win`。2026-10-05）と「⌘クリック（⌘Enter）」/ "⌘-click (or ⌘Enter)"（`_Note_Mac`。2026-10-08）。両 OS に出る文は従来どおり `{0}` + `Loc.Shortcut`。英語で ⌘ とクリックをつなぐときは "⌘-click"（Apple の表記）、キーとは "⌘Enter"（間に何も入れない）。

## 4. リソースキーの命名（`Strings.resx`）

- `領域_要素[_部位]` を PascalCase と `_` で書く。領域: `Common` / `Tray` / `Menu` / `Settings` / `Effect` / `Enum` / `Toast` / `Dialog` / `Notice` / `Permission` / `About` / `ItemEditor` / `PageSettings` / `Board` / `Launch` / `Load` / `Archive`。
- 設定画面は `Settings_<タブ>_<項目>` で、注記は `_Note`、選択肢は `_<値>`: `Settings_General_Language`、`Settings_General_Language_Note`、`Settings_General_Language_System`。タブ名は `Settings_Tab_General`。**項目を別のタブへ移したらキーも改名する**（2026-10-07 の「演出」タブ新設で `Settings_Appearance_Animation` / `_Animation_Note` / `Settings_Appearance_Effects`（→ `Settings_Effects_Advanced`、文言も変更）/ `Settings_Appearance_Effects_ResetAll` / `Settings_Appearance_Effects_Reset` / `Settings_Appearance_EffectRow`（→ `Settings_Effects_Row`）/ `Settings_Appearance_Effects_AmbientNote` / `Settings_Appearance_Effects_ReducedMotionNote` を **`Settings_Effects_*`** に一括改名。値は文言変更のあるもの以外そのまま。コード・テストは `Strings.*` 参照なので置換で追従する。実装済み。**同日夕方、静止した走査線の 3 行も演出タブへ移すので `Settings_Appearance_Hud_Scanlines` / `_Note` / `Settings_Appearance_Hud_ScanlineOpacity` / `Settings_Appearance_Hud_ScanlinePitch` → `Settings_Effects_Scanlines` / `_Note` / `Settings_Effects_ScanlineOpacity` / `Settings_Effects_ScanlinePitch` に改名**（`_Note` は文言も追加。未実装）。JSON キー `appearance.hud.*` は設定の保存先であって UI の位置ではないので改名しない）。
- enum の表示名は `Enum_<型名>_<値>`（`Enum_ClockMode_Minutes`、`Enum_ItemColor_Red`、`Enum_AccentPreset_Red`）。App の `EnumNames` がここから引く。**OS で名前が変わる値だけ `Enum_<型名>_<値>_Mac` を足す**と、`EnumNames.Of` が macOS ではそれを優先する（無ければ基本キー = Windows の名前。`_Win` は作らない）。例: `Enum_FolderOpenTarget_ExistingTab_Mac`（2026-10-08 設計・未実装）。`StringsTests` の「全 enum 値のキーがある」確認は基本キーで行い、`_Mac` キーは両 resx にあることだけ見る。
- 演出名は **`Effect_<演出 ID そのまま>`**（`Effect_boardShow`。ID は camelCase なので PascalCase の例外。`EffectCatalog` の ID から `"Effect_" + id` で引けるようにするため）。
- 件数つきは `_One` / `_Other`。OS 別は `_Win` / `_Mac`。書式引数は `{0}` 形式の番号付き（名前付きにしない）。各引数の意味は resx の `comment` に書く。
- ログ（`AppLog`）・`Commit` の理由文字列・例外の開発者向けメッセージはリソースにしない（日本語のまま。SPEC §11）。

## 5. 製品名と HUD の装飾語（2026-10-04）

- **製品名 `CPFileLauncher`** は両言語とも英字のまま、文言に**直接書く**（`{0}` で埋めない。製品名が変わることはもう無い前提。SPEC §13.2 T1）。副題「CyberPunk FileLauncher」も同じ。対象: `About_*`、`Tray_*`（ツールチップ「CPFileLauncher」、「CPFileLauncher — triggers are disabled」）、`Menu_Quit_Mac`（"Quit CPFileLauncher"）、`Load_*`（破損 / 新しい版の文）、`Permission_*`（アクセシビリティの手順）、`Archive_*`（インポートの確認・「CPFileLauncher がエクスポートした zip」）、`Common_RestartNote`。コード側のタイトル接頭辞「CPFileLauncher — 」は `AppInfo.TitlePrefix`。
- **HUD の装飾語は翻訳しない**（参考画像の大文字英字ラベルの流儀。SPEC §3.9）: ステータス行の `PAGE` / `ITEMS`（Core `StatusLine` の固定文字列。リソースは置かない）、ダイアログのタグ `CFG` / `ITEM` / `PAGE` / `AUTH` / `SYS` / `CONFIRM` / `INFO`（`Hud_Tag_*`）、トーストの状態コード `>` / `!`。タグと状態コードはリソースに置く（lint と一貫性のため）が、`Strings.ja.resx` も同じ値にする。文ではないのでピリオドを付けない。大文字のまま（sentence case の例外）。
- 時刻は `HH:mm` / `HH:mm:ss`（24 時間）、日付は `yyyy/MM/dd`（いずれも `InvariantCulture`）で固定し、OS の書式には従わない（HUD の慣例。SPEC §3.9）。`PAGE` / `ITEMS` は Core の `StatusLine` が返す固定文字列で、リソース `Hud_Page` / `Hud_Items` は作らなかった（翻訳しない語なので lint の対象外。実装 2026-10-04）。
