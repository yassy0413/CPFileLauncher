# IconSpike — アイコン取得スパイク（Windows）

SPEC §5.1（アイコン）/ §9.2（icons/ キャッシュ）/ §10.3（IIconProvider）/ §11（表示遅延）の未検証部分を潰す使い捨てプロジェクト。
macOS（NSWorkspace.iconForFile）は Mac 環境が用意でき次第、別途。

## 検証対象

| # | 内容 |
|---|---|
| J1 | 取得方式の比較: `IShellItemImageFactory.GetImage`（任意サイズ）と `SHGetFileInfo` + システムイメージリスト（16/32/48/256 固定）の画質 |
| J2 | alpha の扱い（Factory の HBITMAP は乗算済み、HICON は straight）。暗い背景で縁ににじみが出ないか |
| J3 | `.lnk`: .lnk 自身のアイコン（矢印オーバーレイの有無・カスタムアイコン）とリンク先アイコンのどちらを使うか |
| J4 | 画像・フォルダ: 「アイコンのみ」OFF でサムネイルになるか、ランチャーとしてどちらが良いか |
| J5 | 存在しないパス（拡張子から取得）、URL（既定ブラウザの exe）、特殊フォルダ（`::{GUID}`） |
| J6 | 速度: 抽出・png 保存・キャッシュ読込（ポップアップ 100ms の前提） |
| J7 | 専用 STA スレッドで固まらないか |

## 実行

```
cd src/spikes/IconSpike
dotnet run                 # GUI
dotnet run -- --selftest   # サンプルを取得 → キャッシュ再取得 → bin/.../selftest.log に書いて終了
```

## 操作

1. 「サンプルを追加」→ exe / フォルダ / ドライブ / 特殊フォルダ / 存在しないパス / URL / スタートメニューの .lnk 数件 / ピクチャ内の画像 1 枚 のタイルが出る。
   1 項目につき Factory と SHGetFileInfo のタイルが並ぶ（.lnk はさらに「→ リンク先」のタイル）。
2. サイズを 32 / 48 / 64 / 96 に切り替えて見比べる（J1）。96 は SHGetFileInfo だと 256(jumbo) を縮小表示。
3. 「タイル背景を暗く」で縁を確認（J2）。
4. 「アイコンのみ」OFF で画像・フォルダの見え方を確認（J4）。
5. 自分のよく使うファイル・ショートカットを枠にドロップ。
