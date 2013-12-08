@PATH ..\..\Bin;%PATH%

@for /D %%a in (PreviousSchemas\*) do (
  SchemaManipulator.exe /loadtype:"%%a\Common" /loadtype:"%%a\Communication" /save:"%%a\Communication.tree"
  SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /gencom:"%%a\Communication.tree","Schema\Compatibility\%%~na.tree","%%~na"
  del "%%a\Communication.tree"
)

SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /import:Communication /t2csc:CSharp\Src\Server\Services\Compatibility.cs,ServerImplementation,Server.Services

@pause
