@if exist ..\Bin (
  @pushd ..\Bin
  @for %%a in (*.exe) do @(
    @del %%~na.pdb /F /Q
    @del %%~na.xml /F /Q
  )
  @del *.vshost.exe /F /Q
  @del *.manifest /F /Q
  @del *.CodeAnalysisLog.xml /F /Q
  @del *.lastcodeanalysissucceeded /F /Q
  @del Test.* /F /S /Q
  @popd
)
@pause
