#include "ExpressionCalculator.h"
#include "BaseSystem/StringUtilities.h"
#include "BaseSystem/ThreadLocalRandom.h"

#include <stdexcept>
#include <cmath>

using namespace Yuki::ExpressionSchema;
using namespace Yuki::Expression;

static BaseSystem::ThreadLocalRandom RNG;

namespace Yuki
{
    namespace Expression
    {
        int pow(int Left, int Right)
        {
            if (Right < 0) { throw std::logic_error("InvalidOperation"); }
            int n = 30;
            for (int k = 30; k >= 0; k -= 1)
            {
                if ((Right & (1 << k)) != 0)
                {
                    n = k;
                    break;
                }
            }
            int v = 1;
            int b = Left;
            for (int k = 0; k <= n; k += 1)
            {
                if ((Right & (1 << k)) != 0)
                {
                    v *= b;
                }
                b *= b;
            }
            return v;
        }

        double pow(double Left, double Right)
        {
            return std::pow(Left, Right);
        }

        double exp(double v)
        {
            return std::exp(v);
        }

        double log(double v)
        {
            return std::log(v);
        }

        int mod(int v, int m)
        {
            auto r = v % m;
            if ((r < 0 && m > 0) || (r > 0 && m < 0)) { r += m; }
            return r;
        }

        int div(int Left, int Right)
        {
            auto r = mod(Left, Right);
            return (Left - r) / Right;
        }

        int round(double v)
        {
            int l = static_cast<int>(std::floor(v));
            int u = static_cast<int>(std::ceil(v));
            if (l == u) { return l; }
            if ((l & 1) != 0)
            {
                if (u - v <= 0.5)
                {
                    return u;
                }
                else
                {
                    return l;
                }
            }
            else
            {
                if (v - l <= 0.5)
                {
                    return l;
                }
                else
                {
                    return u;
                }
            }
        }

        int floor(double v)
        {
            return round(std::floor(v));
        }

        int ceil(double v)
        {
            return round(std::ceil(v));
        }

        double round(double v, int NumFractionDigit)
        {
            auto vv = v * Expression::pow(10.0, (double)(NumFractionDigit));
            auto scale = Expression::pow(0.1, (double)(NumFractionDigit));
            int l = static_cast<int>(std::floor(vv));
            int u = static_cast<int>(std::ceil(vv));
            if (l == u) { return l * scale; }
            if ((l & 1) != 0)
            {
                if (u - vv <= 0.5)
                {
                    return u * scale;
                }
                else
                {
                    return l * scale;
                }
            }
            else
            {
                if (vv - l <= 0.5)
                {
                    return l * scale;
                }
                else
                {
                    return u * scale;
                }
            }
        }

        double floor(double v, int NumFractionDigit)
        {
            return std::floor(v * Expression::pow(10.0, (double)(NumFractionDigit))) * Expression::pow(0.1, (double)(NumFractionDigit));
        }

        double ceil(double v, int NumFractionDigit)
        {
            return std::ceil(v * Expression::pow(10.0, (double)(NumFractionDigit))) * Expression::pow(0.1, (double)(NumFractionDigit));
        }

        int min(int v1, int v2)
        {
            return v1 <= v2 ? v1 : v2;
        }

        int max(int v1, int v2)
        {
            return v1 <= v2 ? v2 : v1;
        }

        int clamp(int v, int LowerBound, int UpperBound)
        {
            if (v <= LowerBound)
            {
                return LowerBound;
            }
            if (v >= UpperBound)
            {
                return UpperBound;
            }
            return v;
        }

        double min(double v1, double v2)
        {
            return v1 <= v2 ? v1 : v2;
        }

        double max(double v1, double v2)
        {
            return v1 <= v2 ? v2 : v1;
        }

        double clamp(double v, double LowerBound, double UpperBound)
        {
            if (v <= LowerBound)
            {
                return LowerBound;
            }
            if (v >= UpperBound)
            {
                return UpperBound;
            }
            return v;
        }

        int abs(int v)
        {
            return std::abs(v);
        }

        double abs(double v)
        {
            return std::abs(v);
        }

        double rand()
        {
            return RNG.NextReal<double>(0, 1);
        }

        int rand(int LowerBound, int UpperBoundExclusive)
        {
            return RNG.NextInt<int>(LowerBound, UpperBoundExclusive - 1);
        }

        double rand(double LowerBound, double UpperBoundExclusive)
        {
            return RNG.NextReal<double>(LowerBound, UpperBoundExclusive);
        }
    }
}

static bool MatchFuncType(Any ParameterFunc, PrimitiveType t)
{
    if (t == PrimitiveType::Boolean && ParameterFunc.type() == typeid(std::function<bool(ExpressionParameterContext &)>))
    {
        return true;
    }
    if (t == PrimitiveType::Int && ParameterFunc.type() == typeid(std::function<int(ExpressionParameterContext &)>))
    {
        return true;
    }
    if (t == PrimitiveType::Real && ParameterFunc.type() == typeid(std::function<double(ExpressionParameterContext &)>))
    {
        return true;
    }
    return false;
}

static bool MatchFunctionNameAndParameters(std::wstring NameFunc, std::shared_ptr<std::vector<Any>> ParameterFuncs, std::wstring Name)
{
    if (NameFunc != Name) { return false; }
    if (ParameterFuncs->size() != 0) { return false; }
    return true;
}
static bool MatchFunctionNameAndParameters(std::wstring NameFunc, std::shared_ptr<std::vector<Any>> ParameterFuncs, std::wstring Name, PrimitiveType t1)
{
    if (NameFunc != Name) { return false; }
    if (ParameterFuncs->size() != 1) { return false; }
    if (!MatchFuncType((*ParameterFuncs)[0], t1)) { return false; }
    return true;
}
static bool MatchFunctionNameAndParameters(std::wstring NameFunc, std::shared_ptr<std::vector<Any>> ParameterFuncs, std::wstring Name, PrimitiveType t1, PrimitiveType t2)
{
    if (NameFunc != Name) { return false; }
    if (ParameterFuncs->size() != 2) { return false; }
    if (!MatchFuncType((*ParameterFuncs)[0], t1)) { return false; }
    if (!MatchFuncType((*ParameterFuncs)[1], t2)) { return false; }
    return true;
}
static bool MatchFunctionNameAndParameters(std::wstring NameFunc, std::shared_ptr<std::vector<Any>> ParameterFuncs, std::wstring Name, PrimitiveType t1, PrimitiveType t2, PrimitiveType t3)
{
    if (NameFunc != Name) { return false; }
    if (ParameterFuncs->size() != 3) { return false; }
    if (!MatchFuncType((*ParameterFuncs)[0], t1)) { return false; }
    if (!MatchFuncType((*ParameterFuncs)[1], t2)) { return false; }
    if (!MatchFuncType((*ParameterFuncs)[2], t3)) { return false; }
    return true;
}

Any ExpressionCalculator::BuildExpr(ExpressionParameterTypeProvider &eptp, std::shared_ptr<Expr> e)
{
    if (e->OnLiteral())
    {
        if (e->Literal->OnBooleanValue())
        {
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext & epc) { return e->Literal->BooleanValue; });
        }
        else if (e->Literal->OnIntValue())
        {
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return e->Literal->IntValue; });
        }
        else if (e->Literal->OnRealValue())
        {
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return e->Literal->RealValue; });
        }
        else
        {
            throw std::logic_error("InvalidOperation");
        }
    }
    else if (e->OnVariable())
    {
        auto Name = e->Variable->Name;
        if (eptp.Parameters.count(Name) == 0)
        {
            throw std::logic_error(w2s(L"ParameterNotExist: " + Name));
        }
        auto t = eptp.Parameters[Name];
        if (t == PrimitiveType::Boolean)
        {
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return AnyCast<bool>(epc.Parameters[Name]); });
        }
        else if (t == PrimitiveType::Int)
        {
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return AnyCast<int>(epc.Parameters[Name]); });
        }
        else if (t == PrimitiveType::Real)
        {
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return AnyCast<double>(epc.Parameters[Name]); });
        }
        else
        {
            throw std::logic_error("InvalidOperation");
        }
    }
    else if (e->OnFunction())
    {
        auto Name = e->Function->Name;
        auto ParameterFuncs = std::make_shared<std::vector<Any>>();
        for (auto p : e->Function->Parameters)
        {
            ParameterFuncs->push_back(BuildExpr(eptp, p));
        }

        //算术运算
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"+", PrimitiveType::Int))
        {
            auto Operand = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return +Operand(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"-", PrimitiveType::Int))
        {
            auto Operand = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return -Operand(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"+", PrimitiveType::Real))
        {
            auto Operand = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return +Operand(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"-", PrimitiveType::Real))
        {
            auto Operand = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return -Operand(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"+", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) + Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"-", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) - Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"*", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) * Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"/", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return (double)(Left(epc)) / (double)(Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"+", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) + Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"-", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) - Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"*", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) * Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"/", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) / Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"pow", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::pow(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"pow", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::pow(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"exp", PrimitiveType::Real))
        {
            auto Operand = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::exp(Operand(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"log", PrimitiveType::Real))
        {
            auto Operand = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::log(Operand(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"mod", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::mod(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"div", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::div(Left(epc), Right(epc)); });
        }

        //逻辑运算
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"!", PrimitiveType::Boolean))
        {
            auto Operand = AnyCast<std::function<bool(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return !Operand(epc); });
        }

        //关系运算
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"<", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) < Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L">", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) > Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"<=", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) <= Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L">=", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) >= Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"==", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) == Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"!=", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) != Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"<", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) < Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L">", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) > Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"<=", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) <= Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L">=", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) >= Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"==", PrimitiveType::Boolean, PrimitiveType::Boolean))
        {
            auto Left = AnyCast<std::function<bool(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<bool(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) == Right(epc); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"!=", PrimitiveType::Boolean, PrimitiveType::Boolean))
        {
            auto Left = AnyCast<std::function<bool(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<bool(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) != Right(epc); });
        }

        //取整运算
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"round", PrimitiveType::Real))
        {
            auto Operand = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::round(Operand(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"floor", PrimitiveType::Real))
        {
            auto Operand = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::floor(Operand(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"ceil", PrimitiveType::Real))
        {
            auto Operand = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::ceil(Operand(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"round", PrimitiveType::Real, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::round(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"floor", PrimitiveType::Real, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::floor(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"ceil", PrimitiveType::Real, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::ceil(Left(epc), Right(epc)); });
        }

        //范围限制运算
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"min", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::min(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"max", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::max(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"clamp", PrimitiveType::Int, PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Arg0 = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Arg1 = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            auto Arg2 = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[2]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::clamp(Arg0(epc), Arg1(epc), Arg2(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"min", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::min(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"max", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::max(Left(epc), Right(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"clamp", PrimitiveType::Real, PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Arg0 = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Arg1 = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            auto Arg2 = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[2]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::clamp(Arg0(epc), Arg1(epc), Arg2(epc)); });
        }

        //其他运算
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"abs", PrimitiveType::Int))
        {
            auto Operand = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::abs(Operand(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"abs", PrimitiveType::Real))
        {
            auto Operand = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::abs(Operand(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"rand"))
        {
            return std::function<double(ExpressionParameterContext &)>([](ExpressionParameterContext &epc) { return Expression::rand(); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"rand", PrimitiveType::Int, PrimitiveType::Int))
        {
            auto Arg0 = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Arg1 = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::rand(Arg0(epc), Arg1(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"rand", PrimitiveType::Real, PrimitiveType::Real))
        {
            auto Arg0 = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            auto Arg1 = AnyCast<std::function<double(ExpressionParameterContext &)>>((*ParameterFuncs)[1]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Expression::rand(Arg0(epc), Arg1(epc)); });
        }
        if (MatchFunctionNameAndParameters(Name, ParameterFuncs, L"creal", PrimitiveType::Int))
        {
            auto Operand = AnyCast<std::function<int(ExpressionParameterContext &)>>((*ParameterFuncs)[0]);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Operand(epc); });
        }

        throw std::logic_error("NotSupported");
    }
    else if (e->OnIf())
    {
        auto l = BuildExpr(eptp, e->If->TruePart);
        auto r = BuildExpr(eptp, e->If->FalsePart);
        if (l.type() != r.type())
        {
            throw std::logic_error("InvalidOperation");
        }
        auto Condition = BuildExpression<bool>(eptp, e->If->Condition);
        auto &t = l.type();
        if (t == typeid(std::function<bool(ExpressionParameterContext &)>))
        {
            auto Left = AnyCast<std::function<bool(ExpressionParameterContext &)>>(l);
            auto Right = AnyCast<std::function<bool(ExpressionParameterContext &)>>(r);
            return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Condition(epc) ? Left(epc) : Right(epc); });
        }
        else if (t == typeid(std::function<int(ExpressionParameterContext &)>))
        {
            auto Left = AnyCast<std::function<int(ExpressionParameterContext &)>>(l);
            auto Right = AnyCast<std::function<int(ExpressionParameterContext &)>>(r);
            return std::function<int(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Condition(epc) ? Left(epc) : Right(epc); });
        }
        else if (t == typeid(std::function<double(ExpressionParameterContext &)>))
        {
            auto Left = AnyCast<std::function<double(ExpressionParameterContext &)>>(l);
            auto Right = AnyCast<std::function<double(ExpressionParameterContext &)>>(r);
            return std::function<double(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Condition(epc) ? Left(epc) : Right(epc); });
        }
        else
        {
            throw std::logic_error("InvalidOperation");
        }
    }
    else if (e->OnAndAlso())
    {
        auto Left = BuildExpression<bool>(eptp, e->AndAlso->Left);
        auto Right = BuildExpression<bool>(eptp, e->AndAlso->Right);
        return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) && Right(epc); });
    }
    else if (e->OnOrElse())
    {
        auto Left = BuildExpression<bool>(eptp, e->OrElse->Left);
        auto Right = BuildExpression<bool>(eptp, e->OrElse->Right);
        return std::function<bool(ExpressionParameterContext &)>([=](ExpressionParameterContext &epc) { return Left(epc) || Right(epc); });
    }
    else
    {
        throw std::logic_error("InvalidOperation");
    }
}
