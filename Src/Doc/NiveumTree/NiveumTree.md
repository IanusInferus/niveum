﻿# NiveumTree语言规范

## 概论

NiveumTree语言文件格式，是一个类似XML的替代文件格式。主要目的是用于录入格式化数据。

因此，本格式的设计目标：

1) 避免重复输入

2) 力求整齐

3) 尽量与XML对应

4) 不是用于解决数据存储
 
5) 不是用于解决数据传输

下面是本格式的一个简单例子：

    Fruits
        Fruit
            Name Apple
        Fruit
            Name Pear
        Fruit
            Name Watermelon

其对应的XML形式如下：

    <?xml version="1.0" encoding="UTF-16" ?>
    <Fruits>
        <Fruit>
            <Name>Apple</Name>
        </Fruit>
        <Fruit>
            <Name>Pear</Name>
        </Fruit>
        <Fruit>
            <Name>Watermelon</Name>
        </Fruit>
    </Fruits>

可以看出，本格式的最大特点是层次主要由缩进来区分，不使用配对标签。

## 基本概念

基本概念组成了Tree格式的基干。

### 字面量

字面量(Literal)是指的一个由Unicode字符构成的字符串。

字面量可以为如下几种形式：

1) 空引用，即null

2) 空字符串，即""

3) 非空字符串，有多个Unicode字符构成

本格式中，在字面量以外，不得有\f\t\v这类控制符。所有的缩进均需要使用空格表示。

字面量的语法形式如下：

1) 空引用，由$Empty表示

2) 空字符串，由""表示

3) 首字符为!#$%&/;=?\^`|~之一的字符串，由两个半角双引号括住，内部的半角双引号需双写($表示预处理指令 #表示自定义指令 @表示标记但是是普通名称不禁止 //表示单行注释 其他字符暂时不开放)

4) 包含()\f\t\v或不配对的<>{}[]或在<>{}[]外面有双引号或空格的字符串，需要按3)的方式书写

5) 包含单独的回车或者单独的换行的字符串，需要由两对双引号括住，可以使用反斜杠转义，内部的半角双引号需要加\\

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
        注意：如果第一个字符为空格，需要使用"\ "转义。

6) 包含完整的回车换行的字符串，可以使用$String预处理指令书写

7) 其他的字符串可以直接书写

简单而言，对于单行字符串，如果搞不清是否需要加双引号，则可直接加双引号

    $Empty       //1)表示空引用
    ""           //2)表示空字符串
    "$Empty"     //3)表示"$Empty"字符串
    "123, 123"   //4)表示"123, 123"字符串
    """"         //4)表示只有一个双引号的字符串
    <{} [12] {}> //4)表示字符串"<{} [12] {}>"
    ""\"""       //5)表示只有一个双引号的字符串
    $String      //6)表示一个C语言的Hello world程序的字符串
        #include <stdio.h>

        int main(void)
        {
            printf("Hello, world!\n");
            return 0;
        }
    123          //7)表示"123"字符串
    0xFFFFFFFF   //7)表示"0xFFFFFFFF"字符串
    0b11110000   //7)表示"0b11110000"字符串
    12.5         //7)表示"12.5"字符串
    中文         //7)表示"中文"字符串
    123/456      //7)表示"123/456"字符串
    123//456     //7)表示"123//456"字符串
    <meta http-equiv="content-type" content="text/html; charset=UTF-8"/> //7)表示一个复杂的HTML结点字符串

如果要在$String的最后增加空行，可以使用$End。

例如：

    $String
        Test String

    $End

$String最后的不足规定缩进的空行会不计做值的一部分。

中间的空行，若不足规定缩进，仍然认为是值的一个空行。

除6)的字面量，称之为单行字面量(SingleLineLiteral)。6)形式的字面量，称之为多行字面量(MultiLineLiteral)。

从这个例子可以看出，不同于大部分编程语言，本格式中的字面量的格式灵活，可由用户自行定义。
多行字面量，使得本格式可以原样嵌入各种各样的语言，也使得我们可以按需自定义语言。
XML的难以书写<、>等符号，需要使用撇脚的CDATA块的问题也得到彻底解决。

但是，多行字面量的功能使得本格式的文法属于上下文相关文法，需要自顶向下来进行文法分析。

### 词

词(Token)，词法分析结果。

    Token =
        | SingleLineLiteral
        | LeftParenthesis
        | RightParenthesis
        | PreprocessDirective
        | FunctionDirective
        | SingleLineComment

### 结点

结点(Node)，基本可用单元。

    Node =
        | Empty
        | Literal
        | Literal Node*

结点可以是空的，可以只有一个值，也可以有名称有子结点。

名称是指一个单行字面量。理论上可以包含任意字符，但是，建议只在XML的名称范围内定义。

例如：

    Fruits
        Fruit
            Name Apple
        Fruit
            Name Pear
        Fruit
            Name Watermelon

这里的Fruits、Fruit、Name、Apple等均是结点。

其中，Fruits有三个子结点Fruit。

元素的写法支持一种简写形式，可以将多行的内容写到一行。

    Fruits
        Fruit
            Name
                Apple
        Fruit
            Name
                Pear
        Fruit
            Name
                Watermelon

    Fruits
        Fruit Name Apple
        Fruit Name Pear
        Fruit Name Watermelon

    Fruits (Fruit Name Apple) (Fruit Name Pear) (Fruit Name Watermelon)

    Fruits
        $List Fruit
            Name Apple
            Name Pear
            Name Watermelon

    Fruits
        $List Fruit
            $List Name
                Apple
                Pear
                Watermelon

以上几种写法都是有效的形式。
以下的为无效形式。

    Fruits
        Fruit Name
            Apple
        Fruit Name
            Pear
        Fruit Name
            Watermelon

### 预处理指令

即如下几个指令：

    $Comment    多行注释
    $Empty      空字面量
    $String     多行字面量
    $End        强制多行结束符，用于在任何多行符号的下一行指明其结尾
    $List       列
    $Table      表

例如：

    $Comment
        注释

    SingleLineNode $Empty

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

### 自定义指令

以"#"加一个可写成无需引号形式的字面量的字符串作为名称的特殊结点。

自定义指令可以带参数和内容。

参数分三种：结点参数、树状参数、表状参数。

    #Function Fruits (Fruit Name Apple) (Fruit Name Pear) (Fruit Name Watermelon) //树状参数，参数为SingleLineNode
    #Function (Fruit Name Apple) (Fruit Name Pear) (Fruit Name Watermelon) //表状参数，表示有3个参数，每个参数为TableLineNode
    #Function (Fruit Name Apple) //结点参数表示，表示有5个结点"(", "Fruit", "Name", "Apple", ")"

内容也分三种：行内容、树状内容、表状内容。

    //行内容，每行均为自由内容
    #Function
        LineContent
        LineContent
        LineContent

    //树状内容，内容为MultiNodes*
    #Function
        MultiNodes
        MultiNodes
        MultiNodes

    //表状内容，内容的每行为TableLineNode*
    #Function
        TableLineNode* SingleLineComment?
        TableLineNode* SingleLineComment?
        TableLineNode* SingleLineComment?

### 森林

森林(Forest)是文件的最顶层，由多个结点组成。

    Forest = Node*

## XML对应概念

为了兼容XML格式，在转换成XML之前，必须将本格式限制在某个范围内。

这个过程可以在使用格式时即约定好，也可以通过程序转换。

### 值

值(Value)是没有子结点的结点。

    Value =
        | Empty
        | Literal

对应于XML中的最底层元素的内部。

例如，<Name>Apple</Name>中有一个值"Apple"。

### 元素

元素(Element)是一个满足如下条件的非底层结点。

    Element =
        | Literal Value
        | Literal Element*

对应于XML中的元素。

名称可用XML的命名空间形式定义，即类似a:Name这种使用冒号分隔的方式。

内容可以为空引用、空值(空字符串)、非空值、一个或多个子元素。

其中，值为空值和子元素个数为0认为同一种情况，空引用值和空引用子元素列表认为同一种情况。

例如：

    Fruits
        Fruit
            Name
                Apple
        Fruit
            Name
                Pear
        Fruit
            Name
                Watermelon

其中，Fruits、三个Fruit和三个Name都是元素，Name是Fruit的子元素。Name有值"Apple"，Fruit没有值，只有子元素。

需要注意的是，本格式与XML不兼容的一点，值与子元素。

XML中，支持值与子元素混合的形式，例如：

    <TextRun>This is <I>a</I> text.</TextRun>

TextRun元素有"This is "、<I>a</I>、" text."三个子级。

本格式认为这是受HTML影响，不支持这种形式。

### 树

树(Tree)是文件的最顶层，由一个元素组成。

    Tree = Element

树能够与XML互相转换。

## 词法分析

词法分析可以使用自动机完成，步骤如下

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
    Tag TokenType 单行字面量、预处理指令、自定义指令
    Stack<ParenthesisType> 括号栈

    初值
    State <- 0
    Tag TokenType <- 单行字面量
    Stack<ParenthesisType> <- 空

    State 0
        EndOfLine -> 返回空Token
        Space -> 前进
        [\f\t\v] -> 失败
        " -> 标记符号开始，State 2，前进
        ( -> 标记符号开始，前进，返回LeftParenthesis
        ) -> 标记符号开始，前进，返回RightParenthesis
        // -> 标记符号开始，加入Output和前进到底，返回SingleLineComment
        / -> 失败
        < -> 标记符号开始，压栈，State 1，前进
        [ -> 标记符号开始，压栈，State 1，前进
        { -> 标记符号开始，压栈，State 1，前进
        [!@%&;=?\^`|~] -> 失败
        Any -> 标记符号开始，State 1，前进

    State 1
        EndOfLine -> 如果栈空，判定词形式（SingleLineLiteral、PreprocessDirective、FunctionDirective）返回，否则失败
        Space -> 如果栈空，判定词形式（SingleLineLiteral、PreprocessDirective、FunctionDirective）返回，前进
        [\f\t\v] -> 失败
        " -> 如果栈空，失败，否则前进
        [()] -> 如果栈空，判定词形式（Operator、PreprocessDirective、Direct）返回，否则失败
        < -> 压栈，前进
        [ -> 压栈，前进
        { -> 压栈，前进
        > -> 前进，如果栈空或者栈顶不匹配，失败，否则退栈
        ] -> 前进，如果栈空或者栈顶不匹配，失败，否则退栈
        } -> 前进，如果栈空或者栈顶不匹配，失败，否则退栈
        Any -> 前进

    State 2
        EndOfLine -> 失败
        " -> State 21，前进
        Any -> 加入Output，State 22，前进

    State 21
        EndOfLine -> 返回SingleLineLiteral
        Space -> 返回SingleLineLiteral
        ( -> 返回SingleLineLiteral
        ) -> 返回SingleLineLiteral
        " -> 加入Output，State 22，前进
        \ -> State 31，前进
        Any -> 加入Output，State 3，前进

    State 22
        EndOfLine -> 失败
        " -> State 23，前进
        Any -> 加入Output，前进

    State 23
        EndOfLine -> 返回SingleLineLiteral
        Space -> 返回SingleLineLiteral
        [\f\t\v] -> 失败
        " -> 加入Output，State 22，前进
        ( -> 返回SingleLineLiteral
        ) -> 返回SingleLineLiteral
        Any -> 失败

    State 3
        EndOfLine -> 失败
        "" -> 前进，前进，返回SingleLineLiteral
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

## 文法定义

这个定义在文法上不是很严格，缩进的地方主要是示意。

    Forest ::= MultiNodes*

    MultiNodes ::=
        | Node
        | ListNodes
        | TableNodes
        | FunctionNodes

    Node ::=
        | SingleLineNode SingleLineComment?
        | MultiLineLiteral
        | SingleLineComment
        | MultiLineComment
        | SingleLineLiteral SingleLineComment?
            MultiNodes
            +
        ("$End" SingleLineComment?)?

    SingleLineNode ::=
        | EmptyNode
        | SingleLineFunctionNode
        | SingleLineLiteral
        | ParenthesisNode
        | SingleLineLiteral (ParenthesisNode | ParenthesisNode* SingleLineNode)

    ParenthesisNode := "(" SingleLineNode ")"

    SingleLineComment ::=
        "//" .*

    MultiLineComment ::=
        "$Comment" SingleLineComment?
            .*
            *
        EndDirective?

    EmptyNode ::= "$Empty"

    SingleLineFunctionNode ::= FunctionDirective Token*?<"(" ")"Matched> (EndOfLine|")"|SingleLineComment)

    MultiLineLiteral ::=
        "$String" SingleLineComment?
            .*
            *
        EndDirective?

    ListNodes ::=
        "$List" SingleLineLiteral SingleLineComment?
            MultiNodes*
            *
        EndDirective?

    TableNodes ::=
        "$Table" SingleLineLiteral SingleLineLiteral* SingleLineComment?
            TableLineNode* SingleLineComment?
            *
        EndDirective?

    TableLineNode ::=
        | EmptyNode
        | SingleLineFunctionNode
        | SingleLineLiteral
        | ParenthesisNode

    FunctionNodes ::=
        FunctionDirective Token*?<"(" ")"Matched> SingleLineComment?
            .*
            *
        EndDirective?

    EndDirective ::= "$End" SingleLineComment?

    FunctionDirective ::= "#" Literal(7)

    Token ::=
        | SingleLineLiteral
        | "("
        | ")"
        | PreprocessDirective : "$" .*
        | FunctionDirective
        | SingleLineComment

## 语义定义

    Forest = Node*

    Node =
        | Empty
        | Literal
        | Literal Node*
