#include "Calculation.h"

#include <assert.h>

static void TestBasic(void)
{
    //等于/不等于
    assert(ExprTest_Test_CaseA01() == true);
    assert(ExprTest_Test_CaseA02() == false);
    assert(ExprTest_Test_CaseA03() == false);
    assert(ExprTest_Test_CaseA04() == true);
    assert(ExprTest_Test_CaseA05() == false);
    assert(ExprTest_Test_CaseA06() == true);
    assert(ExprTest_Test_CaseA07() == true);
    assert(ExprTest_Test_CaseA08() == false);
    assert(ExprTest_Test_CaseA09() == true);
    assert(ExprTest_Test_CaseA10() == false);
    assert(ExprTest_Test_CaseA11() == false);
    assert(ExprTest_Test_CaseA12() == true);

    //特殊运算符
    assert(ExprTest_Test_CaseB01());
    assert(ExprTest_Test_CaseB02());
    assert(ExprTest_Test_CaseB03());
    assert(ExprTest_Test_CaseB04());
    assert(ExprTest_Test_CaseB05());
    //ExprTest_Test_CaseB06(); //integer division by zero
    assert(ExprTest_Test_CaseB07());
    assert(ExprTest_Test_CaseB08());
    assert(ExprTest_Test_CaseB09());
    assert(ExprTest_Test_CaseB10());
    assert(ExprTest_Test_CaseB11());
    //ExprTest_Test_CaseB12(); //integer division by zero
    assert(ExprTest_Test_CaseB13());
    assert(ExprTest_Test_CaseB14());
    assert(ExprTest_Test_CaseB15());
    assert(ExprTest_Test_CaseB16());
    assert(ExprTest_Test_CaseB17());
    //ExprTest_Test_CaseB18(); //integer division by zero
    assert(ExprTest_Test_CaseB19());
    assert(ExprTest_Test_CaseB20());

    //算术运算
    assert(ExprTest_Test_CaseC01());
    assert(ExprTest_Test_CaseC02());
    assert(ExprTest_Test_CaseC03());
    assert(ExprTest_Test_CaseC04());
    assert(ExprTest_Test_CaseC05());
    assert(ExprTest_Test_CaseC06());
    assert(ExprTest_Test_CaseC07());
    assert(ExprTest_Test_CaseC08());
    assert(ExprTest_Test_CaseC09());
    assert(ExprTest_Test_CaseC10());
    assert(ExprTest_Test_CaseC11());
    assert(ExprTest_Test_CaseC12());
    assert(ExprTest_Test_CaseC13());
    assert(ExprTest_Test_CaseC14());
    assert(ExprTest_Test_CaseC15());
    assert(ExprTest_Test_CaseC16());
    assert(ExprTest_Test_CaseC17());
    assert(ExprTest_Test_CaseC18());
    assert(ExprTest_Test_CaseC19());
    assert(ExprTest_Test_CaseC20());
    assert(ExprTest_Test_CaseC21());
    assert(ExprTest_Test_CaseC22());
    assert(ExprTest_Test_CaseC23());
    assert(ExprTest_Test_CaseC24());
    assert(ExprTest_Test_CaseC25());
    assert(ExprTest_Test_CaseC26());
    assert(ExprTest_Test_CaseC27());
    assert(ExprTest_Test_CaseC28());
    assert(ExprTest_Test_CaseC29());
    assert(ExprTest_Test_CaseC30());

    //逻辑运算
    assert(ExprTest_Test_CaseD01());
    assert(ExprTest_Test_CaseD02());

    //关系运算
    assert(ExprTest_Test_CaseE01());
    assert(ExprTest_Test_CaseE02());
    assert(ExprTest_Test_CaseE03());
    assert(ExprTest_Test_CaseE04());
    assert(ExprTest_Test_CaseE05());
    assert(ExprTest_Test_CaseE06());
    assert(ExprTest_Test_CaseE07());
    assert(ExprTest_Test_CaseE08());
    assert(ExprTest_Test_CaseE09());
    assert(ExprTest_Test_CaseE10());
    assert(ExprTest_Test_CaseE11());
    assert(ExprTest_Test_CaseE12());
    assert(ExprTest_Test_CaseE13());
    assert(ExprTest_Test_CaseE14());
    assert(ExprTest_Test_CaseE15());
    assert(ExprTest_Test_CaseE16());
    assert(ExprTest_Test_CaseE17());
    assert(ExprTest_Test_CaseE18());
    assert(ExprTest_Test_CaseE19());
    assert(ExprTest_Test_CaseE20());
    assert(ExprTest_Test_CaseE21());
    assert(ExprTest_Test_CaseE22());
    assert(ExprTest_Test_CaseE23());
    assert(ExprTest_Test_CaseE24());

    //取整运算
    assert(ExprTest_Test_CaseF01());
    assert(ExprTest_Test_CaseF02());
    assert(ExprTest_Test_CaseF03());
    assert(ExprTest_Test_CaseF04());
    assert(ExprTest_Test_CaseF05());
    assert(ExprTest_Test_CaseF06());
    assert(ExprTest_Test_CaseF07());
    assert(ExprTest_Test_CaseF08());
    assert(ExprTest_Test_CaseF09());
    assert(ExprTest_Test_CaseF10());
    assert(ExprTest_Test_CaseF11());
    assert(ExprTest_Test_CaseF12());
    assert(ExprTest_Test_CaseF13());
    assert(ExprTest_Test_CaseF14());
    assert(ExprTest_Test_CaseF15());
    assert(ExprTest_Test_CaseF16());
    assert(ExprTest_Test_CaseF17());
    assert(ExprTest_Test_CaseF18());
    assert(ExprTest_Test_CaseF19());
    assert(ExprTest_Test_CaseF20());
    assert(ExprTest_Test_CaseF21());
    assert(ExprTest_Test_CaseF22());
    assert(ExprTest_Test_CaseF23());
    assert(ExprTest_Test_CaseF24());
    assert(ExprTest_Test_CaseF25());
    assert(ExprTest_Test_CaseF26());
    assert(ExprTest_Test_CaseF27());
    assert(ExprTest_Test_CaseF28());
    assert(ExprTest_Test_CaseF29());
    assert(ExprTest_Test_CaseF30());
    assert(ExprTest_Test_CaseF31());
    assert(ExprTest_Test_CaseF32());
    assert(ExprTest_Test_CaseF33());
    assert(ExprTest_Test_CaseF34());
    assert(ExprTest_Test_CaseF35());
    assert(ExprTest_Test_CaseF36());
    assert(ExprTest_Test_CaseF37());
    assert(ExprTest_Test_CaseF38());
    assert(ExprTest_Test_CaseF39());
    assert(ExprTest_Test_CaseF40());
    assert(ExprTest_Test_CaseF41());
    assert(ExprTest_Test_CaseF42());
    assert(ExprTest_Test_CaseF43());
    assert(ExprTest_Test_CaseF44());
    assert(ExprTest_Test_CaseF45());
    assert(ExprTest_Test_CaseF46());
    assert(ExprTest_Test_CaseF47());
    assert(ExprTest_Test_CaseF48());
    assert(ExprTest_Test_CaseF49());
    assert(ExprTest_Test_CaseF50());
    assert(ExprTest_Test_CaseF51());
    assert(ExprTest_Test_CaseF52());
    assert(ExprTest_Test_CaseF53());
    assert(ExprTest_Test_CaseF54());
    assert(ExprTest_Test_CaseF55());
    assert(ExprTest_Test_CaseF56());
    assert(ExprTest_Test_CaseF57());
    assert(ExprTest_Test_CaseF58());
    assert(ExprTest_Test_CaseF59());
    assert(ExprTest_Test_CaseF60());
    assert(ExprTest_Test_CaseF61());
    assert(ExprTest_Test_CaseF62());
    assert(ExprTest_Test_CaseF63());
    assert(ExprTest_Test_CaseF64());
    assert(ExprTest_Test_CaseF65());
    assert(ExprTest_Test_CaseF66());
    assert(ExprTest_Test_CaseF67());
    assert(ExprTest_Test_CaseF68());
    assert(ExprTest_Test_CaseF69());
    assert(ExprTest_Test_CaseF70());
    assert(ExprTest_Test_CaseF71());
    assert(ExprTest_Test_CaseF72());
    assert(ExprTest_Test_CaseF73());
    assert(ExprTest_Test_CaseF74());
    assert(ExprTest_Test_CaseF75());
    assert(ExprTest_Test_CaseF76());
    assert(ExprTest_Test_CaseF77());
    assert(ExprTest_Test_CaseF78());
    assert(ExprTest_Test_CaseF79());
    assert(ExprTest_Test_CaseF80());
    assert(ExprTest_Test_CaseF81());
    assert(ExprTest_Test_CaseF82());
    assert(ExprTest_Test_CaseF83());
    assert(ExprTest_Test_CaseF84());
    assert(ExprTest_Test_CaseF85());
    assert(ExprTest_Test_CaseF86());
    assert(ExprTest_Test_CaseF87());
    assert(ExprTest_Test_CaseF88());
    assert(ExprTest_Test_CaseF89());
    assert(ExprTest_Test_CaseF90());
    assert(ExprTest_Test_CaseF91());
    assert(ExprTest_Test_CaseF92());
    assert(ExprTest_Test_CaseF93());
    assert(ExprTest_Test_CaseF94());
    assert(ExprTest_Test_CaseF95());
    assert(ExprTest_Test_CaseF96());
    assert(ExprTest_Test_CaseF97());
    assert(ExprTest_Test_CaseF98());
    assert(ExprTest_Test_CaseF99());
    assert(ExprTest_Test_CaseFA0());
    assert(ExprTest_Test_CaseFA1());
    assert(ExprTest_Test_CaseFA2());
    assert(ExprTest_Test_CaseFA3());
    assert(ExprTest_Test_CaseFA4());
    assert(ExprTest_Test_CaseFA5());
    assert(ExprTest_Test_CaseFA6());
    assert(ExprTest_Test_CaseFA7());
    assert(ExprTest_Test_CaseFA8());
    assert(ExprTest_Test_CaseFA9());
    assert(ExprTest_Test_CaseFB0());
    assert(ExprTest_Test_CaseFB1());
    assert(ExprTest_Test_CaseFB2());
    assert(ExprTest_Test_CaseFB3());
    assert(ExprTest_Test_CaseFB4());

    //范围限制运算
    assert(ExprTest_Test_CaseG01());
    assert(ExprTest_Test_CaseG02());
    assert(ExprTest_Test_CaseG03());
    assert(ExprTest_Test_CaseG04());
    assert(ExprTest_Test_CaseG05());
    assert(ExprTest_Test_CaseG06());
    assert(ExprTest_Test_CaseG07());
    assert(ExprTest_Test_CaseG08());
    assert(ExprTest_Test_CaseG09());
    assert(ExprTest_Test_CaseG10());
    assert(ExprTest_Test_CaseG11());
    assert(ExprTest_Test_CaseG12());
    assert(ExprTest_Test_CaseG13());
    assert(ExprTest_Test_CaseG14());
    assert(ExprTest_Test_CaseG15());
    assert(ExprTest_Test_CaseG16());
    assert(ExprTest_Test_CaseG17());
    assert(ExprTest_Test_CaseG18());
    assert(ExprTest_Test_CaseG19());
    assert(ExprTest_Test_CaseG20());
    assert(ExprTest_Test_CaseG21());
    assert(ExprTest_Test_CaseG22());
    assert(ExprTest_Test_CaseG23());
    assert(ExprTest_Test_CaseG24());

    //其他运算
    assert(ExprTest_Test_CaseH01());
    assert(ExprTest_Test_CaseH02());
    assert(ExprTest_Test_CaseH03());
    assert(ExprTest_Test_CaseH04());
    for (int k = 0; k < 100; k += 1)
    {
        assert(ExprTest_Test_CaseH05());
        assert(ExprTest_Test_CaseH06());
        assert(ExprTest_Test_CaseH07());
        assert(ExprTest_Test_CaseH08());
        assert(ExprTest_Test_CaseH09());
        assert(ExprTest_Test_CaseH10());
    }
    assert(ExprTest_Test_CaseH11());
    assert(ExprTest_Test_CaseH12());
}

int main(int argc, char **argv)
{
    assert(ExprTest_Character_GetUpgradeExperience(1, 2) == 2);

    TestBasic();
    return 0;
}
