# s4na VPM Repository

VRChat Creator Companion (VCC) 用パッケージリポジトリ

## Add to VCC

以下のURLをVCCに追加してください:

<https://s4na.github.io/vpm/index.json>

または以下のボタンから追加:

[![Add to VCC](https://img.shields.io/badge/Add%20to-VCC-blue)](https://s4na.github.io/vpm/)

## Packages

| Package | Description |
|---------|-------------|
| com.s4na.utils | Utility scripts collection |
| com.s4na.avatar-essentials | VRChat アバター向けの定番ツールをまとめて導入 |
| com.takuho.tao | [TAO: Takuho's Avatar Optimizer](https://takuhotech.github.io/TakuhoAvatarOptimizer/vpm.html)（外部配布） |

s4na Avatar Essentials 1.1.0 以降をインストールまたは更新すると、TAO も依存パッケージとしてインストールされます。TAO だけを追加することもできます。ZIP は公式リリースから取得し、このリポジトリでは再配布しません。解決に必要な SDK や NDMF が見つからない場合は、VCC にそれぞれの配布元を追加してください。

## Setup (for maintainers)

1. GitHub Pages を有効化: Settings > Pages > Source を "GitHub Actions" に設定
2. main ブランチに push すると自動的にパッケージリストがビルドされます

## Adding a New Package

1. `Packages/com.your.package/` ディレクトリを作成
2. `package.json` を追加（VPM形式）
3. スクリプトを追加
4. main に push

外部パッケージを追加・更新する場合は、配布元の VPM JSON で確認したバージョン情報を `external-packages.json` に登録します。公式 ZIP の URL と `zipSHA256` は変更せず、リリースの情報と照合してください。

## License

MIT
