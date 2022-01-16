#pragma once

#ifdef __cplusplus
extern "C" {
#endif

void Niveum_Expression_init_runtime(void);

int Niveum_Expression_pow_II(int Left, int Right);
double Niveum_Expression_pow_RR(double Left, double Right);
double Niveum_Expression_exp_R(double v);
double Niveum_Expression_log_R(double v);
int Niveum_Expression_mod_II(int v, int m);
int Niveum_Expression_div_II(int Left, int Right);
int Niveum_Expression_round_R(double v);
int Niveum_Expression_floor_R(double v);
int Niveum_Expression_ceil_R(double v);
double Niveum_Expression_round_RI(double v, int NumFractionDigit);
double Niveum_Expression_floor_RI(double v, int NumFractionDigit);
double Niveum_Expression_ceil_RI(double v, int NumFractionDigit);
int Niveum_Expression_min_II(int v1, int v2);
int Niveum_Expression_max_II(int v1, int v2);
int Niveum_Expression_clamp_III(int v, int LowerBound, int UpperBound);
double Niveum_Expression_min_RR(double v1, double v2);
double Niveum_Expression_max_RR(double v1, double v2);
double Niveum_Expression_clamp_RRR(double v, double LowerBound, double UpperBound);
int Niveum_Expression_abs_I(int v);
double Niveum_Expression_abs_R(double v);
double Niveum_Expression_rand_V(void);
int Niveum_Expression_rand_II(int LowerBound, int UpperBoundExclusive);
double Niveum_Expression_rand_RR(double LowerBound, double UpperBoundExclusive);
double Niveum_Expression_creal_I(int v);

#ifdef __cplusplus
}
#endif
