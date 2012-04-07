@PATH ..\..\Bin;%PATH%

:: SchemaManipulator
@if not exist SchemaManipulator\Data @md SchemaManipulator\Data
SchemaManipulator.exe /loadtype:Schema /t2b:Data\WorldData.tree,SchemaManipulator\Data\WorldData.bin,World /b2t:SchemaManipulator\Data\WorldData.bin,SchemaManipulator\Data\WorldData2.tree,World /t2b:SchemaManipulator\Data\WorldData2.tree,SchemaManipulator\Data\WorldData2.bin,World

:: VB.Net
@if not exist VB\Data @md VB\Data
if exist VB\Bin\DataConv.exe (
  VB\Bin\DataConv.exe /t2b:Data\WorldData.tree,VB\Data\WorldData.bin /b2t:VB\Data\WorldData.bin,VB\Data\WorldData2.tree /t2b:VB\Data\WorldData2.tree,VB\Data\WorldData2.bin
)

:: C#
@if not exist CSharp\Data @md CSharp\Data
if exist CSharp\Bin\DataConv.exe (
  CSharp\Bin\DataConv.exe /t2b:Data\WorldData.tree,CSharp\Data\WorldData.bin /b2t:CSharp\Data\WorldData.bin,CSharp\Data\WorldData2.tree /t2b:CSharp\Data\WorldData2.tree,CSharp\Data\WorldData2.bin
)

@pause
