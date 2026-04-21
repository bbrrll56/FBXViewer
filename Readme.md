# FBX Viewer / Runtime Converter

FBXモデルに説明文と音声を紐づけ、Meta Quest上で動作するMR製品説明アプリ用パッケージを生成・閲覧するプロジェクトです。  
最終的には、ユーザーがUnity EditorやVoiceVoxを直接操作せず、ビルド済みPCアプリのUIだけで変換からQuestへのデータ移行まで完了できる構成を目指します。

## コンセプト

FBXファイルとパーツ情報を入力するだけで、Meta Quest上で動作するMR製品説明アプリを自動生成するシステムです。

目標は、製品説明用の3Dモデル、説明文、説明音声、パーツハイライトをひとつの変換フローでまとめて生成し、Quest側では生成済みパッケージを読み込んで表示・再生するだけの状態にすることです。

最終的な運用イメージ:

- ユーザーはFBXを投入する
- ビルド済みPCアプリでパーツを選び、パーツ名と説明文を入力する
- PCアプリがVoiceVoxとUnity Editorを裏側で自動起動/制御する
- Unity Editor側でしか生成できないAssetBundle等を自動生成する
- Quest/Viewer向けパッケージを出力する
- PCアプリがQuestへのデータ移行を行う
- Quest MRアプリはパッケージを読み込み、3Dモデル、説明文、音声、パーツハイライトを表示する

目指す特徴:

- ノーコード運用
- FBX投入と説明文入力だけで利用可能
- 説明文と音声を1対1で対応付け
- 1つのパーツに複数メッシュを紐づけ可能
- Quest側は表示専用に近い構成

現時点では、PCビルドアプリからUnity Editorを自動操作する部分、Questへの自動転送、Quest実機向けRuntimeロードは未完成です。現在の実装は、Unity Editor上で変換フローとViewer挙動を確認し、Quest向けAndroid AssetBundleをパッケージに含める段階です。

## 概要

このプロジェクトは、最終的には以下の役割分担で動作する想定です。

1. PCビルドアプリ
   - ユーザー向けUIを提供
   - FBX投入、パーツ選択、パーツ名、説明文入力を受け付ける
   - VoiceVoxの起動/接続確認を行う
   - Unity Editor変換ジョブを起動する
   - 変換進捗と結果を表示する
   - 完成したPackageをQuestへ転送する

2. Unity Editor変換プロセス
   - PCビルドアプリから自動起動される内部変換エンジン
   - FBX importを行う
   - FBX内のメッシュ/オブジェクトを検出する
   - AssetBundleを生成する
   - Unity Editorでしか生成できないアセットを生成する
   - metadata、audio、model bundleを含むPackageを構築する

3. VoiceVox
   - PCビルドアプリから起動/監視される想定
   - 説明文ごとのWAV音声を生成する

4. Quest MRアプリ / Viewer
   - 生成済みPackageを読み込む
   - 3Dモデル、パーツ、説明文を表示する
   - 説明音声を再生する
   - 対象パーツをハイライトする

現在のUnityプロジェクト内には、開発確認用として以下の画面があります。

- `EditorScene`
  - Unity Editor上で変換フローを手動確認する暫定UI

- `PackageViewer`
  - 生成済みPackageをPC上で確認するViewer

## 現在の実装状況

現在は最終運用のうち、Unity Editor変換プロセスとPC Viewer確認を先に実装しています。  
ユーザー向けのPCビルドアプリからUnity Editorをバッチ起動して変換する仕組みは、今後追加する予定です。

### Runtime変換UI

`EditorScene` には `RuntimeConversionRoot` があり、`RuntimeConversionOverlay` が変換フロー全体を制御します。

これは最終運用におけるユーザー向けUIではなく、現段階で変換フローをUnity Editor上で確認するための暫定UIです。将来的には、ビルド済みPCアプリが変換ジョブを作成し、Unity Editor側のバッチ処理を自動実行する形に置き換える想定です。

画面構成:

- `RuntimeConversionStartView`
  - 開始ボタンのみのスタート画面

- `RuntimeConversionFbxSelectionView`
  - FBX一覧を表示
  - 一覧更新
  - 選択したFBXを開く
  - スタート画面へ戻る

- `RuntimeConversionPartEditorView`
  - 読み込んだFBX名を表示
  - 検出メッシュ一覧をトグルで表示
  - パーツ名を入力
  - 説明文入力欄を追加/削除
  - 選択メッシュを保存
  - 保存済みパーツ一覧を表示
  - 変換進捗を表示

説明文入力欄は `Assets/Prefab/Description.prefab` をテンプレートとして動的生成されます。

### FBX入力

デフォルト入力フォルダ:

```text
Application.persistentDataPath/Input
```

`RuntimeConversionOverlay` はこのフォルダ直下の `.fbx` を一覧化します。

実運用では、PCビルドアプリ側の作業領域として `Application.persistentDataPath/Input` を入力元にします。ただしFBXプレビューやAssetBundle生成にはUnity Editorの `AssetDatabase` が必要なため、変換時には内部キャッシュとして `Assets/Scripts/Converter/ImportedInput` にFBXをコピーし、Unity Editorが読み込める状態にします。

注意: `Application.persistentDataPath/Input` と `Application.persistentDataPath/Packages` が入出力先です。

### パーツ登録

1つのパーツには以下を登録します。

- `partName`
  - 任意のパーツ表示名

- `descriptions`
  - 1件以上の説明文
  - 説明文ごとに1つの音声ファイルを生成

- `selectedObjectNames`
  - FBX内で選択したオブジェクト名
  - 1パーツに複数メッシュを紐づけ可能

パーツ保存時の条件:

- パーツ名が空でない
- 説明文が1件以上ある
- 空欄の説明入力欄が残っていない
- メッシュが1つ以上選択されている

### 選択プレビュー

パーツ編集画面では、メッシュ選択中に以下のプレビューを行います。

- 選択中メッシュを黄色系で強調
- 非選択メッシュを半透明化
- 選択解除や画面遷移時に元のマテリアル状態へ復元

URP系マテリアルの `_BaseColor`、`_Surface`、`_Blend`、`_SrcBlend`、`_DstBlend`、`_ZWrite` などを考慮して復元用状態を保持しています。

### 音声生成

音声生成は `AudioGenerationPipeline` と `VoiceVoxGenerator` が担当します。

最終運用では、VoiceVox自体の起動/監視はPCビルドアプリ側で行い、Unity Editor変換プロセスは接続済みVoiceVoxへ音声生成リクエストを投げる想定です。現在はUnity Editor上の変換フローから接続確認と音声生成を行っています。

仕様:

- 1説明文につき1つの `.wav` を生成
- ファイル名は `{partName}_{index}.wav`
- `description[0]` は `{partName}_0.wav` に対応
- デフォルトspeaker IDは `1`
- 変換開始時にVoiceVox接続確認を実行

VoiceVoxサーバーが起動している必要があります。

### 出力

Runtime変換UIのデフォルト出力先:

```text
Application.persistentDataPath/Packages/{ProjectName}
```

出力構成:

```text
Application.persistentDataPath/Packages/{ProjectName}/
  Audio/
    {partName}_0.wav
    {partName}_1.wav
  project_metadata.json
  Package/
    Bundle/
      model_android.bundle
      model_windows.bundle
    Audio/
      *.wav
    Model/
      *.fbx
    Metadata/
      project_metadata.json
    manifest.json
```

Viewerが実際に読む対象は `Application.persistentDataPath/Packages/{ProjectName}/Package` です。将来的なPCビルドアプリとQuest転送運用を見越して、実運用パスは `Application.persistentDataPath` 基準に寄せています。

`Package/Bundle/model_android.bundle` はAndroid/Quest向けに `BuildTarget.Android` で生成されます。PC Viewer確認用には `model_windows.bundle` も `BuildTarget.StandaloneWindows64` で生成します。FBX本体も `Package/Model` にコピーしていますが、Viewerはmanifestに載っているAssetBundleを優先してロードします。

## メタデータ形式

`project_metadata.json` の主な構造:

```json
{
  "projectName": "MCV",
  "fbxPath": "{ProjectRoot}/Assets/Scripts/Converter/ImportedInput/MCV.FBX",
  "partCount": 2,
  "parts": [
    {
      "partName": "主砲",
      "descriptionCount": 2,
      "descriptions": [
        "これは主砲です。",
        "長距離射撃に使います。"
      ],
      "audioFiles": [
        "{Application.persistentDataPath}/Packages/MCV/Audio/主砲_0.wav",
        "{Application.persistentDataPath}/Packages/MCV/Audio/主砲_1.wav"
      ],
      "selectedObjectNames": [
        "Turret",
        "Barrel"
      ]
    }
  ]
}
```

Viewer側のハイライトは、原則として `selectedObjectNames` を使ってFBX内のTransformを探します。`selectedObjectNames` が空の場合のみ `partName` をフォールバックとして使います。

## Package Viewer

`PackageViewer` は `manifest.json` と `Package/Metadata/project_metadata.json` を読み込みます。モデルはmanifest内のAssetBundleを優先し、PC Viewerでは `model_windows.bundle`、Quest/Androidでは `model_android.bundle` を選択します。AssetBundle読み込みに失敗した場合のみ、Unity Editor上では `fbxPath` からの直接ロードにフォールバックします。

主な機能:

- プロジェクト名表示
- パーツDropdown
- 説明文Dropdown
- 前/次パーツボタン
- Playボタンによる音声再生
- 選択中説明文から、そのパーツの最後の説明文まで連続再生
- 再生開始時に対象パーツを約2秒点滅ハイライト
- 対象外パーツを半透明表示

パッケージ配置:

- 実運用: `Application.persistentDataPath/Packages/{ProjectName}/Package`
- 開発用フォールバック: `Application.streamingAssetsPath/Package`

`PackageViewer` の `packageFolderPath` が空の場合、上記の順番で `manifest.json` と `Metadata/project_metadata.json` を持つパッケージを探します。

現在の制約:

- RuntimeビルドでのFBX直接ロードは未実装
- PC Viewerは `model_windows.bundle` を優先ロード
- Quest Viewerでは `model_android.bundle` をロードする想定
- Runtime変換UIの出力を明示的に確認する場合は、`packageFolderPath` に `Application.persistentDataPath/Packages/{ProjectName}/Package` を指定できます

## 主要ファイル

```text
Assets/Scripts/Converter/
  RuntimeConversionOverlay.cs              Runtime変換フロー全体
  RuntimeConversionStartView.cs            スタート画面UI
  RuntimeConversionFbxSelectionView.cs     FBX選択画面UI
  RuntimeConversionPartEditorView.cs       パーツ編集画面UI
  RuntimeConversionDescriptionInputView.cs 説明文入力欄
  FBXConverter.cs                          変換処理の中心
  FBXImporter.cs                           Editor上でのFBXロード
  PartDetector.cs                          FBX内パーツ/メッシュ検出
  QuestPackager.cs                         Packageフォルダ生成

Assets/Scripts/Audio/
  AudioGenerationPipeline.cs               説明文から音声生成
  VoiceVoxGenerator.cs                     VoiceVox API呼び出し

Assets/Scripts/Viewer/
  PackageViewer.cs                         変換済みパッケージ表示
  PackageReader.cs                         メタデータ読み込み
  WAVPlayer.cs                             WAV再生
  PackageMetadata.cs                       Viewer側メタデータ定義

Assets/Prefab/
  Start.prefab
  FBXList.prefab
  PartEditorPanel.prefab
  Description.prefab
  Mesh.prefab
  SavedPart.prefab
```

## セットアップ手順

現在の暫定確認手順です。最終運用では、ユーザーはUnity Editorを開かず、ビルド済みPCアプリのUIから以下の処理を実行します。

1. VoiceVoxを起動する
2. 変換したいFBXを `Application.persistentDataPath/Input` に置く
3. Unityで `Assets/Scenes/EditorScene.unity` を開く
4. Playを押す
5. StartからFBX一覧へ進む
6. FBXを選択して開く
7. パーツ名、説明文、対象メッシュを登録する
8. Exportを押して変換する
9. 出力された `Application.persistentDataPath/Packages/{ProjectName}/Package` をPackageViewerで開く

## 注意点

- 旧 `Assets/Converer` / `Assets/Conveter` 直下のInput/Outputは廃止しています。FBX投入とPackage出力は `Application.persistentDataPath` 側を使います。
- `Description.prefab` の `RuntimeConversionDescriptionInputView.validationText` は未設定で問題ありません。入力文字表示用のTextを入れると、入力文字が消える原因になります。
- `RuntimeConversionOverlay` はアクティブシーン名が `EditorScene` でない場合、自身を破棄します。
- `RuntimeConversionOverlay` の各View参照、または各View内部の必須UI参照が欠けると起動時にエラーになります。
- 最終コンセプトでは、PCビルドアプリがUnity Editorをバッチ実行し、ユーザーはUnity Editorを操作しません。現在はまだUnity Editor上のRuntime UIで変換操作を行います。
- 元の出力案ではAssetBundle、`.mp3`、TextMeshProフォント生成を想定していました。現在の実装はAndroid AssetBundle、FBXコピー、正式音声形式としての`.wav`生成、JSONメタデータ、Packageフォルダ生成が中心です。
- 生成済み出力フォルダや `.meta` はUnity上で作成されるため、コミット対象にするかは運用方針に合わせてください。

## 今後の課題

- PCビルドアプリ側の変換ジョブJSON定義
- Unity Editor側のバッチ変換エントリポイント実装
- PCビルドアプリからUnity Editorを自動起動する処理
- PCビルドアプリからVoiceVoxを自動起動/監視する処理
- QuestへのPackage自動転送
- Quest Viewer側のAndroid AssetBundleロード対応
- RuntimeビルドでのFBX/モデルロード方式の確定
- AssetBundle以外にAddressables等へ移行する必要があるかの検討
- TextMeshPro日本語フォント生成/同梱方針の決定
- VoiceVoxサーバー未起動時のUIエラー表示改善
- 既存スクリプトコメントの文字化け修正
