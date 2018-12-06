@cd..
@if exist Bin (
  @cd Bin
  @for %%a in (*.exe) do @(
    @del %%~na.pdb /F /Q
    @del %%~na.xml /F /Q
  )
  @del *.vshost.exe /F /Q
  @del *.manifest /F /Q
  @del *.CodeAnalysisLog.xml /F /Q
  @del *.lastcodeanalysissucceeded /F /Q
  @del Test.* /F /S /Q
  @cd..
)
@cd Src
attrib -H Communication.suo
attrib -H Communication.v11.suo
attrib -H Communication.v12.suo
del *.suo /F /Q
del *.cache /F /Q
pause
