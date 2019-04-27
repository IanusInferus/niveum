//==========================================================================
//
//  File:        Program.cpp
//  Location:    Niveum.Examples <C++ 2011>
//  Description: 表达式计算工具
//  Version:     2019.04.28.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "ExpressionSchema.h"
#include "ExpressionSchemaBinary.h"
#include "Streams.h"
#include "Calculation.h"
#include "BaseSystem/StringUtilities.h"

#include <memory>
#include <exception>
#include <cwchar>
#include <cassert>

using namespace Niveum::ExpressionSchema;

namespace ExprTest
{
    class Program
    {
    public:
        static int MainInner(int argc, char **argv)
        {
            DisplayInfo();

            Test();

            return 0;
        }

        static void DisplayInfo()
        {
            std::wprintf(L"%ls\n", L"表达式测试:");
            std::wprintf(L"%ls\n", L"ExprTest, Public Domain");
            std::wprintf(L"%ls\n", L"F.R.C.");
        }

        static void Test()
        {
            std::shared_ptr<Assembly> a;
            {
                ReadableStream s(u"Assembly.bin");
                a = BinaryTranslator::AssemblyFromBinary(s);
            }

            auto c = std::make_shared<Calculation>(a);
            assert(c->Character->GetUpgradeExperience(1, 2) == 2);

            TestBasic(c);
        }

        static void TestBasic(std::shared_ptr<Calculation> c)
        {
            //等于/不等于
            assert(c->Test->CaseA01() == true);
            assert(c->Test->CaseA02() == false);
            assert(c->Test->CaseA03() == false);
            assert(c->Test->CaseA04() == true);
            assert(c->Test->CaseA05() == false);
            assert(c->Test->CaseA06() == true);
            assert(c->Test->CaseA07() == true);
            assert(c->Test->CaseA08() == false);
            assert(c->Test->CaseA09() == true);
            assert(c->Test->CaseA10() == false);
            assert(c->Test->CaseA11() == false);
            assert(c->Test->CaseA12() == true);

            //特殊运算符
            assert(c->Test->CaseB01());
            assert(c->Test->CaseB02());
            assert(c->Test->CaseB03());
            assert(c->Test->CaseB04());
            assert(c->Test->CaseB05());
            try
            {
                assert(c->Test->CaseB06());
                assert(false);
            }
            catch (...)
            {
                assert(true);
            }
            assert(c->Test->CaseB07());
            assert(c->Test->CaseB08());
            assert(c->Test->CaseB09());
            assert(c->Test->CaseB10());
            assert(c->Test->CaseB11());
            try
            {
                assert(c->Test->CaseB12());
                assert(false);
            }
            catch (...)
            {
                assert(true);
            }
            assert(c->Test->CaseB13());
            assert(c->Test->CaseB14());
            assert(c->Test->CaseB15());
            assert(c->Test->CaseB16());
            assert(c->Test->CaseB17());
            try
            {
                assert(c->Test->CaseB18());
                assert(false);
            }
            catch (...)
            {
                assert(true);
            }
            assert(c->Test->CaseB19());
            assert(c->Test->CaseB20());

            //算术运算
            assert(c->Test->CaseC01());
            assert(c->Test->CaseC02());
            assert(c->Test->CaseC03());
            assert(c->Test->CaseC04());
            assert(c->Test->CaseC05());
            assert(c->Test->CaseC06());
            assert(c->Test->CaseC07());
            assert(c->Test->CaseC08());
            assert(c->Test->CaseC09());
            assert(c->Test->CaseC10());
            assert(c->Test->CaseC11());
            assert(c->Test->CaseC12());
            assert(c->Test->CaseC13());
            assert(c->Test->CaseC14());
            assert(c->Test->CaseC15());
            assert(c->Test->CaseC16());
            assert(c->Test->CaseC17());
            assert(c->Test->CaseC18());
            assert(c->Test->CaseC19());
            assert(c->Test->CaseC20());
            assert(c->Test->CaseC21());
            assert(c->Test->CaseC22());
            assert(c->Test->CaseC23());
            assert(c->Test->CaseC24());
            assert(c->Test->CaseC25());
            assert(c->Test->CaseC26());
            assert(c->Test->CaseC27());
            assert(c->Test->CaseC28());
            assert(c->Test->CaseC29());
            assert(c->Test->CaseC30());

            //逻辑运算
            assert(c->Test->CaseD01());
            assert(c->Test->CaseD02());

            //关系运算
            assert(c->Test->CaseE01());
            assert(c->Test->CaseE02());
            assert(c->Test->CaseE03());
            assert(c->Test->CaseE04());
            assert(c->Test->CaseE05());
            assert(c->Test->CaseE06());
            assert(c->Test->CaseE07());
            assert(c->Test->CaseE08());
            assert(c->Test->CaseE09());
            assert(c->Test->CaseE10());
            assert(c->Test->CaseE11());
            assert(c->Test->CaseE12());
            assert(c->Test->CaseE13());
            assert(c->Test->CaseE14());
            assert(c->Test->CaseE15());
            assert(c->Test->CaseE16());
            assert(c->Test->CaseE17());
            assert(c->Test->CaseE18());
            assert(c->Test->CaseE19());
            assert(c->Test->CaseE20());
            assert(c->Test->CaseE21());
            assert(c->Test->CaseE22());
            assert(c->Test->CaseE23());
            assert(c->Test->CaseE24());

            //取整运算
            assert(c->Test->CaseF01());
            assert(c->Test->CaseF02());
            assert(c->Test->CaseF03());
            assert(c->Test->CaseF04());
            assert(c->Test->CaseF05());
            assert(c->Test->CaseF06());
            assert(c->Test->CaseF07());
            assert(c->Test->CaseF08());
            assert(c->Test->CaseF09());
            assert(c->Test->CaseF10());
            assert(c->Test->CaseF11());
            assert(c->Test->CaseF12());
            assert(c->Test->CaseF13());
            assert(c->Test->CaseF14());
            assert(c->Test->CaseF15());
            assert(c->Test->CaseF16());
            assert(c->Test->CaseF17());
            assert(c->Test->CaseF18());
            assert(c->Test->CaseF19());
            assert(c->Test->CaseF20());
            assert(c->Test->CaseF21());
            assert(c->Test->CaseF22());
            assert(c->Test->CaseF23());
            assert(c->Test->CaseF24());
            assert(c->Test->CaseF25());
            assert(c->Test->CaseF26());
            assert(c->Test->CaseF27());
            assert(c->Test->CaseF28());
            assert(c->Test->CaseF29());
            assert(c->Test->CaseF30());
            assert(c->Test->CaseF31());
            assert(c->Test->CaseF32());
            assert(c->Test->CaseF33());
            assert(c->Test->CaseF34());
            assert(c->Test->CaseF35());
            assert(c->Test->CaseF36());
            assert(c->Test->CaseF37());
            assert(c->Test->CaseF38());
            assert(c->Test->CaseF39());
            assert(c->Test->CaseF40());
            assert(c->Test->CaseF41());
            assert(c->Test->CaseF42());
            assert(c->Test->CaseF43());
            assert(c->Test->CaseF44());
            assert(c->Test->CaseF45());
            assert(c->Test->CaseF46());
            assert(c->Test->CaseF47());
            assert(c->Test->CaseF48());
            assert(c->Test->CaseF49());
            assert(c->Test->CaseF50());
            assert(c->Test->CaseF51());
            assert(c->Test->CaseF52());
            assert(c->Test->CaseF53());
            assert(c->Test->CaseF54());
            assert(c->Test->CaseF55());
            assert(c->Test->CaseF56());
            assert(c->Test->CaseF57());
            assert(c->Test->CaseF58());
            assert(c->Test->CaseF59());
            assert(c->Test->CaseF60());
            assert(c->Test->CaseF61());
            assert(c->Test->CaseF62());
            assert(c->Test->CaseF63());
            assert(c->Test->CaseF64());
            assert(c->Test->CaseF65());
            assert(c->Test->CaseF66());
            assert(c->Test->CaseF67());
            assert(c->Test->CaseF68());
            assert(c->Test->CaseF69());
            assert(c->Test->CaseF70());
            assert(c->Test->CaseF71());
            assert(c->Test->CaseF72());
            assert(c->Test->CaseF73());
            assert(c->Test->CaseF74());
            assert(c->Test->CaseF75());
            assert(c->Test->CaseF76());
            assert(c->Test->CaseF77());
            assert(c->Test->CaseF78());
            assert(c->Test->CaseF79());
            assert(c->Test->CaseF80());
            assert(c->Test->CaseF81());
            assert(c->Test->CaseF82());
            assert(c->Test->CaseF83());
            assert(c->Test->CaseF84());
            assert(c->Test->CaseF85());
            assert(c->Test->CaseF86());
            assert(c->Test->CaseF87());
            assert(c->Test->CaseF88());
            assert(c->Test->CaseF89());
            assert(c->Test->CaseF90());
            assert(c->Test->CaseF91());
            assert(c->Test->CaseF92());
            assert(c->Test->CaseF93());
            assert(c->Test->CaseF94());
            assert(c->Test->CaseF95());
            assert(c->Test->CaseF96());
            assert(c->Test->CaseF97());
            assert(c->Test->CaseF98());
            assert(c->Test->CaseF99());
            assert(c->Test->CaseFA0());
            assert(c->Test->CaseFA1());
            assert(c->Test->CaseFA2());
            assert(c->Test->CaseFA3());
            assert(c->Test->CaseFA4());
            assert(c->Test->CaseFA5());
            assert(c->Test->CaseFA6());
            assert(c->Test->CaseFA7());
            assert(c->Test->CaseFA8());
            assert(c->Test->CaseFA9());
            assert(c->Test->CaseFB0());
            assert(c->Test->CaseFB1());
            assert(c->Test->CaseFB2());
            assert(c->Test->CaseFB3());
            assert(c->Test->CaseFB4());

            //范围限制运算
            assert(c->Test->CaseG01());
            assert(c->Test->CaseG02());
            assert(c->Test->CaseG03());
            assert(c->Test->CaseG04());
            assert(c->Test->CaseG05());
            assert(c->Test->CaseG06());
            assert(c->Test->CaseG07());
            assert(c->Test->CaseG08());
            assert(c->Test->CaseG09());
            assert(c->Test->CaseG10());
            assert(c->Test->CaseG11());
            assert(c->Test->CaseG12());
            assert(c->Test->CaseG13());
            assert(c->Test->CaseG14());
            assert(c->Test->CaseG15());
            assert(c->Test->CaseG16());
            assert(c->Test->CaseG17());
            assert(c->Test->CaseG18());
            assert(c->Test->CaseG19());
            assert(c->Test->CaseG20());
            assert(c->Test->CaseG21());
            assert(c->Test->CaseG22());
            assert(c->Test->CaseG23());
            assert(c->Test->CaseG24());

            //其他运算
            assert(c->Test->CaseH01());
            assert(c->Test->CaseH02());
            assert(c->Test->CaseH03());
            assert(c->Test->CaseH04());
            for (int k = 0; k < 100; k += 1)
            {
                assert(c->Test->CaseH05());
                assert(c->Test->CaseH06());
                assert(c->Test->CaseH07());
                assert(c->Test->CaseH08());
                assert(c->Test->CaseH09());
                assert(c->Test->CaseH10());
            }
            assert(c->Test->CaseH11());
            assert(c->Test->CaseH12());
        }
    };
}

#ifdef _MSC_VER

#include <io.h>
#include <fcntl.h>

void ModifyStdoutUnicode()
{
    _setmode(_fileno(stdout), _O_U16TEXT);
}

#else

void ModifyStdoutUnicode()
{
}

#endif

#include <locale.h>

void SetLocale()
{
    setlocale(LC_ALL, "");
}

int main(int argc, char **argv)
{
    ModifyStdoutUnicode();
    SetLocale();

    try
    {
        return ExprTest::Program::MainInner(argc, argv);
    }
    catch (std::exception &ex)
    {
        std::wprintf(L"Error:\n%ls\n", systemToWideChar(ex.what()).c_str());
        return -1;
    }
}
