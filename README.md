# ExifFrameClip

ExifTool( https://exiftool.org/ )をタグ取得に使用するExifFrameライクな画像生成ツール  
画像ビューワーの実行からExif情報を付加した画像を生成しクリップボードにコピーする為のもの

実行必須要件: ExifToolにPATHが通っている or カレントディレクトリにExifToolが存在する

# Build
+ ビルド済みデータを使用する
https://github.com/eajtciv/ExifFrameClip/releases  

+ monoを使用する
> mcs ./ExifFrameClip.cs  ./JsonUtil.cs -reference:System.Drawing.dll -reference:System.Windows.Forms.dll -win32icon:./res/icon.ico -out:./bin/ExifFrameClip.exe

# その他仕様
ExifFrameClip.exe から ExifFrameClip[overlay.json].exe のように[]で実行ファイル名に設定ファイル名を指定して使用することができます  
設定ファイル詳細はWikiを参照

# 設定ファイル別サンプル画像  
+ default.json
<img src="https://raw.githubusercontent.com/eajtciv/ExifFrameClip/main/sample-image/default.jpg" width="50%">

+ draw-text.json
<img src="https://raw.githubusercontent.com/eajtciv/ExifFrameClip/main/sample-image/draw-text.jpg" width="50%">

+ exifframe-style.json
<img src="https://raw.githubusercontent.com/eajtciv/ExifFrameClip/main/sample-image/exifframe-style.jpg" width="50%">

+ overlay.json
<img src="https://raw.githubusercontent.com/eajtciv/ExifFrameClip/main/sample-image/overlay.jpg" width="50%">

+ text.json
> NIKON D5600  
> AF-P DX Nikkor 70-300mm f/4.5-6.3G ED VR  
> 135mm 1/2000s F4.8 ISO400  
> 2022-08-28
