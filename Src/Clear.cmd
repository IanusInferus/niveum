@if exist ..\Bin\net48 (
  @pushd ..\Bin\net48
  @for %%a in (*.exe) do @(
    @del %%~na.pdb /F /Q
  )
  @popd
)
@pause
