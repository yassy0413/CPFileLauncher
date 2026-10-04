# M3 — アイテム登録・アイコン・起動（Windows）

目標: Explorer / ブラウザから盤面へ D&D で登録でき、正しいアイコンが出て、クリックで起動できる。

移植元: `src/spikes/IconSpike`（アイコン取得・png キャッシュ。移植したらスパイクは削除）、`src/spikes/PlatformSpike`（D&D 受信）

## 状況（2026-10-02）

下記タスクは実装済み・ユーザー確認待ち（テスト 57 件）。確認手順:
1. Explorer から空きスロットへ: ファイル / 複数件 / フォルダ / .lnk / exe をドロップ → 正しいアイコンで登録され、再起動後も残る
2. ブラウザのアドレスバーやリンクを空きスロットへ → URL アイテム（既定ブラウザのアイコン）
3. 画像ファイル → サムネイル
4. クリックで起動（ファイル・フォルダ・アプリ・URL）、起動後に盤面が閉じる
5. アイテムの上へファイルをドロップ → そのアプリで開く。フォルダアイテムへ → コピー、Shift で移動（確認あり）。同名があるとき: 同一内容ならスキップ、別内容なら `名前 2.ext` で追加され、完了トーストに件数が出る（下の「追加タスク」の後で確認）
6. 登録後に元ファイルを削除・リネーム → グレーアウト + 赤い「!」、クリックでトースト
7. 空きが足りない複数ドロップ → 行追加の確認

## タスク

- [ ] D&D 登録（SPEC §6.1）: ファイル / フォルダ / .lnk（リンク先を解決して登録し `linkPath` に元を保持）/ URL テキスト / パス文字列。複数同時は右→次行へ順に空きへ。D&D API は Avalonia の新 API（`DataTransfer`）
- [ ] アイテム種別の判定（file / folder / app / url / command）と既定の表示名
- [ ] `Platform.Windows`: `IIconProvider`（SPEC §5.4 の規則。専用 STA スレッド）
- [ ] Core: アイコンキャッシュ（`icons/`、キー = パス + px + 方式 + 更新時刻）。取得完了まで汎用アイコン
- [ ] `Platform.Windows`: `IShellService`（ShellExecuteEx。引数・作業フォルダ・起動方法・管理者実行・環境変数展開・%1）、格納フォルダを開く
- [ ] 起動失敗のトースト通知（SPEC §5.2）、存在しないパスのグレーアウト＋警告（SPEC §5.1）
- [ ] Drop-to-Open（SPEC §5.3）: アイテム上へのドロップでそのアプリで開く / フォルダへコピー・移動
- [ ] 起動後に閉じる（設定 `CloseOnLaunch`）
- [ ] テスト: 種別判定、空きスロット配置、%1 展開、キャッシュキー

## 追加タスク（2026-10-03）: Drop-to-Open の競合規則（SPEC §5.3。Windows / macOS 共通）

ユーザー判断: 同名でも内容が違うファイルは入れたい。Windows の `SHFileOperation`（OS の競合ダイアログ）はやめ、両 OS を同じ規則に揃える。

- [x] Core に `FileTransfer`（`src/FileLauncher.Core/Items/` あたり。OS 非依存、.NET の File / Directory API のみ）:
  - `Task<TransferResult> TransferAsync(string folder, IReadOnlyList<string> sources, bool move, CancellationToken ct)`。`TransferResult(int Added, int Renamed, int Skipped, int Failed)`（Added = 同名が無くそのままの名前で入れた件数、Renamed = 連番の別名で入れた件数）。失敗の理由はログ（`Trace` / `AppLog`）へ。
  - 候補名の生成 `CandidateNames(folder, sourceName)`: `名前.ext`, `名前 2.ext`, `名前 3.ext`, …（拡張子は `Path.GetExtension`。フォルダは拡張子なし扱い）。
  - 同一判定 `AreIdentical(src, dest)`: ファイルはサイズ一致 → SHA-256 のストリーム比較。フォルダは相対パスの集合一致 + 各ファイルの同一判定。シンボリックリンクはリンク先文字列の比較。ドロップ元と転送先が同じフルパスならスキップ扱い。
  - 判定の順序: 候補を順に見て、存在して同一 → Skipped（移動でも元は消さない）、存在して別内容 → 次の候補、存在しない → ここへ転送（最初の候補なら Added、2 番目以降なら Renamed）。
  - 移動: 同一ボリューム（`Path.GetPathRoot` が一致、Mac は `/Volumes/` 配下の先頭要素で判定）なら `File.Move` / `Directory.Move`、別ボリュームならコピーしてから元を削除（コピー失敗なら元は残す）。フォルダのコピーは再帰。
- [x] `IShellService.TransferInto` を削除し、`WindowsShellService` / `MacShellService` の実装（SHFileOperation 版・スキップ版）も削除。`BoardEditor.DropOntoItemAsync` は `FileTransfer.TransferAsync` を `await`（`Task.Run` でバックグラウンド）し、完了時に `Dispatcher.UIThread` でトースト: 「『<フォルダ名>』へ N 件コピー（移動）しました（別名 n 件、同一のため n 件スキップ）」。n が 0 の括弧内項目は省く。Failed > 0 なら「n 件失敗（ログ参照）」を加え `error: true`。全件スキップでもトーストを出す。文言のリソース化は M5 ステップ 5 で済み（`Toast_Transfer*` キー、単複は `Loc.Count`、断片は `Loc.Join`。2026-10-04）。
- [x] テスト（Core、一時フォルダで）: 同名なし → Added / 同名・同一 → Skipped・元が残る / 同名・別内容 → `a 2.txt` / `a.txt` と `a 2.txt` が別内容で既存 → `a 3.txt` / `a 2.txt` が同一 → Skipped / フォルダ同士の同一・別内容 / 移動で元が消える（同一ボリューム）/ 候補名の列（`a.tar.gz` → `a.tar 2.gz`、拡張子なし、フォルダ）。
- [ ] Windows でユーザー確認: 確認手順 5 の同名ケース。Explorer の進捗ダイアログが出なくなることは仕様どおり（SPEC §5.3、進捗表示は §12）。

## 完了条件

ユーザーがよく使うファイル・アプリ・URL を登録して起動できる。
