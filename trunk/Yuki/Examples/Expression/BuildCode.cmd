@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src @md CSharp\Src
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,CSharp\Bin\Assembly.bin /t2csbl:CSharp\Src\Calculation.cs,ExprCalc

:: C++2011
@if not exist CPP\Src @md CPP\Src
SchemaManipulator.exe /loadtype:..\..\Src\YukiCore\Common\Common.tree /loadtype:..\..\Src\YukiExpression\ExpressionSchema /t2cpp:CPP\Src\ExpressionSchema.h,Yuki.ExpressionSchema
SchemaManipulator.exe /loadtyperef:..\..\Src\YukiCore\Common\Common.tree /loadtype:..\..\Src\YukiExpression\ExpressionSchema /t2cppb:CPP\Src\ExpressionSchemaBinary.h,Yuki.ExpressionSchema
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,Cpp\Bin\Assembly.bin /import:""ExpressionCalculator.h"" /t2cppbl:Cpp\Src\Calculation.h,ExprTest

@pause
