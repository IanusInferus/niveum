cd..
cd Bin
del *.pdb /F /Q
del *.xml /F /Q
del *.vshost.exe /F /Q
del *.manifest /F /Q
del *.user /F /Q
cd..
cd Src
attrib -H ExprCalc.suo
attrib -H ExprCalc.v11.suo
attrib -H ExprCalc.v12.suo
del *.suo /F /Q
del *.cache /F /Q
pause
