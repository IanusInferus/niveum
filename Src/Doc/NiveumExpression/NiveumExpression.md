# NiveumExpression语言规范

## 概述

NiveumObject语言是一个微型纯数值表达式计算语言，主要用于提供一种可配置的公式，并在各个语言中执行。

## 示例

一个计算升级所需经验值的公式。

公式签名

    GetUpgradeExperience(Level:Int, Initial:Int):Int

其中Level是人物等级，Initial是初始升级经验值(1级到2级所需的升级经验值)。

公式表达式

    ceil(Initial * pow(1.1, Level - 1))

其中pow为指数函数，ceil为向上取整。

## 数据类型

    Boolean 布尔类型
    Int 整数
    Real 实数

## 语法

### 空白

    whitespace ::= ' '

空白不表达语义

### 字面量

    boolean_literal ::= 'false' | 'true'
    int_literal ::= [0-9]+
    real_literal ::= [0-9]+ '.' [0-9]* | [0-9]* '.' [0-9]+
    literal ::= boolean_literal | int_literal | real_literal

### 标识符

    identifier ::= [A-Za-z_][A-Za-z0-9_]*

### 运算符

只有左结合中缀二目运算符和前缀单目运算符两种形式。

    binary_operator ::= '+' | '-' | '*' | '/'
                    | '&&' | '||'
                    | '<' | '>' | '<=' | '>=' | '==' | '!='

    unary_operator ::= '!' | '+' | '-'

### 表达式

    expr ::= literal                                                        // 字面量
           | identifier '(' parameter_list ')'                              // 函数
           | identifier                                                     // 单个变量
           | '(' expr ')'                                                   // 括号
           | unary_operator expr                                            // 前缀单目运算
           | expr binary_operator expr                                      // 中缀二目运算
    
    parameter_list ::= epsilon                                              // 空参数列表
                     | nonnull_parameter_list                               // 非空参数列表
    
    nonnull_parameter_list ::= expr                                         // 单个参数列表
                             | nonnull_parameter_list ',' expr              // 多个参数列表

### 二目运算符运算顺序

    * /
    + -
    < > <= >= == !=
    && ||                                                                   // 逻辑与、逻辑或，但不能像a &&     b || c这样串连

## 语义

    expr ::= <literal>      <boolean> boolean | <int> int | <real> real     // 字面量
           | <variable>     name:string                                     // 单个变量
           | <function>     name:string parameters:expr*                    // 函数
           | <if>           condition:expr true_part:expr false_part:expr   // if伪函数
           | <andalso>      left:expr right:expr                            // &&运算符
           | <orelse>       left:expr right:expr                            // ||运算符

if伪函数、&&运算符、||运算符单独处理，因为需要考虑求值顺序。

除此之外的运算符作为函数处理。

生成语义树的时候，会在类型不匹配的时候进行重载决策，插入恰当的类型转换函数。

当前的隐式类型转换函数仅有Int -> Real。

三个特殊运算符的类型约束如下：

    (if)(c:Bool, t:'T, f:'T) -> 'T          // 如果c为真，则计算并返回t(不计算f)，否则计算并返回f
    (&&)(Bool, Bool) -> Bool                // 逻辑与，实现短路计算，若第一个参数为假，则不计算第二个参数的表达式
    (||)(Bool, Bool) -> Bool                // 逻辑或，实现短路计算，若第一个参数为真，则不计算第二个参数的表达式

## 标准库函数

### 算术运算

    (+)(Int) -> Int
    (-)(Int) -> Int
    (+)(Real) -> Real
    (-)(Real) -> Real
    (+)(Int, Int) -> Int
    (-)(Int, Int) -> Int
    (*)(Int, Int) -> Int
    (/)(Int, Int) -> Real                   // 两个整数相除返回实数
    (+)(Real, Real) -> Real
    (-)(Real, Real) -> Real
    (*)(Real, Real) -> Real
    (/)(Real, Real) -> Real
    pow(b:Int, e:Int) -> Int                // 指数函数，b为底数，p为指数，b != 0，p >= 0
    pow(b:Real, e:Real) -> Real             // 指数函数，实数版，b为底数，p为指数，b != 0
    exp(Real) -> Real                       // 指数函数
    log(Real) -> Real                       // 自然对数函数
    mod(Int, Int) -> Int                    // 模运算，获得带余除法的余数，余数的符号始终和模数相同
    div(Int, Int) -> Int                    // 整除，无论除数为正还是负，总有恒等式a == div(a, b) * b + mod    (a, b)

### 逻辑运算

    (!)(Boolean) -> Boolean                 // 取反

### 关系运算

    (<)(Int, Int) -> Boolean
    (>)(Int, Int) -> Boolean
    (<=)(Int, Int) -> Boolean
    (>=)(Int, Int) -> Boolean
    (==)(Int, Int) -> Boolean
    (!=)(Int, Int) -> Boolean
    (<)(Real, Real) -> Boolean
    (>)(Real, Real) -> Boolean
    (<=)(Real, Real) -> Boolean
    (>=)(Real, Real) -> Boolean
    (==)(Boolean, Boolean) -> Boolean
    (!=)(Boolean, Boolean) -> Boolean

### 取整运算

    round(Real) -> Int                       // 取整，四舍六入五凑偶
    floor(Real) -> Int                       // 向下取整
    ceil(Real) -> Int                        // 向上取整
    round(Real, Int) -> Real                 // 按小数位数舍入，四舍六入五凑偶
    floor(Real, Int) -> Real                 // 按小数位数向下舍入
    ceil(Real, Int) -> Real                  // 按小数位数向上舍入

### 范围限制运算

    min(Int, Int) -> Int
    max(Int, Int) -> Int
    clamp(v:Int, l:Int, u:Int) -> Int         // 门函数，若v <= l，则取l，若v >= u，则取u，否则取v
    min(Real, Real) -> Real
    max(Real, Real) -> Real
    clamp(v:Real, l:Real, u:Real) -> Real     // 门函数，若v <= l，则取l，若v >= u，则取u，否则取v

### 其他运算

    abs(Int) -> Int                           // 取绝对值
    abs(Real) -> Real                         // 取绝对值
    rand() -> Real                            // 取[0, 1)范围内的随机实数
    rand(Int, Int) -> Int                     // 取[a, b)范围内的随机整数
    rand(Real, Real) -> Real                  // 取[a, b)范围内的随机实数
    creal(Int) -> Real                        // 整数转实数函数
    (Real)(Int) -> Real                       // 整数转实数隐式运算符
