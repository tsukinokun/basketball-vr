# Basketball VR

Meta Quest 向けのバスケットボール VR ゲーム（Unity 6 / URP / Meta XR SDK）。
60秒の制限時間内に、ボールを掴んでゴールに投げ入れた数を競う。

## 操作
| | VR (Quest) | PC (エディタ) |
|---|---|---|
| 開始 | B ボタン | B キー |
| 掴む / 投げる | 右トリガーを握る / 振って離す | Space を押す / マウスを動かしながら離す |
| タイトルへ戻る | A ボタン 3 秒長押し | A キー 3 秒長押し |

## Git に入れるもの
`Assets/` `Packages/` `ProjectSettings/` `.gitignore` `.mcp.json` `README.md`

`Library/`（数 GB）は Unity が自動生成するキャッシュなので入れない（`.gitignore` 済み）。
Meta XR SDK などのパッケージは `Packages/manifest.json` に書かれているだけで、開いたときに自動でダウンロードされる。

## 空のリポジトリへ移す手順
1. GitHub で空のリポジトリを作り、クローンする
2. このフォルダから上記のファイル・フォルダをクローン先にコピーする
3. クローン先を Unity Hub で開く（初回は `Library/` の再生成で時間がかかる）
4. `git add .` → `git commit` → `git push`
