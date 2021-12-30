# NiveumRelation语言规范

## 概述

NiveumRelation语言，是内嵌于NiveumTree语言的类型定义语言，主要用于描述关系数据库用的基于关系的数据结构，以生成各个数据库的创建和数据导入代码。

\* 目前由于很多数据库支持了JSON字段(如PostgreSQL)，正在考虑重新设计。

## 示例

以下为一个邮件的例子。

    #Entity Mail                                        [CN:Mails]邮件
        Id                  Int64                       [I]邮件ID
        Title               String                      [P:64]标题
        FromId              Int                         发件用户ID
        Time                String                      "[P:32]时间(UTC)：yyyy-MM-ddTHH:mm:ssZ形式"
        Content             String                      [P:512]内容

        //导航属性
        From                UserProfile                 [FK:FromId=Id]来源用户
        Tos                 List<MailTo>                [RFK:Id=Id]收件人
        Attachments         List<MailAttachment>        [RFK:Id=Id]附件
        Owners              List<MailOwner>             [RFK:Id=Id]邮件所有关系

    #Entity MailTo                                      "[CN:MailTos][PK:Id, ToId][NKC:Id][NK:ToId]邮件收件人"
        Id                  Int64                       邮件ID
        ToId                Int                         收件用户ID

        //导航属性
        Mail                Mail                        [FK:Id=Id]邮件

    #Entity MailAttachment                              "[CN:MailAttachments][PK:Id, Name]邮件附件"
        Id                  Int64                       邮件ID
        Name                String                      [P:128]名称
        Content             List<Byte>                  内容

        //导航属性
        Mail                Mail                        [FK:Id=Id]邮件

    #Entity MailOwner                                   "[CN:MailOwners][PK:Id, OwnerId][NKC:OwnerId, Time-][NK:Id]邮件所有关系"
        Id                  Int64                       邮件ID
        OwnerId             Int                         所有者用户ID
        IsNew               Boolean                     是否是新邮件
        Time                String                      "[P:32]时间(UTC)：yyyy-MM-ddTHH:mm:ssZ形式"

        //导航属性
        Mail                Mail                        [FK:Id=Id]邮件

    #Query
        From Mail Select One By Id
        From MailTo Select Many By Id
        From MailOwner Select One By (Id OwnerId)
        From MailOwner Select Count By Id
        From MailOwner Select Count By (Id OwnerId)
        From MailOwner Select Many By Id
        From MailOwner Select Count By OwnerId
        From MailOwner Select Range By OwnerId OrderBy (OwnerId Time-)
        From MailAttachment Select Many By Id

        From Mail Insert One
        From MailTo Insert Many
        From MailOwner Insert Many
        From MailAttachment Insert Many
        From MailOwner Update One
        From Mail Delete One By Id
        From MailTo Delete Many By Id
        From MailOwner Delete One By (Id OwnerId)
        From MailAttachment Delete Many By Id

## 概念

基于关系的数据结构是指由

    基元类型(Primitive)
    实体(Entity)
    枚举(Enum)

组成的数据结构。

### 基元类型 Primitive

没有泛型参数，指由所有相关外部系统均理解的基础类型，如32位有符号整数、字符串等。

### 实体 Entity

没有泛型参数，表示一个数据表，有多个字段。

### 枚举 Enum

没有泛型参数，由若干标签名和对应的整数组成。

### 查询 Query

表示Select、Lock、Insert、Update、Upsert、Delete等6种基本操作。

## 支持矩阵

功能 vs 语言

|            | C# | C++2017 | XHTML |
|:----------:|:--:|:-------:|:-----:|
| 类型定义   | √ | √      | √    |
| 只读数据库 | √ | √      | 无需  |
| Krustallos | √ | ×      | 无需  |
| SQL Server | √ | ×      | 无需  |
| MySQL      | √ | ×      | 无需  |

## 词法分析

直接使用NiveumObject中的词法分析。

## 文法分析

文法非常简单，基本上在词法分析的结果上可以直接对应。
