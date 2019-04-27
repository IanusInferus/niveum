#pragma once

#include "ExpressionSchema.h"

#include <memory>
#include <string>
#include <functional>
#include <vector>
#include <unordered_map>
#include "BaseSystem/Any.h"

namespace Niveum
{
    namespace Expression
    {
        int pow(int Left, int Right);
        double pow(double Left, double Right);
        int mod(int v, int m);
        int div(int Left, int Right);
        int round(double v);
        int floor(double v);
        int ceil(double v);
        double round(double v, int NumFractionDigit);
        double floor(double v, int NumFractionDigit);
        double ceil(double v, int NumFractionDigit);
        int min(int v1, int v2);
        int max(int v1, int v2);
        int clamp(int v, int LowerBound, int UpperBound);
        double min(double v1, double v2);
        double max(double v1, double v2);
        double clamp(double v, double LowerBound, double UpperBound);
        int abs(int v);
        double rand();
        int rand(int LowerBound, int UpperBoundExclusive);
        double rand(double LowerBound, double UpperBoundExclusive);

        class ExpressionParameterContext
        {
        public:
            std::unordered_map<std::u16string, Any> Parameters;
        };

        class ExpressionParameterTypeProvider
        {
        public:
            std::unordered_map<std::u16string, Niveum::ExpressionSchema::PrimitiveType> Parameters;

            ExpressionParameterTypeProvider()
            {
            }

            ExpressionParameterTypeProvider(std::shared_ptr<std::vector<std::shared_ptr<Niveum::ExpressionSchema::VariableDef>>> ParameterDefs)
            {
                for (int k = 0; k < static_cast<int>(ParameterDefs->size()); k += 1)
                {
                    auto p = (*ParameterDefs)[k];
                    Parameters[p->Name] = p->Type;
                }
            }
        };

        class ExpressionCalculator
        {
        public:
            ExpressionCalculator()
            {
            }

            template <typename T>
            std::function<T(ExpressionParameterContext &)> BuildExpression(ExpressionParameterTypeProvider &eptp, std::shared_ptr<Niveum::ExpressionSchema::Expr> e)
            {
                auto f = BuildExpr(eptp, e);
                return AnyCast<std::function<T(ExpressionParameterContext &)>>(f);
            }

        private:
            Any BuildExpr(ExpressionParameterTypeProvider &eptp, std::shared_ptr<Niveum::ExpressionSchema::Expr> e);
        };
    }
}
