@PATH ..\..\Bin;%PATH%

:: SchemaManipulator
@if not exist SchemaManipulator\Data @md SchemaManipulator\Data
SchemaManipulator.exe /loadtype:Schema /t2b:Data\WorldData.tree,SchemaManipulator\Data\WorldData.bin,World /b2t:SchemaManipulator\Data\WorldData.bin,SchemaManipulator\Data\WorldData.Binary.tree,World /t2b:SchemaManipulator\Data\WorldData.Binary.tree,SchemaManipulator\Data\WorldData.Binary.bin,World

:: VB.Net
@if not exist VB\Data @md VB\Data
if exist VB\Bin\DataConv.exe (
  VB\Bin\DataConv.exe /t2b:Data\WorldData.tree,VB\Data\WorldData.bin /b2t:VB\Data\WorldData.bin,VB\Data\WorldData.Binary.tree /t2b:VB\Data\WorldData.Binary.tree,VB\Data\WorldData.Binary.bin
)

:: C#
@if not exist CSharp\Data @md CSharp\Data
if exist CSharp\Bin\DataConv.exe (
  CSharp\Bin\DataConv.exe /t2b:Data\WorldData.tree,CSharp\Data\WorldData.bin /b2t:CSharp\Data\WorldData.bin,CSharp\Data\WorldData.Binary.tree /t2b:CSharp\Data\WorldData.Binary.tree,CSharp\Data\WorldData.Binary.bin
  CSharp\Bin\DataConv.exe /t2j:Data\WorldData.tree,CSharp\Data\WorldData.json /j2t:CSharp\Data\WorldData.json,CSharp\Data\WorldData.Json.tree /t2j:CSharp\Data\WorldData.Json.tree,CSharp\Data\WorldData.Json.json
)

:: Java
@if not exist Java\Data @md Java\Data
if exist Java\bin\Program.class (
  @pushd Java\bin\
  java Program ..\..\SchemaManipulator\Data\WorldData.bin ..\Data\WorldData.bin
  @popd
)

:: C++2011
@if not exist CPP\Data @md CPP\Data
if exist CPP\Bin\DataCopy.exe (
  CPP\Bin\DataCopy.exe SchemaManipulator\Data\WorldData.bin CPP\Data\WorldData.bin
)

:: Haxe
@if not exist Haxe\Data @md Haxe\Data
if exist Haxe\bin\DataCopy.n (
  @pushd Haxe
  Run.cmd
  @popd
)

:: ActionScript
@if not exist ActionScript\Data @md ActionScript\Data
if exist ActionScript\bin\DataCopy.swf (
  @pushd ActionScript
  Run.bat
  @popd
)

@pause
