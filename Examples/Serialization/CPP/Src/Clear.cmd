rd .vs /S /Q
cd..
cd Bin
del *.pdb /F /Q
del *.xml /F /Q
del *.vshost.exe /F /Q
del *.manifest /F /Q
del *.user /F /Q
rd Debug /S /Q
rd Release /S /Q
cd..
cd Src
attrib -H DataCopy.suo
attrib -H DataCopy.v11.suo
attrib -H DataCopy.v12.suo
del *.suo /F /Q
del *.cache /F /Q
del *.ncb /F /Q
del *.sdf /F /Q
del *.user /F /Q
rd ipch /S /Q
pause
