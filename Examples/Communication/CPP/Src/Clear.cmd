rd .vs /S /Q
@cd..
@if exist Bin (
  @cd Bin
  @del *.pdb /F /Q
  @del *.xml /F /Q
  @del *.vshost.exe /F /Q
  @del *.manifest /F /Q
  @del *.user /F /Q
  @rd Debug /S /Q
  @rd Release /S /Q
  @cd..
)
@cd Src
attrib -H Communication.suo
attrib -H Communication.v11.suo
attrib -H Communication.v12.suo
del *.suo /F /Q
del *.cache /F /Q
del *.ncb /F /Q
del *.sdf /F /Q
del *.db /F /Q
del *.user /F /Q
rd ipch /S /Q
pause
