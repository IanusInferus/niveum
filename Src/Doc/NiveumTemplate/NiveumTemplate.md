﻿# NiveumTemplate语言规范

## 概述

NiveumTemplate语言，是内嵌于NiveumTree语言的模板语言，主要用于描述代码生成时的模板，可以生成C#代码，并支持直接嵌入C#代码。

## 词法分析

### 词(Token)

词法分析中，每个词都是单行的，不包含\r\n，多行字面量均通过预处理指令在文法分析的时候处理

词有如下几种形式

1) Direct，直接，不包含(),\f\t\v，不在<>{}[]外包含空格，不包含不配对的<>{}[]，不以;`.开头，整体和开头的一部分都不能解释为8)、9)

2) Quoted，双引号引用，以""括住的字符串，其中双引号双写表示单个双引号，其他字符表示自身

3) Escaped，双双引号引用，以""""括住的字符串，可以使用反斜杠转义，转义表如下

        \0 与null \u0000 匹配
        \a 与响铃（警报）\u0007 匹配 
        \b 与退格符 \u0008 匹配
        \f 与换页符 \u000C 匹配
        \n 与换行符 \u000A 匹配
        \r 与回车符 \u000D 匹配
        \t 与 Tab 符 \u0009 匹配 
        \v 与垂直 Tab 符 \u000B 匹配
        \x?? 与 \u00?? 匹配
        \u???? 与对应的UTF16字符对应
        \U????? 与对应的UTF32字符对应
        其他反斜杠后面的字符表示该字符自身
        注意：如果第一个字符为空格，会和2)产生歧义，需要使用"\ "转义。

4) LeftParenthesis，左括号，即"("

5) RightParenthesis，右括号，即")"

6) Comma，逗号，即","

7) PreprocessDirective，预处理指令，和1)类似，区别在于以$或#开头

8) Operator，运算符，和1)类似，但完全由!%&*+-/<=>?@\^|~组合构成，且不符合9)

9) SingleLineComment，单行注释，在行的开始处或空格后以//开头直到行的结束

每个词有是否为一行的起始IsLeadingToken和是否在非缩进空格后IsAfterSpace两个属性

状态机

    State 0    Whitespace空格
    State 1    普通Token
    State 1-n  普通Token<中
    State 1-n  普通Token[中
    State 1-n  普通Token{中
    State 2    双引号开始
    State 21   双双引号开始
    State 22   普通双引号Token确定
    State 23   普通双引号Token结束双引号/转义双引号
    State 3    转义双双引号Token
    State 31   转义双双引号Token转义符
    State 4    运算符
    Stack<ParenthesisType> 括号栈
    IsLeadingToken 是否为一行的起始
    IsAfterSpace 是否在非缩进空格

    初值
    State <- 0
    Stack<ParenthesisType> <- 空
    IsLeadingToken 外部传入
    IsAfterSpace <- False

    State 0
        EndOfLine -> 返回空
        Space -> IsAfterSpace <- True，前进
        [\f\t\v] -> 失败
        [;`] -> 失败
        " -> 标记符号开始，State 2，前进
        ( -> 标记符号开始，前进，返回LeftParenthesis
        ) -> 标记符号开始，前进，返回RightParenthesis
        , -> 标记符号开始，前进，返回Comma
        . -> 标记符号开始，前进，返回Operator
        // -> 标记符号开始，加入Output和前进到底，返回SingleLineComment
        < -> 标记符号开始，压栈，State 1，前进
        [ -> 标记符号开始，压栈，State 1，前进
        { -> 标记符号开始，压栈，State 1，前进
        [!%&*+-/<=>?@\\^|~] -> 标记符号开始，State 4，前进
        Any -> 标记符号开始，State 1，前进

    State 1
        EndOfLine -> 如果栈空，判定词形式（Operator、PreprocessDirective、Direct）返回，否则如果词符合运算符，返回Operator，否则失败
        Space -> 如果栈空，判定词形式（Operator、PreprocessDirective、Direct）返回，否则如果词符合运算符，返回Operator，否则前进
        [\f\t\v] -> 失败
        " -> 如果栈空，失败，否则前进
        [()] -> 如果栈空，判定词形式（Operator、PreprocessDirective、Direct）返回，否则如果词符合运算符，返回Operator，否则失败
        , -> 如果栈空，判定词形式（Operator、PreprocessDirective、Direct）返回，否则如果词符合运算符，返回Operator，否则前进
        < -> 压栈，前进
        [ -> 压栈，前进
        { -> 压栈，前进
        > -> 前进，如果栈空或者栈顶不匹配，如果词符合运算符，State 4，清栈，否则失败，否则退栈
        ] -> 前进，如果栈空或者栈顶不匹配，失败，否则退栈
        } -> 前进，如果栈空或者栈顶不匹配，失败，否则退栈
        Any -> 前进

    State 2
        EndOfLine -> 失败
        " -> State 21，前进
        Any -> 加入Output，State 22，前进

    State 21
        EndOfLine -> 返回Quoted
        Space -> 返回Quoted
        " -> 加入Output，State 22，前进
        \ -> State 31，前进
        Any -> 加入Output，State 3，前进

    State 22
        EndOfLine -> 失败
        ( -> 返回SingleLineLiteral
        ) -> 返回SingleLineLiteral
        " -> State 23，前进
        Any -> 加入Output，前进

    State 23
        EndOfLine -> 返回Quoted
        Space -> 返回Quoted
        [\f\t\v] -> 失败
        " -> 加入Output，State 22，前进
        [(),] -> 返回Quoted
        Any -> 失败

    State 3
        EndOfLine -> 失败
        "" -> 前进2，返回Escaped
        " -> 失败
        \ -> State 31，前进
        Any -> 加入Output，前进

    State 31
        EndOfLine -> 失败
        0 -> 加入U+0000到Output，State 3，前进
        a -> 加入U+0007到Output，State 3，前进
        b -> 加入U+0008到Output，State 3，前进
        f -> 加入U+000C到Output，State 3，前进
        n -> 加入U+000A到Output，State 3，前进
        r -> 加入U+000D到Output，State 3，前进
        t -> 加入U+0009到Output，State 3，前进
        v -> 加入U+000B到Output，State 3，前进
        x[0-9A-Fa-f]{2} -> 加入U+00..到Output，State 3，前进3
        u[0-9A-Fa-f]{4} -> 加入U+....到Output，State 3，前进5
        U[0-9A-Fa-f]{5} -> 加入U+.....到Output，State 3，前进6
        x -> 失败
        u -> 失败
        U -> 失败
        Any -> 加入Output，State 3，前进

    State 4
        EndOfLine -> 返回Operator
        Space -> 返回Operator
        [!%&*+-/<=>?@\\^|~] -> 前进
        Any -> 失败

Direct词，可以作为符号名称，如果不能直接书写或与保留字重复，可以使用{}括住不能直接书写的部分，{}内部分使用字面量转义规则进行转义，例如

    String
    List<String>
    {Match}
    List<Mat{ \n\x7B\u007B\U0007B}ch>

状态机

    State 0       开始
    State 1       内部
    State 2       转义中

    State 0
        EndOfString -> 结束
        { -> State 1，前进
        } -> 失败
        Any -> 加入到Output，前进

    State 1
        EndOfString -> 失败
        { -> 失败
        } -> State 0，前进
        \ -> State 2，前进
        Any -> 加入到Output，前进

    State 2
        EndOfLine -> 失败
        0 -> 加入U+0000到Output，State 1，前进
        a -> 加入U+0007到Output，State 1，前进
        b -> 加入U+0008到Output，State 1，前进
        f -> 加入U+000C到Output，State 1，前进
        n -> 加入U+000A到Output，State 1，前进
        r -> 加入U+000D到Output，State 1，前进
        t -> 加入U+0009到Output，State 1，前进
        v -> 加入U+000B到Output，State 1，前进
        x[0-9A-Fa-f]{2} -> 加入U+00..到Output，State 1，前进3
        u[0-9A-Fa-f]{4} -> 加入U+....到Output，State 1，前进5
        U[0-9A-Fa-f]{5} -> 加入U+.....到Output，State 1，前进6
        x -> 失败
        u -> 失败
        U -> 失败
        Any -> 加入Output，State 1，前进

### 符号(Symbol)

词可以作为对类型的引用（类型规范），如List<Int>, Map<Int, String>，A<Int>.B<String>.C
或者作为变量引用，如a, a.b.c, a<Int>.b<String>.c

状态机

    State 0       前导空格
    State 1       外层名称中
    State 2+n     尖括号内部
    State 3       最外层尖括号后
    State 4       结尾空格
    Level

    State 0
        EndOfString -> 失败
        Space -> 前进
        Any -> SymbolStartIndex = Index，State 1

    State 1
        EndOfString -> 如果SymbolChars为空，则失败，否则State 3
        < -> 如果SymbolChars为空，则失败，否则State 2
        > -> 失败
        . -> 如果SymbolChars为空，则失败，否则State 3
        Any -> 加入到SymbolChars，前进

    State 2
        EndOfString -> 失败
        < -> （如果Level > 0，则加入到ParamChars，否则ParamStartIndex = Index + 1），Level += 1, 前进
        > -> Level -= 1, 如果Level > 0，则加入到ParamChars，前进，否则提交参数到Parameters，清空ParamChars，State 3，前进
        , -> 如果Level = 1, 则提交参数到Parameters，清空ParamChars，ParamStartIndex = Index + 1，前进
        Any -> 加入到ParamChars，前进

    State 3
        EndOfString -> SymbolChars和Parameters加入到Output，清空，结束
        Space -> SymbolChars和Parameters加入到Output，清空，State 4，前进
        . -> SymbolChars和Parameters加入到Output，清空，SymbolStartIndex = Index + 1，State 1，前进
        Any -> 失败

    State 4
        EndOfString -> 结束
        Space -> 前进
        Any -> 失败

## 文法分析

### 注释

有两种形式

1) 在一行的缩进开头或非词空格后以//开头，到行结束

        //abcdefg
        123 //abcdefg

2) 以$Comment标记的预处理指令

        $Comment
            abcdefg

### 字面量和符号名称

Direct词，默认为符号名称，但在$Comment、$String、$List、$Table、Primitive字面量中默认为字面量

符号名称中

* Null为空字面量，用于兼容.Net代码

* Default为默认值字面量

* This为当前对象引用（保留不用）

* _为忽略变量标记

* Tuple为元组标记

* Array为数组标记，用于兼容.Net代码

* Cast为类型转换标记

* True、False为Boolean字面量

* 可看作Int、Float的字面量，默认为对应的类型

* 其他为非法

可以看作Int的字面量，正则表达式为

    [+-]?[0-9]+
    [+-]?0x[0-9A-Fa-f]+
    [+-]?0b[01]+

可以看作Float的字面量，正则表达式为

    [+-]?([0-9]+|\.[0-9]+|[0-9]+\.[0-9]+)([eE][+-][0-9]+)?

### 预处理指令

模板表达式中

    $$          可执行表达式
    $End        强制多行结束符，用于在任何多行符号的下一行指明其结尾

可执行表达式中

    $Comment    多行注释
    $String     多行字面量
    $End        强制多行结束符，用于在任何多行符号的下一行指明其结尾
    $List       列
    $Table      表
    #           内嵌模板
    ##          内嵌模板生成

例如：

    $$
        Let a = 1

    $Comment
        注释

    $String
        多行字面量

    $String
        多行字面量

    $End //表示多行字面量中最后有一个空行

    //下面的列和后面的4个单独的结点等价
    $List Int
        0
        1
        2
        3

    Int 0
    Int 1
    Int 2
    Int 3

    //下面的表和后面3个单独的结点等价
    $Table Head FieldA FieldB FieldC
        0   1   2
        3   4   5
        6   7   8

    Head
        FieldA 0
        FieldB 1
        FieldC 2
    Head
        FieldA 3
        FieldB 4
        FieldC 5
    Head
        FieldA 6
        FieldB 7
        FieldC 8

    #
        public static void main()
        {
        }

    ##
        public static void main()
        {
        }

### 括号

所有的文法形式都是由多树构成的，例如

    A
        B
            C
            D
        E
            F
        G
    H
        I
            J

()可用于在单行内表达多个结点，逗号(,)可以用来分隔多个结点，例如上面的例子可以写为

    A
        B(C, D)
        E(F)
        G
    H
        I(J)

还可以写为

    A(B(C, D), E(F), G)
    H(I(J))

如果一行以空格隔开了多个结点，则默认情况下他们为第一个为父结点，其余为子结点，如上面的例子可以写为

    A (B C D) (E F) G
    H (I J)

第一个结点为特殊名称时除外，例如

    Let a = 1

如果一个结点后面不带空格即连接括号()，表示括号内内容为其子结点，如

    A B(C, D) E(F) G
    H(I J)

()可以跨多行；只要当前行括号不匹配，则下一个和当前行缩进相同的行之前的行均看作一个子结点，之后的行接着当前行，如

    A(B(
        C
        D
    ),
    E(
        F
    ), G)
    H(
        I
            J
    )

部分文法构造具有强制最右方结点为下方子结点的父结点的功能，如

    Let a = Tuple<Int, Int>(1, 2)

和

    Let a = Tuple<Int, Int>
        1
        2

和

    Let a =
        Tuple<Int, Int>
            1
            2

含义相同

部分文法构造具有并列结点转换为树的功能，如

    1 + 2

和

    +
        1
        2

和

    + 1 2

含义相同

### 文法森林

每个模板由若干模板结点组成，分成两种情况

1) 模板字面量文本

2) 可执行结点，由$$和之下的可执行表达式结点构成

每个可执行结点有如下几种情况

1) Direct，直接

2) Literal，字面量，包含双引号引用、双双引号引用和多行字面量

3) Operator，运算符

4) Template，内嵌模板，包含若干模板结点

5) YieldTemplate，内嵌模板生成，包含若干模板结点

6) Stem，茎，包含可选的头部和若干子结点

7) Undetermined，待定序列，包含若干结点，用于在语义分析中进一步确定关系

## 语义分析(未实现)

目前主要使用嵌入的C#代码代替模板的语义部分的定义，所以下面列出的功能基本没有实现

### 保留字

    Yield, YieldMany, Throw, Let, Var, If, Match, For, While, Continue, Break, Return, Null, Default, This, _, Tuple, Cast, True, False

保留字在恰当的位置，会按照预先定义的规则进行解释，可以使用""或{}等进行避免。

内置类型

    Unit, Boolean, String, Int, Real, Type, Optional, List, Set, Map
    UInt8, UInt16, UInt32, UInt64, Int8, Int16, Int32, Int64, Float32, Float64

其中Int表示Int32，Real表示Float64

此外还有Tuple, Array, Func, Action泛型类型系列

### 运算符

运算符有

    一元 + - !
    乘除 * / \ % //乘法 除法 整除 取模
    加减 + -
    关系 < > <= >=
    相等 == !=
    条件 || &&
    转换 Cast

目前暂不考虑优先级的问题，均需要加括号

### 变量定义

#Constant中定义的常量可以在所有模板中使用

`$$`中定义的变量延续到当前模板的结束

Sequence、If、Match、For、While均创建了一个变量的范围，变量延续到该范围的结束

变量只需要与当前范围内的其他变量不重名，引用时，先在当前范围内寻找，再到更高的范围依次寻找

### 跳转和返回

Continue、Break表达式可以出现在Sequence、If、Match、For、While中，并被For、While接收

Yield、YieldMany、##表达式用于将表达式生成为模板内容，可以出现在Sequence、If、Match、For、While、`$$`中，并被`$$`接收

Return表达式可以出现在Sequence、If、Match、For、While、`$$`、Lambda中，并被`$$`、Lambda接收，但在`$$`中不能返回值，只能用来退出`$$`

Lambda的内部，如果出现了一个Return，则最后应出现Return（最后为静态分析可得的死循环的情况除外），返回值的类型由所有的Return一致决定

如果没有出现Return，则返回值为最后一个表达式的值，如果最后一个表达式没有值，则Lambda没有返回值

如果最后一个表达式为函数调用，而需要没有返回值，可以使用赋值语句赋值给_

`$$`、For、While、Lambda的内部、If、Match的分支中，最外层表达式如果是Sequence，则看作是顺序执行的表达式

If、Match的分支中，如果所有分支最后一个表达式均有值，则If、Match也具有值，如果所有分支最后一个表达式均无值，则If、Match也无值，不允许混合情况

如果最后一个表达式为函数调用，而需要没有返回值，可以使用赋值语句赋值给_
