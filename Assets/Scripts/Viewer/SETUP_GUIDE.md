# Package Viewer Scene セットアップガイド

## 概要
生成されたパッケージからモデルと説明文を読み込んで表示し、説明を読み上げるシーンです。

## シーンセットアップ手順

### ステップ 1: 新規シーン作成
1. Unity エディタで新規シーンを作成: `Assets/Scenes/PackageViewer.unity`
2. `Main Camera` を保持（デフォルト）

### ステップ 2: Canvas を作成
1. `Hierarchy` で右クリック → `UI` → `Canvas`
2. Canvas の設定
   - Canvas Scaler: `UI Scale Mode` → `Scale With Screen Size`
   - Reference Resolution: `1920 x 1080`

### ステップ 3: UI 要素を追加

#### 3.1 Project Name Panel
- Canvas の子に `Panel` を作成 → 名前: `ProjectNamePanel`
- 以下の Text を追加:
  ```
  ProjectNameText (Text)
  - Alignment: Upper Left
  - Font Size: 40
  - Color: White
  ```

#### 3.2 Description Panel
- Canvas の子に `Panel` を作成 → 名前: `DescriptionPanel`
- レイアウト: Rect Transform で配置（上部）
- 以下を追加:
  ```
  DescriptionText (Text)
  - Alignment: Upper Left
  - Font Size: 28
  - Color: White
  Text: "説明文がここに表示されます"
  ```

#### 3.3 Control Panel
- Canvas の子に `Panel` を作成 → 名前: `ControlPanel`
- レイアウト: 下部に配置

**ドロップダウン1: Part Selector**
- 子に `Dropdown` を追加 → 名前: `PartDropdown`
- Dropdown 設定:
  ```
  Item Text Component: Text
  Template: Dropdown List
  ```

**ドロップダウン2: Description Selector**
- 子に `Dropdown` を追加 → 名前: `DescriptionDropdown`

**ボタン群**
- `Button` × 4 を作成:
  1. `PreviousButton` - テキスト: "< 前のパーツ"
  2. `NextButton` - テキスト: "次のパーツ >"
  3. `PlayAudioButton` - テキスト: "音声再生"
  4. その他操作ボタン

### ステップ 4: スクリプトを割り当て

#### 4.1 Canvas に PackageViewer をアタッチ
1. Canvas を選択
2. `Inspector` → `Add Component` → `PackageViewer`
3. 以下を設定:
   ```
   Package Folder Path: (空のまま - 自動検出)
   Model Container: (空のまま - 今後実装)
   Project Name Text: ProjectNameText
   Description Text: DescriptionText
   Previous Button: PreviousButton
   Next Button: NextButton
   Play Audio Button: PlayAudioButton
   Part Dropdown: PartDropdown
   Description Dropdown: DescriptionDropdown
   WAV Player: (自動作成)
   ```

### ステップ 5: パッケージパスの設定

**オプション A: 自動検出（推奨）**
- PackageViewer スクリプトは以下をチェックします:
  ```
  Application.persistentDataPath/Packages
  StreamingAssets/Package
  ```
- 現在のRuntime変換UIは、実運用パスとして以下へ出力します:
  ```
  Application.persistentDataPath/Packages/{ProjectName}/Package
  ```

**オプション B: 手動設定**
- PackageViewer の `packageFolderPath` に直接パスを入力:
  ```
  {Application.persistentDataPath}\Packages\{ProjectName}\Package
  ```

## 使用方法

### 実行時
1. Play ボタンを押す
2. Viewer が自動的にパッケージを読み込む
3. UI から:
   - `Part Dropdown` / 前後ボタン: パーツを切り替え
   - `Description Dropdown`: 説明文を選択
   - `Play Audio Button`: 説明文の音声を再生

## 実装済み機能

✅ **パッケージ読み込み**
- manifest.json（基本情報）
- project_metadata.json（詳細メタデータ）

✅ **UI 表示**
- プロジェクト名
- パーツリスト（ドロップダウン）
- 説明文表示
- ナビゲーション（前後ボタン）

✅ **音声再生**
- WAV ファイルの PCM デコード
- 16bit モノラル/ステレオ対応
- AudioClip への動的変換

## 今後の実装予定

✅ **モデル表示機能**
- manifest.json のAssetBundle情報を参照
- PC Viewerでは `model_windows.bundle` を優先ロード
- Quest/Androidでは `model_android.bundle` をロードする想定

✅ **ハイライト機能**
- 説明中のパーツを視覚的にハイライト

⏳ **UI 改善**
- キーボード/ゲームパッド操作対応
- タッチ操作対応（VR・AR）

## トラブルシューティング

### "パッケージの読み込みに失敗しました"
1. コンソール出力を確認
2. パッケージパスが正しいか確認
3. `project_metadata.json` が存在するか確認

### 音声が再生されない
1. `Application.persistentDataPath/Packages/{ProjectName}/Package/Audio/` にファイルが存在するか確認
2. ファイル名の文字化け（Shift-JIS）を確認
3. `WAVPlayer` の `PlayWAV()` メッセージを確認

## ファイル構成

```
Assets/Scripts/Viewer/
├── PackageMetadata.cs      - JSON データ構造定義
├── PackageReader.cs        - パッケージ読み込み
├── PackageViewerScene.cs   - UI・シーン管理
└── WAVPlayer.cs            - WAV 再生エンジン
```
