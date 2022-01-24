@PATH ..\..\Bin\net48;%PATH%

:: C#
@if not exist CSharp\Src @md CSharp\Src
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,CSharp\Bin\net48\Assembly.bin /t2csbl:CSharp\Src\Calculation.cs,ExprCalc

:: C++2011
@if not exist CPP\Src @md CPP\Src
SchemaManipulator.exe /loadtype:..\..\Src\NiveumObject\Common /loadtype:..\..\Src\NiveumExpression\ExpressionSchema /t2cpp:CPP\Src\ExpressionSchema.h,Niveum.ExpressionSchema
SchemaManipulator.exe /loadtyperef:..\..\Src\NiveumObject\Common /loadtype:..\..\Src\NiveumExpression\ExpressionSchema /t2cppb:CPP\Src\ExpressionSchemaBinary.h,Niveum.ExpressionSchema
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,Cpp\Bin\Assembly.bin /import:""ExpressionCalculator.h"" /t2cppbl:Cpp\Src\Calculation.h,ExprTest

:: C
@if not exist C\Src @md C\Src
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,C\Src\Calculation.bin /t2c:C\Src\Calculation.bin,C\Src\Calculation.h,C\Src\Calculation.c,ExprTest

:: RV64
@if not exist RV64\Src @md RV64\Src
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,RV64\Src\Calculation.bin /t2rv64:RV64\Src\Calculation.bin,RV64\Src\Calculation.h,RV64\Src\Calculation.s,ExprTest

@pause
