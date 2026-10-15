# Basketball VR

Meta Quest 向けのバスケットボール VR ゲーム（Unity 6 / URP / Meta XR SDK）。
60秒の制限時間内に、ボールを掴んでゴールに投げ入れた数を競う。

## 操作
| | 素手 (Quest ハンドトラッキング) | コントローラー (Quest) | PC (エディタ) |
|---|---|---|---|
| 開始 | 左手でピンチ（親指と人差し指をつまむ） | B ボタン | B キー |
| 掴む | 右手のひらをボールに触れる | 右トリガーを握る | Space を押す |
| 投げる | 手首のスナップで自動的に指先から離れる | 手首のスナップ（トリガーを離しても投げられる） | マウスを動かしながら Space を離す |
| タイトルへ戻る | 左手のピンチを 3 秒続ける | A ボタン 3 秒長押し | A キー 3 秒長押し |

## Git に入れるもの
`Assets/` `Packages/` `ProjectSettings/` `.gitignore` `.mcp.json` `README.md`

`Library/`（数 GB）は Unity が自動生成するキャッシュなので入れない（`.gitignore` 済み）。
Meta XR SDK などのパッケージは `Packages/manifest.json` に書かれているだけで、開いたときに自動でダウンロードされる。

## 空のリポジトリへ移す手順
1. GitHub で空のリポジトリを作り、クローンする
2. このフォルダから上記のファイル・フォルダをクローン先にコピーする
3. クローン先を Unity Hub で開く（初回は `Library/` の再生成で時間がかかる）
4. `git add .` → `git commit` → `git push`
