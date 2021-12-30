# NiveumObject语言规范

## 概述

NiveumObject语言，是内嵌于NiveumTree语言的类型定义语言，主要用于描述存储和通讯用的基于对象的数据结构，以生成各个语言的类型定义和序列化代码。

## 示例

以下为一个3D模型的例子。

    #Record World                                       世界模型
        Name                Optional<String>            名称
        Authors             Set<String>                 作者
        MetaData            Map<String, String>         元信息
        Objects             List<Object3d>              物体列表
        Blobs               Map<String, List<Byte>>     二进制数据

    #TaggedUnion Object3d                               物体
        Primitive           PrimitiveObject3d           基元物体
        Grouped             GroupedObject3d             组合物体
        Translated          TranslatedObject3d          平移物体
        Rotated             RotatedObject3d             旋转物体
        Scaled              ScaledObject3d              拉伸物体
        Transformed         TransformedObject3d         变换物体

    #Record GroupedObject3d                             组合物体
        Objects             List<Object3d>              物体列表

    #Record TranslatedObject3d                          平移物体
        Object              Object3d                    物体
        Translation         Vector3d                    平移向量

    #Record RotatedObject3d                             旋转物体
        Object              Object3d                    物体
        Axes                Vector3d                    旋转轴
        Rho                 Float64                     "旋转角(弧度)"

    #Record ScaledObject3d                              拉伸物体
        Object              Object3d                    物体
        Scale               Float64                     拉伸系数

    #Record TransformedObject3d                         变换物体
        Object              Object3d                    物体
        Transformation      Matrix44d                   变换

    #TaggedUnion PrimitiveObject3d                      基元物体
        Point               Point3d                     点
        Line                Line3d                      线段
        Triangle            Triangle3d                  三角形

    #Alias Point3d                                      点
        Vector3d

    #Record Line3d                                      线段
        Start               Point3d                     起点
        End                 Point3d                     终点

    #Record Triangle3d                                  三角形
        A                   Point3d                     点A
        B                   Point3d                     点B
        C                   Point3d                     点C

以下为一个消息传递的例子。“>”前为请求参数的Record，“>”后为响应结果的TaggedUnion。其中使用的类型可以用上面的类型语法定义。

    #ClientCommand SendMessage                          发送消息
        Content             String                      内容
        >
        Success             Unit                        成功
        TooLong             Unit                        内容过长

    #ServerCommand MessageReceived                      接收到消息
        Content             String                      内容

## 概念

基于对象的数据结构是指的由

    基元类型(Primitive)
    别名(Alias)
    记录(Record)
    标签联合(TaggedUnion)
    枚举(Enum)
    客户端方法(ClientCommand)
    服务端事件(ServerCommand)

组成的数据结构。

### 基元类型 Primitive

没有泛型参数，指由所有相关外部系统均理解的基础类型，如32位有符号整数、字符串等。

### 别名 Alias

可有泛型参数，用以表示一个基元类型、别名、记录、标签联合或其泛型特化，以及多元组。

### 记录 Record

可有泛型参数，用以表示一个有多个字段的顺序结构。

### 标签联合 TaggedUnion

可有泛型参数，用以表示一个有标签和多个字段的选择结构，其实例只表示多个字段中的一个。

### 枚举 Enum

没有泛型参数，相当于一个所有字段的类型都是空类型(Unit)的标签联合。

### 客户端方法 ClientCommand

没有泛型参数，在通讯中表示由客户端发出的方法，由一个多个字段的顺序结构表示客户端传给服务端的参数，由一个有标签和多个字段的选择结构表示服务端返回给客户端的返回值。

### 服务端事件 ServerCommand

没有泛型参数，在通讯中表示由服务端发出的方法，由一个多个字段的顺序结构表示服务端传给客户端的参数。

### 元组 Tuple

一个匿名的有多个匿名字段的顺序结构，类型的一种组合方法。

### 泛型特化 GenericTypeSpec

一个匿名的数据结构，表示将某个有泛型参数的类型代入参数的结果。

## 支持矩阵

功能 vs 语言

|              | VB.Net  | C#          | Java | C++2017 | Haxe   | Python | XHTML |
|:------------:|:-------:|:-----------:|:----:|:-------:|:------:|:------:|:-----:|
| 类型定义     | √      | √          | √   | √      | √     | √(*)  | √    |
| 二进制序列化 | Firefly | √\|Firefly | √   | √      | ×     | √     | 无需  |
| JSON序列化   | ×      | √          | ×   | ×      | √     | ×     | 无需  |
| 二进制通讯   | ×      | 两端        | ×   | 两端    | ×     | ×     | 无需  |
| JSON通讯     | ×      | 两端        | ×   | ×      | 客户端 | ×     | 无需  |
| 版本兼容     | ×      | √          | ×   | √      | ×     | ×     | 无需  |

\* Python不支持多命名空间。

## 词法分析

### 词(Token)

直接使用NiveumTree中的词法分析。

### 符号(Symbol)

与NiveumTemplate中的符号(Symbol)的做法一致。

## 文法分析

文法非常简单，基本上在词法分析的结果上可以直接对应。
