#!/bin/bash
set -e

options=(--sysroot=/root/riscv64-linux-musl-native --prefix=/root/riscv64-linux-musl-native/usr/lib/gcc/riscv64-linux-musl/11.2.1 -L/root/riscv64-linux-musl-native/usr/lib/gcc/riscv64-linux-musl/11.2.1)

mkdir -p ../Bin
clang -g -target riscv64-linux-musl ${options[@]} -march=rv64g -mabi=lp64d -c Calculation.s -o ../Bin/Calculation.o
clang -g -target riscv64-linux-musl ${options[@]} -march=rv64g -mabi=lp64d -c NiveumExpressionRuntime.c -o ../Bin/NiveumExpressionRuntime.o
clang -g -target riscv64-linux-musl ${options[@]} -march=rv64g -mabi=lp64d -c Program.c -o ../Bin/Program.o
clang -g -target riscv64-linux-musl ${options[@]} -march=rv64g -mabi=lp64d -static -o ../Bin/program ../Bin/Calculation.o ../Bin/NiveumExpressionRuntime.o ../Bin/Program.o
