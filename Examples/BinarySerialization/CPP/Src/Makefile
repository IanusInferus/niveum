# Makefile
#

CC =	g++-4.7
PROG =	DataCopy

.PHONY: all clean

all:	$(PROG)

clean:
	rm -f DataCopy *.o

$(PROG): Utility.o Program.o
	$(CC) Utility.o Program.o -o $(PROG)
	chmod a+x $(PROG)

%.o:%.cpp
	$(CC) -std=c++11 -I../Lib_Gcc $(patsubst %.o,%.cpp,$@) -c -o $@

Program.cpp: Utility.h World.h WorldBinary.h Streams.h
Utility.cpp: Utility.h
