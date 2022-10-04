# NiveumQuery语言定义

## 概述

本文定义一种参考了[GraphQL](https://graphql.org/)思想的查询语言，用于通讯协议中客户端传递到服务器端，并用于服务器端执行的灵活的查询。区别主要是增加了用于静态语言的代码生成。NiveumQuery语言是NiveumObject语言中的嵌套DSL，使用#Query语句定义。

## 样例

样例为一个邮件系统，主要数据为用户账号和邮件，数据存储的方式没有特别指定，可以保存到文件中或数据库中，但Niveum图查询语言可能对存储的结构有一些要求(比如需要一些索引)。

    #Record UserProfile                                 用户账号信息
        Id                  Int                         用户号
        Name                String                      用户名
        EmailAddress        Optional<String>            邮件地址

        Mails               List<Mail>                  邮件列表，非管理员用户无法查看他人的邮件

    #Record Mail                                        [By:Id][OrderBy:Id]邮件
        Id                  Int64                       邮件ID
        Title               String                      标题
        FromId              Int                         发件用户ID
        Time                String                      "时间(UTC)：yyyy-MM-ddTHH:mm:ssZ形式"
        Content             MailContent                 内容
        IsNew               Boolean                     是否是新邮件

        From                UserProfile                 来源用户
        Tos                 List<UserProfile>           收件人
        Attachments         List<MailAttachment>        附件

    #TaggedUnion MailContent                            邮件内容
        PlainText           String                      纯文本
        RichText            String                      RTF文本

    #Record MailAttachment                              [By:Id]邮件附件
        Id                  Int64                       邮件ID
        Name                String                      名称
        Content             List<Byte>                  内容

    #ClientCommand GetUserProfile                       获取用户信息
        UserId              Int                         用户号
        Query               QuerySpec<UserProfile>      查询
        >
        Success             QueryResult<UserProfile>    查询结果
        NotExist            Unit                        用户不存在
        NotEnoughPrivilege  Unit                        权限不足

    #Query InboxViewQuery UserProfile Lower:Int Upper:Int
        UserId Id
        Name
        EmailAddress
        MailCount Count(Mails)
        Mails Select(Mails (Range OrderBy Id-) Lower Upper) // 容器均映射为列表；选择器包括选择索引和排序索引，均应在Record中标记才能使用，方向完全相反的两个排序索引可以互相使用
            Id
            Title
            FromId
            Time
            ContentBrief Content
                PlainText None() // TaggedUnion的每个Alternative的映射表达式只能为自身或空，不能进行复杂运算
                RichText None()
            IsNew
            AttachmentCount Count(Attachments)

## 词法分析

直接使用NiveumTree中的词法分析。

## 文法定义

    QueryBody ::= QueryMappingSpec

    QueryMappingSpec ::= SingleLineLiteral
                       | SingleLineLiteral QueryMappingExpr

    QueryMappingExpr ::= SingleLineLiteral
                       | QueryFunctionExpr? QueryMappingExpr*

    QueryFunctionExpr ::= "None" "()"
                        | "Count" "(" SingleLineLiteral ")"
                        | "Select" "(" SingleLineLiteral QueryFilter SingleLineLiteral* ")"

    QueryFilter ::= "(" Numeral ("By" BySpec)? ("OrderBy" OrderBySpec)? ")"

    Numeral ::= "Optional" | "One" | "Many" | "All" | "Range" | "Count"

    BySpec ::= SingleLineLiteral
            | "(" SingleLineLiteral* ")"

    OrderBySpec ::= SingleLineLiteral "-"?
                  | "(" (SingleLineLiteral "-"?)* ")"

## 文法分析

文法比较简单，不需要比较复杂的文法分析。

## 语义

    None():Unit //返回空类型
    Count(Variable:SingleLineLiteral):Int //获取List、Set、Map长度
    Select(Variable:SingleLineLiteral, Filter:QueryFilter, Arguments:List<String>):? //查询选择器，返回类型与Filter中的Numeral相关，Arguments的内容为Query的顶层参数
