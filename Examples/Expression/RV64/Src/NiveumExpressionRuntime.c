#include "NiveumExpressionRuntime.h"

#include <math.h>
#include <stdlib.h>
#include <time.h>

void Niveum_Expression_init_runtime(void)
{
    srand((unsigned int)(time(NULL)));
}

int Niveum_Expression_pow_II(int Left, int Right)
{
    if (Right < 0) { abort(); }
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

double Niveum_Expression_pow_RR(double Left, double Right)
{
    return pow(Left, Right);
}

double Niveum_Expression_exp_R(double v)
{
    return exp(v);
}

double Niveum_Expression_log_R(double v)
{
    return log(v);
}

int Niveum_Expression_mod_II(int v, int m)
{
    int r = v % m;
    if ((r < 0 && m > 0) || (r > 0 && m < 0)) { r += m; }
    return r;
}

int Niveum_Expression_div_II(int Left, int Right)
{
    int r = Niveum_Expression_mod_II(Left, Right);
    return (Left - r) / Right;
}

int Niveum_Expression_round_R(double v)
{
    int l = (int)(floor(v));
    int u = (int)(ceil(v));
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

int Niveum_Expression_floor_R(double v)
{
    return Niveum_Expression_round_R(floor(v));
}

int Niveum_Expression_ceil_R(double v)
{
    return Niveum_Expression_round_R(ceil(v));
}

double Niveum_Expression_round_RI(double v, int NumFractionDigit)
{
    double vv = v * Niveum_Expression_pow_RR(10.0, (double)(NumFractionDigit));
    double scale = Niveum_Expression_pow_RR(0.1, (double)(NumFractionDigit));
    int l = (int)(floor(vv));
    int u = (int)(ceil(vv));
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

double Niveum_Expression_floor_RI(double v, int NumFractionDigit)
{
    return floor(v * Niveum_Expression_pow_RR(10.0, (double)(NumFractionDigit))) * Niveum_Expression_pow_RR(0.1, (double)(NumFractionDigit));
}

double Niveum_Expression_ceil_RI(double v, int NumFractionDigit)
{
    return ceil(v * Niveum_Expression_pow_RR(10.0, (double)(NumFractionDigit))) * Niveum_Expression_pow_RR(0.1, (double)(NumFractionDigit));
}

int Niveum_Expression_min_II(int v1, int v2)
{
    return v1 <= v2 ? v1 : v2;
}

int Niveum_Expression_max_II(int v1, int v2)
{
    return v1 <= v2 ? v2 : v1;
}

int Niveum_Expression_clamp_III(int v, int LowerBound, int UpperBound)
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

double Niveum_Expression_min_RR(double v1, double v2)
{
    return v1 <= v2 ? v1 : v2;
}

double Niveum_Expression_max_RR(double v1, double v2)
{
    return v1 <= v2 ? v2 : v1;
}

double Niveum_Expression_clamp_RRR(double v, double LowerBound, double UpperBound)
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

int Niveum_Expression_abs_I(int v)
{
    return abs(v);
}

double Niveum_Expression_abs_R(double v)
{
    return fabs(v);
}

double Niveum_Expression_rand_V(void)
{
    return rand() / (RAND_MAX + 1.0);
}

int Niveum_Expression_rand_II(int LowerBound, int UpperBoundExclusive)
{
    return LowerBound + rand() % (UpperBoundExclusive - LowerBound);
}

double Niveum_Expression_rand_RR(double LowerBound, double UpperBoundExclusive)
{
    return LowerBound + Niveum_Expression_rand_V() * (UpperBoundExclusive - LowerBound);
}

double Niveum_Expression_creal_I(int v)
{
    return (double)(v);
}
