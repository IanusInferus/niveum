@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src @md CSharp\Src
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,CSharp\Bin\Assembly.bin /t2csbl:CSharp\Src\Calculation.cs,ExprCalc

@pause
