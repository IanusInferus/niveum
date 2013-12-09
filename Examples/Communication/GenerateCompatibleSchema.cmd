@PATH ..\..\Bin;%PATH%

@for /D %%a in (PreviousSchemas\*) do (
  SchemaManipulator.exe /loadtype:"%%a\Common" /loadtype:"%%a\Communication" /save:"%%a\Communication.tree"
  SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /gencom:"%%a\Communication.tree","Schema\Compatibility\%%~na.tree","%%~na"
  del "%%a\Communication.tree"
)

@pause
